using System.Windows;
using System.Windows.Threading;
using DesktopAudioController.Services;
using DesktopAudioController.ViewModels;
using DesktopAudioController.Views;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopAudioController;

/// <summary>
/// 애플리케이션 시작 시 서비스와 초기 창을 구성하는 진입점입니다.
/// </summary>
public partial class App : System.Windows.Application
{
    internal sealed record AudioRuntimeRecoveryResult(
        MainViewModel ViewModel,
        IAudioNotificationService NotificationService,
        bool NotificationServiceStartDeferred);

    private sealed class AudioServicesBundle
    {
        public required IAudioDeviceCatalogService DeviceCatalogService { get; init; }

        public required IAudioSessionService SessionService { get; init; }

        public required IAudioNotificationService NotificationService { get; init; }

        public required IProcessMetadataCacheService ProcessMetadataCacheService { get; init; }
    }

    // 부팅 직후 Core Audio 열거가 멎는 경우 UI가 영원히 안 뜨지 않도록 초기 로드 시간 제한을 둡니다.
    private static readonly TimeSpan InitialDeviceLoadTimeout = TimeSpan.FromSeconds(6);

    // 설정 파일 입출력을 담당하는 서비스입니다.
    private ISettingsService? _settingsService;

    // 출력 장치 목록을 조회하는 서비스입니다.
    private IAudioDeviceCatalogService? _audioDeviceCatalogService;

    // 장치별 애플리케이션 세션 목록을 조회하는 서비스입니다.
    private IAudioSessionService? _audioSessionService;

    // Windows 오디오 이벤트를 구독해 UI 새로고침을 트리거하는 서비스입니다.
    private IAudioNotificationService? _audioNotificationService;

    // 세션 앱 실행 파일 경로 기준으로 아이콘을 캐싱하는 서비스입니다.
    private IAppIconService? _appIconService;

    // PID 기준 프로세스 이름/실행 경로를 캐싱하는 서비스입니다.
    private IProcessMetadataCacheService? _processMetadataCacheService;

    // Windows 자동 실행 레지스트리 등록을 제어하는 서비스입니다.
    private IStartupLaunchService? _startupLaunchService;

    // GitHub 릴리즈 기준 새 버전 여부를 확인하는 서비스입니다.
    private IUpdateCheckService? _updateCheckService;

    // 이전 실행의 정상 종료 여부를 기록하는 상태 서비스입니다.
    private AppRunStateService? _appRunStateService;

    // 중복 실행을 막고 기존 인스턴스를 다시 활성화하는 서비스입니다.
    private SingleInstanceService? _singleInstanceService;

    // 메인 창 생성 전에 활성화 요청이 오면 창이 준비된 뒤 복원하도록 보류합니다.
    private bool _pendingExternalActivationRequest;

    // 현재 앱이 제한 모드인지 나타냅니다. 설정 저장 보호와 수동 복구 경로에서 사용합니다.
    private bool _isInDegradedMode;

    // 부팅 직후 열거 타임아웃이 난 제한 모드는 같은 프로세스에서 복구하지 않고 재시작 복구로 넘깁니다.
    private bool _degradedModeRecoveryRequiresRestart;

    // 제한 모드 복구는 한 번에 하나만 진행합니다.
    private readonly SemaphoreSlim _audioRuntimeRecoverySemaphore = new(1, 1);

    // 타임아웃된 백그라운드 복구 작업이 아직 끝나지 않았으면 새 시도를 막습니다.
    private readonly PendingRecoveryTaskGate<AudioServicesBundle> _pendingTimedOutAudioRecoveryGate = new();

    // 수동 복구 시 서비스 재생성은 UI를 오래 붙잡지 않도록 별도 시간 제한을 둡니다.
    private static readonly TimeSpan DegradedRecoveryServiceCreationTimeout = TimeSpan.FromSeconds(8);

    // 제한 모드 복구 직후 알림 서비스 시작은 UI와 분리해 한 번에 하나만 백그라운드로 진행합니다.
    private readonly object _deferredNotificationStartSyncRoot = new();
    private Task? _deferredNotificationStartTask;

    // 재시작 복구는 한 번만 예약합니다.
    private int _restartRecoveryScheduled;

    internal bool IsInDegradedMode => _isInDegradedMode;
    internal bool RequiresRestartToRecoverDegradedMode => _isInDegradedMode && _degradedModeRecoveryRequiresRestart;

    /// <summary>
    /// 앱 시작 시 서비스 초기화, 메인 뷰모델 로드, 메인 창 표시를 수행합니다.
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppLog.Initialize();
        AppLog.Info("App", $"OnStartup args=[{string.Join(", ", e.Args)}]");
        if (!TryEnsurePrimaryInstance())
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += App_OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_OnUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_OnUnobservedTaskException;
        _appRunStateService = new AppRunStateService();

        // 현재 실행이 Windows 자동 실행 레지스트리를 통해 시작된 경우에만 true입니다.
        var isStartupLaunch = e.Args.Any(argument =>
            string.Equals(
                argument,
                RegistryStartupLaunchService.StartupLaunchArgument,
                StringComparison.OrdinalIgnoreCase));
        AppLog.Info("App", $"startupLaunch={isStartupLaunch}");
        var previousRunIncident = BeginRunStateTracking();

        _settingsService = new SettingsService();
        _startupLaunchService = new RegistryStartupLaunchService();
        _updateCheckService = new GitHubReleaseUpdateCheckService();
        _appIconService = new CachedAppIconService();

        var startupWarningMessage = TryInitializeAudioServices();

        // zip 배포 특성상 실행 경로가 바뀔 수 있으므로, 자동 실행이 켜져 있으면 현재 exe 경로로 재동기화합니다.
        var startupSettings = _settingsService.Load();
        if (startupSettings.RunAtWindowsStartup)
        {
            try
            {
                _startupLaunchService.Apply(true);
                AppLog.Info("App", "자동 실행 레지스트리 재동기화 완료");
            }
            catch
            {
                // 앱 시작 자체는 막지 않고, 사용자가 설정창을 다시 저장할 때 동일 오류를 안내합니다.
                AppLog.Warn("App", "자동 실행 레지스트리 재동기화 실패");
            }
        }

        var (mainViewModel, effectiveStartupWarningMessage) = await CreateMainViewModelAsync(startupWarningMessage);
        startupWarningMessage = effectiveStartupWarningMessage;

        // 메인 창은 설정 창 팩토리를 받아 필요할 때마다 새 설정 뷰모델을 생성합니다.
        var mainWindow = new MainWindow(
            mainViewModel,
            CreateSettingsViewModel,
            _audioNotificationService ?? throw new InvalidOperationException("Audio notification service is not initialized."),
            _settingsService,
            _updateCheckService,
            isStartupLaunch,
            !mainViewModel.HasConfiguredDevices || !string.IsNullOrWhiteSpace(startupWarningMessage) || previousRunIncident.Detected,
            startupWarningMessage,
            previousRunIncident);
        MainWindow = mainWindow;
        mainWindow.Show();
        if (_pendingExternalActivationRequest)
        {
            _pendingExternalActivationRequest = false;
            mainWindow.RestoreFromExternalActivation();
        }

        AppLog.Info("App", "메인 창 표시 완료");

        if (_settingsService.TryConsumeLoadWarning(out var warningMessage))
        {
            AppLog.Warn("App", $"설정 파일 복구 경고 표시 path={_settingsService.SettingsFilePath}");
            System.Windows.MessageBox.Show(
                mainWindow,
                warningMessage,
                "설정 파일 복구 안내",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        // 첫 실행처럼 표시할 장치가 아직 없으면 설정 창을 바로 띄웁니다.
        if (!mainViewModel.HasConfiguredDevices && string.IsNullOrWhiteSpace(startupWarningMessage))
        {
            AppLog.Info("App", "표시 장치 미설정 상태로 첫 실행 설정창 표시");
            mainWindow.OpenSettingsOnFirstRun();
        }
    }

    private async Task<(MainViewModel ViewModel, string? WarningMessage)> CreateMainViewModelAsync(string? startupWarningMessage)
    {
        var mainViewModel = CreateMainViewModelForCurrentServices();

        try
        {
            AppLog.Info("App", "초기 장치 로드 시작");
            await mainViewModel.LoadAsync().WaitAsync(InitialDeviceLoadTimeout);
            AppLog.Info("App", $"초기 장치 로드 완료 visibleDevices={mainViewModel.VisibleDevices.Count} hasConfiguredDevices={mainViewModel.HasConfiguredDevices}");
            return (mainViewModel, startupWarningMessage);
        }
        catch (TimeoutException exception)
        {
            mainViewModel.InvalidatePendingSnapshots();
            AppLog.Error("App", "초기 장치 로드 타임아웃, 제한 모드로 전환", exception);
            startupWarningMessage = EnterDegradedMode(
                "부팅 직후 오디오 장치 초기화가 지연되어 제한 모드로 시작했습니다. 잠시 후 다시 실행해 주세요.",
                requiresRestartForRecovery: true);
        }
        catch (Exception exception)
        {
            AppLog.Error("App", "초기 장치 로드 실패, 제한 모드로 전환", exception);
            startupWarningMessage = EnterDegradedMode("오디오 장치 초기화에 실패해 제한 모드로 시작했습니다. 로그를 확인한 뒤 앱을 다시 실행해 주세요.");
        }

        var degradedMainViewModel = CreateMainViewModelForCurrentServices();
        degradedMainViewModel.Load();
        AppLog.Info("App", $"제한 모드 장치 로드 완료 visibleDevices={degradedMainViewModel.VisibleDevices.Count} hasConfiguredDevices={degradedMainViewModel.HasConfiguredDevices}");
        return (degradedMainViewModel, startupWarningMessage);
    }

    /// <summary>
    /// 앱 종료 시 네이티브 오디오 서비스가 잡고 있는 COM 리소스를 정리합니다.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        AppLog.Info("App", "OnExit 시작");
        AppLog.Info("App", "OnExit 장치 서비스 Dispose 시작");
        (_audioDeviceCatalogService as IDisposable)?.Dispose();
        AppLog.Info("App", "OnExit 장치 서비스 Dispose 완료");

        AppLog.Info("App", "OnExit 세션 서비스 Dispose 시작");
        (_audioSessionService as IDisposable)?.Dispose();
        AppLog.Info("App", "OnExit 세션 서비스 Dispose 완료");

        AppLog.Info("App", "OnExit 알림 서비스 Dispose 시작");
        _audioNotificationService?.Dispose();
        AppLog.Info("App", "OnExit 알림 서비스 Dispose 완료");
        AppLog.Info("App", "OnExit 단일 인스턴스 서비스 Dispose 시작");
        _singleInstanceService?.Dispose();
        AppLog.Info("App", "OnExit 단일 인스턴스 서비스 Dispose 완료");
        AppLog.Info("App", "OnExit 완료");
        TryMarkCleanShutdown();
        base.OnExit(e);
    }

    /// <summary>
    /// 설정 창 전용 뷰모델을 새로 생성합니다.
    /// </summary>
    private SettingsViewModel CreateSettingsViewModel()
    {
        return new SettingsViewModel(
            _settingsService ?? throw new InvalidOperationException("Settings service is not initialized."),
            _audioDeviceCatalogService ?? throw new InvalidOperationException("Audio device service is not initialized."),
            _startupLaunchService ?? throw new InvalidOperationException("Startup launch service is not initialized."),
            _isInDegradedMode);
    }

    private string? TryInitializeAudioServices()
    {
        try
        {
            ApplyAudioServices(CreateNativeAudioServices());
            _isInDegradedMode = false;
            _degradedModeRecoveryRequiresRestart = false;
            return null;
        }
        catch (Exception exception)
        {
            AppLog.Error("App", "오디오 서비스 초기화 실패, 제한 모드로 전환", exception);
            return EnterDegradedMode("오디오 초기화에 실패해 제한 모드로 시작했습니다. 일부 오디오 제어 기능은 비활성화됩니다.");
        }
    }

    private string EnterDegradedMode(string warningMessage, bool requiresRestartForRecovery = false)
    {
        var audioNotificationServiceToDispose = _audioNotificationService;
        var audioSessionServiceToDispose = _audioSessionService;
        var audioDeviceCatalogServiceToDispose = _audioDeviceCatalogService;
        _audioDeviceCatalogService = UnavailableAudioServices.CreateDeviceCatalogService();
        _audioSessionService = UnavailableAudioServices.CreateSessionService();
        _audioNotificationService = UnavailableAudioServices.CreateNotificationService();
        _isInDegradedMode = true;
        _degradedModeRecoveryRequiresRestart = requiresRestartForRecovery;
        AppLog.Warn("App", $"제한 모드 진입 reason={warningMessage}");
        _ = DisposeAudioServicesForRecoveryAsync(
            audioNotificationServiceToDispose,
            audioSessionServiceToDispose,
            audioDeviceCatalogServiceToDispose);
        return warningMessage;
    }

    internal async Task<AudioRuntimeRecoveryResult?> TryRecoverFromDegradedModeAsync(string reason)
    {
        if (!_isInDegradedMode)
        {
            AppLog.Debug("App", $"제한 모드 복구 생략 reason={reason} degradedMode=False");
            return null;
        }

        await _audioRuntimeRecoverySemaphore.WaitAsync();
        try
        {
            if (!_isInDegradedMode)
            {
                AppLog.Debug("App", $"제한 모드 복구 생략 reason={reason} degradedMode=False postWait");
                return null;
            }

            if (!_pendingTimedOutAudioRecoveryGate.TryAcquireNewAttempt(out _))
            {
                AppLog.Warn("App", $"제한 모드 복구 생략: 이전 복구 작업 진행 중 reason={reason}");
                return null;
            }

            AppLog.Info("App", $"제한 모드 복구 시도 시작 reason={reason}");

            AudioServicesBundle audioServices;
            try
            {
                var recoveryTask = Task.Run(() =>
                {
                    AppLog.Info("App", $"제한 모드 복구용 오디오 서비스 생성 시작 reason={reason}");
                    var services = CreateNativeAudioServices(startNotificationService: false);
                    AppLog.Info("App", $"제한 모드 복구용 오디오 서비스 생성 완료 reason={reason}");
                    return services;
                });

                var completedTask = await Task.WhenAny(
                    recoveryTask,
                    Task.Delay(DegradedRecoveryServiceCreationTimeout));
                if (!ReferenceEquals(completedTask, recoveryTask))
                {
                    _pendingTimedOutAudioRecoveryGate.Track(
                        recoveryTask,
                        completedRecoveryTask => HandleTimedOutRecoveryTaskCompletion(completedRecoveryTask, reason));
                    throw new TimeoutException("The operation has timed out.");
                }

                audioServices = await recoveryTask;
            }
            catch (TimeoutException exception)
            {
                AppLog.Error("App", $"제한 모드 복구 실패: 오디오 서비스 재생성 타임아웃 reason={reason}", exception);
                return null;
            }
            catch (Exception exception)
            {
                AppLog.Error("App", $"제한 모드 복구 실패: 오디오 서비스 재생성 실패 reason={reason}", exception);
                return null;
            }

            var previousNotificationService = _audioNotificationService;
            var previousSessionService = _audioSessionService;
            var previousDeviceCatalogService = _audioDeviceCatalogService;
            ApplyAudioServices(audioServices);
            _isInDegradedMode = false;
            _degradedModeRecoveryRequiresRestart = false;

            TryDispose(previousNotificationService);
            TryDispose(previousSessionService as IDisposable);
            TryDispose(previousDeviceCatalogService as IDisposable);

            AppLog.Info("App", $"제한 모드 복구 성공 reason={reason}");
            return new AudioRuntimeRecoveryResult(
                CreateMainViewModelForCurrentServices(),
                _audioNotificationService ?? throw new InvalidOperationException("Audio notification service is not initialized."),
                NotificationServiceStartDeferred: true);
        }
        finally
        {
            _audioRuntimeRecoverySemaphore.Release();
        }
    }

    internal bool TryScheduleRestartRecovery(string reason)
    {
        if (Interlocked.Exchange(ref _restartRecoveryScheduled, 1) != 0)
        {
            AppLog.Warn("App", $"제한 모드 재시작 복구 생략: 이미 예약됨 reason={reason}");
            return true;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            AppLog.Warn("App", $"제한 모드 재시작 복구 실패: 실행 파일 경로 없음 reason={reason}");
            Interlocked.Exchange(ref _restartRecoveryScheduled, 0);
            return false;
        }

        try
        {
            AppLog.Info("App", $"제한 모드 재시작 복구 예약 reason={reason} executablePath={executablePath}");
            Process.Start(new ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/c ping -n 3 127.0.0.1 > nul && start \"\" \"{executablePath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            Shutdown();
            return true;
        }
        catch (Exception exception)
        {
            AppLog.Error("App", $"제한 모드 재시작 복구 실패 reason={reason}", exception);
            Interlocked.Exchange(ref _restartRecoveryScheduled, 0);
            return false;
        }
    }

    internal void EnsureRecoveredAudioNotificationServiceStarted(string reason)
    {
        if (_audioNotificationService is null)
        {
            return;
        }

        lock (_deferredNotificationStartSyncRoot)
        {
            if (_deferredNotificationStartTask is { IsCompleted: false })
            {
                AppLog.Debug("App", $"복구 후 오디오 알림 서비스 시작 생략: 이미 진행 중 reason={reason}");
                return;
            }

            var notificationService = _audioNotificationService;
            var startTask = Task.Run(() =>
            {
                AppLog.Info("App", $"복구 후 오디오 알림 서비스 시작 reason={reason}");
                notificationService.Start();
                AppLog.Info("App", $"복구 후 오디오 알림 서비스 시작 완료 reason={reason}");
            });

            _deferredNotificationStartTask = startTask;
            _ = startTask.ContinueWith(
                completedTask =>
                {
                    try
                    {
                        if (completedTask.Exception is not null)
                        {
                            AppLog.Warn(
                                "App",
                                $"복구 후 오디오 알림 서비스 시작 실패 reason={reason}",
                                completedTask.Exception.GetBaseException());
                        }
                    }
                    finally
                    {
                        lock (_deferredNotificationStartSyncRoot)
                        {
                            if (ReferenceEquals(_deferredNotificationStartTask, completedTask))
                            {
                                _deferredNotificationStartTask = null;
                            }
                        }
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private PreviousRunIncident BeginRunStateTracking()
    {
        if (_appRunStateService is null)
        {
            return PreviousRunIncident.None;
        }

        try
        {
            return _appRunStateService.BeginRun();
        }
        catch (Exception exception)
        {
            AppLog.Warn("App", "실행 상태 추적 시작 실패", exception);
            return PreviousRunIncident.None;
        }
    }

    private MainViewModel CreateMainViewModelForCurrentServices()
    {
        return new MainViewModel(
            _settingsService ?? throw new InvalidOperationException("Settings service is not initialized."),
            _audioDeviceCatalogService ?? throw new InvalidOperationException("Audio device service is not initialized."),
            _audioSessionService ?? throw new InvalidOperationException("Audio session service is not initialized."),
            _appIconService ?? throw new InvalidOperationException("App icon service is not initialized."));
    }

    private void ApplyAudioServices(AudioServicesBundle audioServices)
    {
        _audioDeviceCatalogService = audioServices.DeviceCatalogService;
        _audioSessionService = audioServices.SessionService;
        _audioNotificationService = audioServices.NotificationService;
        _processMetadataCacheService = audioServices.ProcessMetadataCacheService;
    }

    private static AudioServicesBundle CreateNativeAudioServices(bool startNotificationService = true)
    {
        IAudioDeviceCatalogService? audioDeviceCatalogService = null;
        IAudioSessionService? audioSessionService = null;
        IAudioNotificationService? audioNotificationService = null;
        IProcessMetadataCacheService? processMetadataCacheService = null;

        try
        {
            audioDeviceCatalogService = new NativeAudioDeviceCatalogService();
            processMetadataCacheService = new CachedProcessMetadataService();
            audioSessionService = new NativeAudioSessionService(processMetadataCacheService!);
            audioNotificationService = new NativeAudioNotificationService();
            if (startNotificationService)
            {
                audioNotificationService.Start();
            }

            return new AudioServicesBundle
            {
                DeviceCatalogService = audioDeviceCatalogService!,
                SessionService = audioSessionService!,
                NotificationService = audioNotificationService!,
                ProcessMetadataCacheService = processMetadataCacheService!
            };
        }
        catch
        {
            TryDispose(audioNotificationService);
            TryDispose(audioSessionService as IDisposable);
            TryDispose(audioDeviceCatalogService as IDisposable);
            throw;
        }
    }

    private static void HandleTimedOutRecoveryTaskCompletion(Task<AudioServicesBundle> completedTask, string reason)
    {
        if (completedTask.IsCompletedSuccessfully)
        {
            AppLog.Warn("App", $"제한 모드 복구용 오디오 서비스 생성 지연 완료 후 결과 폐기 reason={reason}");
            DisposeAudioServicesBundle(completedTask.Result);
            return;
        }

        if (completedTask.Exception is not null)
        {
            AppLog.Warn(
                "App",
                $"제한 모드 복구용 오디오 서비스 생성 지연 실패 reason={reason}",
                completedTask.Exception.GetBaseException());
        }
    }

    private bool TryEnsurePrimaryInstance()
    {
        try
        {
            _singleInstanceService = new SingleInstanceService();
            if (_singleInstanceService.TryAcquirePrimaryInstance())
            {
                _singleInstanceService.StartActivationListener(HandleExternalActivationRequestAsync);
                AppLog.Info("App", "단일 인스턴스 가드 활성화 완료");
                return true;
            }

            AppLog.Warn("App", "이미 실행 중인 인스턴스 감지");
            var notified = _singleInstanceService
                .TryNotifyExistingInstanceAsync(TimeSpan.FromSeconds(2))
                .GetAwaiter()
                .GetResult();
            AppLog.Info("App", $"기존 인스턴스 활성화 요청 결과 success={notified}");
            return false;
        }
        catch (Exception exception)
        {
            AppLog.Warn("App", "단일 인스턴스 가드 초기화 실패, 보호 없이 계속 진행", exception);
            TryDispose(_singleInstanceService);
            _singleInstanceService = null;
            return true;
        }
    }

    private Task HandleExternalActivationRequestAsync()
    {
        return Dispatcher.InvokeAsync(() =>
        {
            AppLog.Info("App", "기존 인스턴스 활성화 요청 수신");
            if (MainWindow is MainWindow mainWindow)
            {
                mainWindow.RestoreFromExternalActivation();
                return;
            }

            _pendingExternalActivationRequest = true;
        }).Task;
    }

    private void TryMarkCleanShutdown()
    {
        if (_appRunStateService is null)
        {
            return;
        }

        try
        {
            _appRunStateService.MarkCleanShutdown();
        }
        catch (Exception exception)
        {
            AppLog.Warn("App", "정상 종료 상태 기록 실패", exception);
        }
    }

    private void TryMarkUnexpectedTermination()
    {
        if (_appRunStateService is null)
        {
            return;
        }

        try
        {
            _appRunStateService.MarkUnexpectedTermination();
        }
        catch (Exception exception)
        {
            AppLog.Warn("App", "비정상 종료 상태 기록 실패", exception);
        }
    }

    private Task DisposeAudioServicesForRecoveryAsync(
        IAudioNotificationService? audioNotificationService,
        IAudioSessionService? audioSessionService,
        IAudioDeviceCatalogService? audioDeviceCatalogService)
    {
        if (audioNotificationService is null && audioSessionService is null && audioDeviceCatalogService is null)
        {
            return Task.CompletedTask;
        }

        AppLog.Info("App", "제한 모드 복구용 오디오 서비스 비동기 정리 예약");
        return Task.Run(() =>
        {
            AppLog.Info("App", "제한 모드 복구용 오디오 서비스 Dispose 시작");
            TryDispose(audioNotificationService);
            TryDispose(audioSessionService as IDisposable);
            TryDispose(audioDeviceCatalogService as IDisposable);
            AppLog.Info("App", "제한 모드 복구용 오디오 서비스 Dispose 완료");
        });
    }

    private static void TryDispose(IDisposable? disposable)
    {
        if (disposable is null)
        {
            return;
        }

        try
        {
            disposable.Dispose();
        }
        catch
        {
            // 제한 모드 복구 과정의 dispose 실패는 이후 fallback 경로를 막지 않습니다.
        }
    }

    private static void DisposeAudioServicesBundle(AudioServicesBundle audioServices)
    {
        TryDispose(audioServices.NotificationService);
        TryDispose(audioServices.SessionService as IDisposable);
        TryDispose(audioServices.DeviceCatalogService as IDisposable);
    }

    /// <summary>
    /// WPF UI 스레드 예외를 로그에 남깁니다.
    /// </summary>
    private void App_OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        TryMarkUnexpectedTermination();
        AppLog.Error("App", "DispatcherUnhandledException", e.Exception);
    }

    /// <summary>
    /// 처리되지 않은 AppDomain 예외를 로그에 남깁니다.
    /// </summary>
    private void CurrentDomain_OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.IsTerminating)
        {
            TryMarkUnexpectedTermination();
        }

        AppLog.Error("App", $"UnhandledException isTerminating={e.IsTerminating}", e.ExceptionObject as Exception);
    }

    /// <summary>
    /// 관찰되지 않은 Task 예외를 로그에 남깁니다.
    /// </summary>
    private void TaskScheduler_OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AppLog.Error("App", "UnobservedTaskException", e.Exception);
    }
}
