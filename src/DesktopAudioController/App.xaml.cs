using System.Windows;
using System.Windows.Threading;
using DesktopAudioController.Services;
using DesktopAudioController.ViewModels;
using DesktopAudioController.Views;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DesktopAudioController;

/// <summary>
/// 애플리케이션 시작 시 서비스와 초기 창을 구성하는 진입점입니다.
/// </summary>
public partial class App : System.Windows.Application
{
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

    /// <summary>
    /// 앱 시작 시 서비스 초기화, 메인 뷰모델 로드, 메인 창 표시를 수행합니다.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppLog.Initialize();
        AppLog.Info("App", $"OnStartup args=[{string.Join(", ", e.Args)}]");
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

        // 메인 화면에서 사용할 뷰모델을 만들고 저장된 설정 기준으로 데이터를 채웁니다.
        var mainViewModel = new MainViewModel(
            _settingsService,
            _audioDeviceCatalogService ?? throw new InvalidOperationException("Audio device service is not initialized."),
            _audioSessionService ?? throw new InvalidOperationException("Audio session service is not initialized."),
            _appIconService);

        try
        {
            mainViewModel.Load();
            AppLog.Info("App", $"초기 장치 로드 완료 visibleDevices={mainViewModel.VisibleDevices.Count} hasConfiguredDevices={mainViewModel.HasConfiguredDevices}");
        }
        catch (Exception exception)
        {
            AppLog.Error("App", "초기 장치 로드 실패, 제한 모드로 전환", exception);
            startupWarningMessage = EnterDegradedMode("오디오 장치 초기화에 실패해 제한 모드로 시작했습니다. 로그를 확인한 뒤 앱을 다시 실행해 주세요.");
            mainViewModel = new MainViewModel(
                _settingsService,
                _audioDeviceCatalogService ?? throw new InvalidOperationException("Audio device service is not initialized."),
                _audioSessionService ?? throw new InvalidOperationException("Audio session service is not initialized."),
                _appIconService);
            mainViewModel.Load();
            AppLog.Info("App", $"제한 모드 장치 로드 완료 visibleDevices={mainViewModel.VisibleDevices.Count} hasConfiguredDevices={mainViewModel.HasConfiguredDevices}");
        }

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
            _startupLaunchService ?? throw new InvalidOperationException("Startup launch service is not initialized."));
    }

    private string? TryInitializeAudioServices()
    {
        try
        {
            _audioDeviceCatalogService = new NativeAudioDeviceCatalogService();
            _processMetadataCacheService = new CachedProcessMetadataService();
            _audioSessionService = new NativeAudioSessionService(_processMetadataCacheService);
            _audioNotificationService = new NativeAudioNotificationService();
            _audioNotificationService.Start();
            return null;
        }
        catch (Exception exception)
        {
            AppLog.Error("App", "오디오 서비스 초기화 실패, 제한 모드로 전환", exception);
            return EnterDegradedMode("오디오 초기화에 실패해 제한 모드로 시작했습니다. 일부 오디오 제어 기능은 비활성화됩니다.");
        }
    }

    private string EnterDegradedMode(string warningMessage)
    {
        DisposeAudioServicesForRecovery();
        _audioDeviceCatalogService = UnavailableAudioServices.CreateDeviceCatalogService();
        _audioSessionService = UnavailableAudioServices.CreateSessionService();
        _audioNotificationService = UnavailableAudioServices.CreateNotificationService();
        AppLog.Warn("App", $"제한 모드 진입 reason={warningMessage}");
        return warningMessage;
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

    private void DisposeAudioServicesForRecovery()
    {
        TryDispose(_audioNotificationService);
        TryDispose(_audioSessionService as IDisposable);
        TryDispose(_audioDeviceCatalogService as IDisposable);
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
