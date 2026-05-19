using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using DesktopAudioController.Services;
using DesktopAudioController.ViewModels;
using System.Threading;
using System.Threading.Tasks;
using Forms = System.Windows.Forms;

namespace DesktopAudioController.Views;

/// <summary>
/// 장치 마스터 볼륨 카드 목록을 보여주는 메인 창입니다.
/// </summary>
public partial class MainWindow : Window
{
    private readonly record struct NotificationRefreshBatch(
        AudioNotificationChangeKind Kind,
        int StateEventCount,
        int TopologyEventCount)
    {
        public int TotalEventCount => StateEventCount + TopologyEventCount;
    }

    // 메인 화면 데이터와 바인딩되는 뷰모델입니다.
    private MainViewModel _viewModel;

    // 설정 창을 열 때마다 새 설정 뷰모델을 만들기 위한 팩토리입니다.
    private readonly Func<SettingsViewModel> _settingsViewModelFactory;

    // Core Audio 이벤트를 받아 메인 화면 갱신을 트리거하는 서비스입니다.
    private IAudioNotificationService _audioNotificationService;

    // 시작 최소화, 트레이 최소화 같은 창 동작 옵션을 읽는 설정 서비스입니다.
    private readonly ISettingsService _settingsService;

    // GitHub 릴리즈 기준 새 버전 존재 여부를 확인하는 서비스입니다.
    private readonly IUpdateCheckService _updateCheckService;

    // 마지막 업데이트 확인 결과를 보관해 수동 다운로드 버튼과 상태 문구에 재사용합니다.
    private UpdateCheckResult? _latestUpdateCheckResult;

    // 현재 실행이 Windows 자동 실행으로 시작된 경우에만 true입니다.
    private readonly bool _isStartupLaunch;

    // 첫 실행처럼 장치 설정이 필요한 경우 창을 강제로 보이게 유지하기 위한 플래그입니다.
    private readonly bool _forceVisibleOnStartup;

    // 창 닫기/최소화 때마다 settings.json을 다시 읽지 않도록 트레이 옵션을 메모리에 유지합니다.
    private bool _minimizeToTray = true;

    // 업데이트 확인 시 prerelease를 포함할지 여부도 설정 저장 직후 메모리에 반영합니다.
    private bool _includePreReleaseUpdates;

    // 시스템 트레이 영역에 표시할 아이콘 인스턴스입니다.
    private readonly Forms.NotifyIcon _notifyIcon;

    // 오디오 콜백 스레드와 UI 스레드가 함께 접근하는 새로고침 큐 상태를 직렬화하는 잠금 객체입니다.
    private readonly object _notificationRefreshSyncRoot = new();

    // 설정 창 중복 열기와 재활성화를 직렬화하는 잠금 객체입니다.
    private readonly object _settingsWindowSyncRoot = new();

    // 장치/세션 재조회는 한 번에 하나만 실행해 UI 반영 순서를 보장합니다.
    private readonly SemaphoreSlim _viewRefreshSemaphore = new(1, 1);

    // 같은 틱에서 중복 새로고침 요청이 들어올 때 한 번으로 합치기 위한 플래그입니다.
    private bool _isNotificationRefreshQueued;

    // 실제 새로고침 작업이 실행 중인지 나타냅니다. true인 동안 새 이벤트는 다음 1회로만 합칩니다.
    private bool _isNotificationRefreshProcessing;

    // 같은 틱 안에서 여러 이벤트가 섞이면 더 큰 범위의 갱신 종류를 보존하기 위한 필드입니다.
    private AudioNotificationChangeKind _pendingNotificationKind = AudioNotificationChangeKind.State;

    // 현재 배치에 합쳐진 상태 변경 이벤트 개수입니다.
    private int _pendingStateNotificationCount;

    // 현재 배치에 합쳐진 토폴로지 변경 이벤트 개수입니다.
    private int _pendingTopologyNotificationCount;

    // 사용자 의도로 종료하는 중인지 여부입니다. false면 닫기를 트레이 최소화로 전환합니다.
    private bool _isExitRequested;

    // 사용자를 GitHub 릴리즈 목록으로 보내는 고정 URL입니다.
    private static readonly string ReleasesPageUrl = "https://github.com/TailFox-Forge/DesktopAudioController/releases";

    // 상태 이벤트 폭주 시 즉시 재열거하지 않도록 짧게 모아두는 지연 시간입니다.
    private static readonly TimeSpan StateRefreshCoalescingDelay = TimeSpan.FromMilliseconds(120);

    // 일부 가상 장치는 기본 장치 변경 후 기본 장치 이벤트를 늦게 보내거나 보내지 않아, 짧게 기다린 뒤 상태를 한 번 더 맞춥니다.
    private static readonly TimeSpan DefaultDeviceRefreshDelay = TimeSpan.FromMilliseconds(250);

    // 전체 재로딩은 VoiceMeeter 환경에서 2초를 넘길 수 있어 여유를 조금 더 둡니다.
    private static readonly TimeSpan FullRefreshOperationTimeout = TimeSpan.FromSeconds(6);

    // 상태만 다시 읽는 경로는 더 가벼우므로 상대적으로 짧은 타임아웃을 둡니다.
    private static readonly TimeSpan StateRefreshOperationTimeout = TimeSpan.FromSeconds(4);

    // 설정창 장치 로딩도 COM 조회이므로 메인 UI를 막지 않게 비동기로 열고, 실패 시 에러로 돌립니다.
    private static readonly TimeSpan SettingsLoadTimeout = TimeSpan.FromSeconds(6);

    // 트레이 메뉴는 실제 표시 내용이 바뀔 때만 다시 구성합니다.
    private string? _lastTrayMenuSignature;

    // 시작 시 제한 모드로 전환됐을 때 상단에 표시할 경고 문구입니다.
    private readonly string? _startupWarningMessage;

    // 이전 실행이 정상 종료되지 않았을 때 시작 직후 안내에 사용할 정보입니다.
    private readonly PreviousRunIncident _previousRunIncident;

    // 외부에서 앱을 다시 실행했을 때 기존 인스턴스를 보여줘야 하는 요청 플래그입니다.
    private bool _externalActivationRequested;

    // 설정 창 로딩이 진행 중인지 여부입니다.
    private bool _isSettingsWindowOpening;

    // 현재 떠 있는 설정 창 인스턴스입니다.
    private SettingsWindow? _activeSettingsWindow;

    /// <summary>
    /// 메인 창을 초기화하고 데이터 바인딩을 연결합니다.
    /// </summary>
    public MainWindow(
        MainViewModel viewModel,
        Func<SettingsViewModel> settingsViewModelFactory,
        IAudioNotificationService audioNotificationService,
        ISettingsService settingsService,
        IUpdateCheckService updateCheckService,
        bool isStartupLaunch,
        bool forceVisibleOnStartup,
        string? startupWarningMessage = null,
        PreviousRunIncident previousRunIncident = default)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsViewModelFactory = settingsViewModelFactory;
        _audioNotificationService = audioNotificationService;
        _settingsService = settingsService;
        _updateCheckService = updateCheckService;
        _isStartupLaunch = isStartupLaunch;
        _forceVisibleOnStartup = forceVisibleOnStartup;
        _startupWarningMessage = startupWarningMessage;
        _previousRunIncident = previousRunIncident;
        _notifyIcon = CreateNotifyIcon();
        RefreshTrayMenu(force: true);
        DataContext = _viewModel;
        ApplyPrimaryMonitorBounds();
        _audioNotificationService.Changed += AudioNotificationService_OnChanged;
        Loaded += MainWindow_OnLoaded;
        StateChanged += MainWindow_OnStateChanged;
        Closing += MainWindow_OnClosing;
        VersionText.Text = $"버전 {GetApplicationVersionText()}";
        ApplyStartupStatus();
        UpdateEmptyState();
    }

    /// <summary>
    /// 첫 실행 시 설정 창을 강제로 여는 진입점입니다.
    /// </summary>
    public void OpenSettingsOnFirstRun()
    {
        // 첫 실행 설정창은 시작 최소화 여부와 무관하게 항상 보이도록 창을 먼저 복원합니다.
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
        _ = OpenSettingsInternalAsync();
    }

    /// <summary>
    /// 후속 실행이 기존 인스턴스를 앞으로 가져와 달라고 요청했을 때 호출됩니다.
    /// </summary>
    public void RestoreFromExternalActivation()
    {
        _externalActivationRequested = true;
        if (!IsLoaded)
        {
            return;
        }

        RestoreFromExternalActivationCore();
    }

    /// <summary>
    /// 상단 설정 버튼 클릭 이벤트입니다.
    /// </summary>
    private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = OpenSettingsInternalAsync();
    }

    /// <summary>
    /// 상단 릴리즈 버튼 클릭 시 GitHub 릴리즈 페이지를 기본 브라우저로 엽니다.
    /// </summary>
    private void OpenReleasePageButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryOpenReleasePage(_latestUpdateCheckResult?.ReleasePageUrl))
        {
            System.Windows.MessageBox.Show(
                this,
                "브라우저에서 릴리즈 페이지를 열지 못했습니다.",
                "릴리즈 페이지 열기 실패",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// 상단 업데이트 버튼 클릭 시 최신 zip 다운로드 링크를 열고 수동 덮어쓰기 절차를 안내합니다.
    /// </summary>
    private void UpdateButton_OnClick(object sender, RoutedEventArgs e)
    {
        var updateCheckResult = _latestUpdateCheckResult;
        if (updateCheckResult is null || !updateCheckResult.IsUpdateAvailable || string.IsNullOrWhiteSpace(updateCheckResult.LatestVersion))
        {
            if (!TryOpenReleasePage())
            {
                System.Windows.MessageBox.Show(
                    this,
                    "브라우저에서 릴리즈 페이지를 열지 못했습니다.",
                    "릴리즈 페이지 열기 실패",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            return;
        }

        var currentVersion = GetApplicationVersionText();
        var targetUrl = string.IsNullOrWhiteSpace(updateCheckResult.DownloadUrl)
            ? updateCheckResult.ReleasePageUrl
            : updateCheckResult.DownloadUrl;

        var result = System.Windows.MessageBox.Show(
            this,
            $"현재 버전: {currentVersion}\n새 버전: {updateCheckResult.LatestVersion}\n\n이 앱은 설치 프로그램 대신 zip 덮어쓰기 방식으로 업데이트합니다.\n1. 새 zip 다운로드\n2. 실행 중인 앱 종료\n3. 압축 해제 후 기존 실행 폴더에 덮어쓰기\n4. DesktopAudioController.exe 다시 실행\n\n지금 다운로드 페이지를 열까요?",
            "업데이트 안내",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Information);

        if (result != System.Windows.MessageBoxResult.OK)
        {
            return;
        }

        if (!TryOpenReleasePage(targetUrl))
        {
            System.Windows.MessageBox.Show(
                this,
                "브라우저에서 다운로드 페이지를 열지 못했습니다.",
                "다운로드 페이지 열기 실패",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// 상단 새로고침 버튼 클릭 시 장치 목록을 다시 읽습니다.
    /// </summary>
    private async void RefreshButton_OnClick(object sender, RoutedEventArgs e)
    {
        var app = System.Windows.Application.Current as App;
        var degradedMode = app?.IsInDegradedMode == true;
        AppLog.Info("MainWindow", $"수동 장치 새로고침 요청 degradedMode={degradedMode}");

        if (degradedMode && app?.RequiresRestartToRecoverDegradedMode == true)
        {
            AppLog.Warn("MainWindow", "수동 장치 새로고침: 제한 모드 재시작 복구로 전환");
            if (app.TryScheduleRestartRecovery("manual_refresh_button"))
            {
                return;
            }

            AppLog.Warn("MainWindow", "수동 장치 새로고침: 재시작 복구 예약 실패, 기존 복구 경로로 계속");
        }

        var executionResult = await ManualRefreshCoordinator.ExecuteAsync(
            degradedMode,
            "manual_refresh_button",
            reason => app?.TryRecoverFromDegradedModeAsync(reason) ?? Task.FromResult<App.AudioRuntimeRecoveryResult?>(null),
            recoveryResult => ApplyRecoveredAudioRuntime(recoveryResult.ViewModel, recoveryResult.NotificationService),
            ReloadViewModelAsync,
            (recoveryResult, reason) =>
            {
                if (recoveryResult.NotificationServiceStartDeferred)
                {
                    app?.EnsureRecoveredAudioNotificationServiceStarted(reason);
                }

                return Task.CompletedTask;
            });

        if (executionResult.RecoveryAttempted && !executionResult.RecoveryApplied)
        {
            AppLog.Warn("MainWindow", "수동 장치 새로고침 복구 실패: 제한 모드 유지");
        }
    }

    /// <summary>
    /// 장치 카드의 기본 장치 변경 버튼 클릭 이벤트입니다.
    /// </summary>
    private void SetDefaultButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: VisibleDeviceViewModel device })
        {
            return;
        }

        AppLog.Info("MainWindow", $"기본 장치 버튼 클릭 deviceId={device.Id} name={device.Name}");

        // 연결이 끊긴 장치는 기본 출력 장치로 승격할 수 없으므로 호출 전 바로 차단합니다.
        if (!device.IsConnected)
        {
            System.Windows.MessageBox.Show(
                this,
                "현재 연결되지 않은 장치는 기본 출력 장치로 설정할 수 없습니다.\n장치를 다시 연결한 뒤 시도하세요.",
                "장치 연결 필요",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        try
        {
            TrySetDefaultDevice(device);
        }
        catch (Exception exception)
        {
            HandleSetDefaultFailure(device, exception);
        }
    }

    /// <summary>
    /// 설정 창을 열고 저장 결과가 있으면 메인 화면을 다시 갱신합니다.
    /// </summary>
    private async Task OpenSettingsInternalAsync()
    {
        AppLog.Debug("MainWindow", "설정 창 로딩 시작");
        if (TryActivateExistingSettingsWindow())
        {
            return;
        }

        lock (_settingsWindowSyncRoot)
        {
            if (_isSettingsWindowOpening)
            {
                AppLog.Debug("MainWindow", "설정 창 열기 요청 무시: 이미 로딩 중");
                return;
            }

            _isSettingsWindowOpening = true;
        }

        SettingsWindow? settingsWindow = null;
        var semaphoreAcquired = false;
        try
        {
            _viewModel.FlushPendingProgramPreferenceSave();
            using var loadCancellationTokenSource = new CancellationTokenSource(SettingsLoadTimeout);
            await _viewRefreshSemaphore.WaitAsync(loadCancellationTokenSource.Token);
            semaphoreAcquired = true;
            var settingsViewModel = _settingsViewModelFactory();
            await settingsViewModel.LoadAsync(loadCancellationTokenSource.Token).WaitAsync(loadCancellationTokenSource.Token);
            AppLog.Debug("MainWindow", $"설정 창 로딩 완료 visibleDevices={settingsViewModel.AvailableDevices.Count}");

            settingsWindow = new SettingsWindow(settingsViewModel)
            {
                Owner = this
            };

            lock (_settingsWindowSyncRoot)
            {
                _activeSettingsWindow = settingsWindow;
            }

            _viewRefreshSemaphore.Release();
            semaphoreAcquired = false;

            var result = settingsWindow.ShowDialog();
            if (result == true)
            {
                _minimizeToTray = settingsViewModel.MinimizeToTray;
                _includePreReleaseUpdates = settingsViewModel.IncludePreReleaseUpdates;
                await ReloadViewModelAsync("settings_saved");
                await CheckForUpdateInBackgroundAsync();
            }
        }
        catch (OperationCanceledException exception)
        {
            AppLog.Error("MainWindow", "설정 창 로딩 타임아웃", new TimeoutException("The operation has timed out.", exception));
            System.Windows.MessageBox.Show(
                this,
                "설정 창을 여는 동안 장치 정보를 읽지 못해 시간이 초과됐습니다.\n잠시 후 다시 시도해 주세요.",
                "설정 창 열기 실패",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
        catch (TimeoutException exception)
        {
            AppLog.Error("MainWindow", "설정 창 로딩 타임아웃", exception);
            System.Windows.MessageBox.Show(
                this,
                "설정 창을 여는 동안 장치 정보를 읽지 못해 시간이 초과됐습니다.\n잠시 후 다시 시도해 주세요.",
                "설정 창 열기 실패",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
        catch (Exception exception)
        {
            AppLog.Error("MainWindow", "설정 창 로딩 실패", exception);
            System.Windows.MessageBox.Show(
                this,
                $"설정 창을 여는 중 오류가 발생했습니다.\n\n원인: {exception.Message}",
                "설정 창 열기 실패",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            if (semaphoreAcquired)
            {
                _viewRefreshSemaphore.Release();
            }

            lock (_settingsWindowSyncRoot)
            {
                if (ReferenceEquals(_activeSettingsWindow, settingsWindow))
                {
                    _activeSettingsWindow = null;
                }

                _isSettingsWindowOpening = false;
            }
        }
    }

    private void ApplyRecoveredAudioRuntime(MainViewModel viewModel, IAudioNotificationService audioNotificationService)
    {
        AppLog.Info("MainWindow", "제한 모드 복구 성공: 뷰모델/알림 서비스 교체");
        _audioNotificationService.Changed -= AudioNotificationService_OnChanged;
        _audioNotificationService = audioNotificationService;
        _audioNotificationService.Changed += AudioNotificationService_OnChanged;
        _viewModel = viewModel;
        DataContext = _viewModel;
        StartupStatusText.Visibility = Visibility.Collapsed;
        StartupStatusText.Text = string.Empty;
        _lastTrayMenuSignature = null;
        UpdateEmptyState();
        RefreshTrayMenu(force: true);
    }

    private bool TryActivateExistingSettingsWindow()
    {
        SettingsWindow? activeSettingsWindow;
        lock (_settingsWindowSyncRoot)
        {
            activeSettingsWindow = _activeSettingsWindow;
        }

        if (activeSettingsWindow is null)
        {
            return false;
        }

        AppLog.Debug("MainWindow", "기존 설정 창 재활성화");
        if (activeSettingsWindow.WindowState == WindowState.Minimized)
        {
            activeSettingsWindow.WindowState = WindowState.Normal;
        }

        activeSettingsWindow.Activate();
        activeSettingsWindow.Focus();
        return true;
    }

    /// <summary>
    /// 세션 표시 이름을 사용자가 직접 덮어쓸 수 있는 작은 입력 창을 엽니다.
    /// </summary>
    private async void RenameSessionButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: AudioSessionViewModel session })
        {
            return;
        }

        var renameWindow = new RenameProgramWindow(session.DisplayName, session.ExecutablePath)
        {
            Owner = this
        };

        var result = renameWindow.ShowDialog();
        if (result != true)
        {
            return;
        }

        if (!_viewModel.SetCustomSessionDisplayName(session.DeviceId, session.Id, renameWindow.CustomDisplayName))
        {
            System.Windows.MessageBox.Show(
                this,
                "현재 세션의 이름 변경 설정을 저장하지 못했습니다.\n잠시 후 다시 시도해 주세요.",
                "이름 변경 실패",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        await ReloadViewModelAsync("custom_session_name_saved");
    }

    /// <summary>
    /// 창 로드가 끝난 시점에 시작 최소화 옵션을 적용합니다.
    /// </summary>
    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyPrimaryMonitorBounds();

        // 창 동작 옵션과 업데이트 채널 정책은 메인 창이 뜬 직후 한 번만 읽어 메모리에 유지합니다.
        var settings = _settingsService.Load();
        _minimizeToTray = settings.MinimizeToTray;
        _includePreReleaseUpdates = settings.IncludePreReleaseUpdates;

        // 창 표시 이후에 백그라운드 업데이트 확인을 시작해, 오프라인 상태여도 UI가 멈추지 않게 합니다.
        _ = CheckForUpdateInBackgroundAsync();

        if (_externalActivationRequested)
        {
            RestoreFromExternalActivationCore();
            ShowPreviousRunIncidentIfNeeded();
            return;
        }

        // 수동 실행 또는 첫 실행 설정이 필요한 경우에는 시작 최소화를 적용하지 않습니다.
        if (!_isStartupLaunch || _forceVisibleOnStartup)
        {
            ShowPreviousRunIncidentIfNeeded();
            return;
        }
        if (!settings.StartMinimized)
        {
            ShowPreviousRunIncidentIfNeeded();
            return;
        }

        if (settings.MinimizeToTray)
        {
            HideToTray();
            ShowPreviousRunIncidentIfNeeded();
            return;
        }

        WindowState = WindowState.Minimized;
        ShowPreviousRunIncidentIfNeeded();
    }

    /// <summary>
    /// 창 상태가 최소화로 바뀌면 필요 시 트레이로 숨깁니다.
    /// </summary>
    private void MainWindow_OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && ShouldMinimizeToTray())
        {
            HideToTray();
        }
    }

    /// <summary>
    /// 메인 창을 주 모니터 작업 영역 기준으로 배치하고, 시작 높이도 그 높이에 맞춥니다.
    /// </summary>
    private void ApplyPrimaryMonitorBounds()
    {
        var primaryWorkArea = SystemParameters.WorkArea;
        MinHeight = primaryWorkArea.Height;
        MaxHeight = primaryWorkArea.Height;
        Left = primaryWorkArea.Right - Width;
        Top = primaryWorkArea.Top;
    }

    /// <summary>
    /// 닫기 버튼이 눌렸을 때 종료 대신 트레이 최소화로 바꿀지 결정합니다.
    /// </summary>
    private void MainWindow_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isExitRequested || !ShouldMinimizeToTray())
        {
            _viewModel.FlushPendingProgramPreferenceSave();
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    /// <summary>
    /// 선택된 장치가 없을 때 안내 문구를 보여주고, 있으면 장치 목록을 표시합니다.
    /// </summary>
    private void UpdateEmptyState()
    {
        // 선택된 표시 장치가 하나라도 있는지 여부입니다.
        var hasDevices = _viewModel.VisibleDevices.Count > 0;
        EmptyStateText.Visibility = hasDevices ? Visibility.Collapsed : Visibility.Visible;
        DevicesItemsControl.Visibility = hasDevices ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Core Audio 콜백이 들어오면 WPF UI 스레드에서 목록 새로고침을 예약합니다.
    /// </summary>
    private void AudioNotificationService_OnChanged(object? sender, AudioNotificationChangedEventArgs e)
    {
        // 오디오 콜백은 서로 다른 스레드에서 거의 동시에 들어올 수 있으므로
        // 읽기-수정-쓰기 전체를 lock으로 감싸 원자적으로 처리합니다.
        lock (_notificationRefreshSyncRoot)
        {
            if (e.Kind == AudioNotificationChangeKind.Topology)
            {
                _pendingTopologyNotificationCount++;
                _pendingNotificationKind = AudioNotificationChangeKind.Topology;
            }
            else
            {
                _pendingStateNotificationCount++;
            }

            if (_isNotificationRefreshQueued || _isNotificationRefreshProcessing)
            {
                return;
            }

            _isNotificationRefreshQueued = true;
        }

        // Core Audio 콜백은 UI 스레드가 아닐 수 있으므로 Dispatcher를 통해 화면 갱신을 예약합니다.
        Dispatcher.InvokeAsync(() =>
        {
            try
            {
                _ = ProcessQueuedNotificationRefreshAsync();
            }
            catch (Exception exception)
            {
                AppLog.Error("MainWindow", "오디오 변경 이벤트 처리 중 예외", exception);
            }
        });
    }

    /// <summary>
    /// 기본 장치 변경을 실제로 수행하고 성공 시 화면을 즉시 새 상태로 다시 읽습니다.
    /// </summary>
    private void TrySetDefaultDevice(VisibleDeviceViewModel device)
    {
        AppLog.Info("MainWindow", $"기본 장치 변경 시도 deviceId={device.Id} name={device.Name}");
        // 선택된 장치를 Windows 기본 출력 장치로 변경합니다.
        device.SetAsDefault();
        PromoteDefaultDeviceLocally(device.Id);
        AppLog.Info("MainWindow", $"기본 장치 변경 요청 완료, 토폴로지 이벤트 대기 deviceId={device.Id}");
        _ = RefreshAfterDefaultDeviceChangeAsync(device.Id);
    }

    /// <summary>
    /// 기본 장치 변경 실패 시 원인을 분류해 사용자에게 재시도/설정 열기 선택지를 제공합니다.
    /// </summary>
    private void HandleSetDefaultFailure(VisibleDeviceViewModel device, Exception exception)
    {
        // failureReason은 사용자에게 보여줄 실패 분류 메시지입니다.
        var failureReason = ClassifyDefaultDeviceFailure(exception);

        // 사용자는 다시 시도 / 설정 열기 / 취소 중 하나를 선택할 수 있습니다.
        var result = System.Windows.MessageBox.Show(
            this,
            $"기본 출력 장치를 바꾸지 못했습니다.\n\n대상 장치: {device.Name}\n원인 추정: {failureReason}\n상세 메시지: {exception.Message}\n\n예: 다시 시도\n아니오: Windows 소리 설정 열기\n취소: 이 창 닫기",
            "기본 장치 변경 실패",
            System.Windows.MessageBoxButton.YesNoCancel,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                TrySetDefaultDevice(device);
            }
            catch (Exception retryException)
            {
                System.Windows.MessageBox.Show(
                    this,
                    $"재시도에도 실패했습니다.\n\n원인 추정: {ClassifyDefaultDeviceFailure(retryException)}\n상세 메시지: {retryException.Message}\n\nWindows 소리 설정에서 직접 변경할 수 있습니다.",
                    "기본 장치 변경 재시도 실패",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }

            return;
        }

        if (result == System.Windows.MessageBoxResult.No)
        {
            if (!TryOpenWindowsSoundSettings())
            {
                System.Windows.MessageBox.Show(
                    this,
                    "Windows 소리 설정을 열지 못했습니다.",
                    "소리 설정 열기 실패",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }

    /// <summary>
    /// 예외 타입과 HRESULT를 기준으로 기본 장치 변경 실패 이유를 사람이 읽기 쉬운 문장으로 변환합니다.
    /// </summary>
    private static string ClassifyDefaultDeviceFailure(Exception exception)
    {
        if (exception is UnauthorizedAccessException)
        {
            return "권한 또는 보안 정책 때문에 장치 전환이 차단되었을 가능성이 있습니다.";
        }

        if (exception is COMException comException)
        {
            return comException.HResult switch
            {
                unchecked((int)0x80070490) => "대상 장치를 찾지 못했습니다. 장치가 제거되었거나 ID가 바뀌었을 수 있습니다.",
                unchecked((int)0x80070005) => "접근이 거부되었습니다. 관리자 정책 또는 오디오 서비스 상태를 확인하세요.",
                unchecked((int)0x88890004) => "오디오 엔드포인트 상태가 유효하지 않습니다. 장치 연결 상태를 다시 확인하세요.",
                _ => $"COM 호출이 실패했습니다. HRESULT=0x{comException.HResult:X8}"
            };
        }

        if (exception is InvalidOperationException)
        {
            return "현재 오디오 장치 상태가 변경 중이어서 요청을 처리하지 못했습니다.";
        }

        return "예상하지 못한 오류가 발생했습니다.";
    }

    /// <summary>
    /// 창이 닫힐 때 오디오 이벤트 구독을 해제합니다.
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        _audioNotificationService.Changed -= AudioNotificationService_OnChanged;
        Loaded -= MainWindow_OnLoaded;
        StateChanged -= MainWindow_OnStateChanged;
        Closing -= MainWindow_OnClosing;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        base.OnClosed(e);
    }

    /// <summary>
    /// Windows 기본 소리 설정 창을 열어 사용자가 수동으로 장치를 바꿀 수 있게 합니다.
    /// </summary>
    private static bool TryOpenWindowsSoundSettings()
    {
        try
        {
            // ms-settings URI는 Windows 설정 앱의 사운드 페이지를 직접 엽니다.
            Process.Start(new ProcessStartInfo("ms-settings:sound")
            {
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception exception)
        {
            AppLog.Warn("MainWindow", "Windows 소리 설정 열기 실패", exception);
            return false;
        }
    }

    /// <summary>
    /// GitHub 릴리즈 페이지를 기본 브라우저로 엽니다.
    /// </summary>
    private static bool TryOpenReleasePage(string? releasePageUrl = null)
    {
        try
        {
            Process.Start(new ProcessStartInfo(string.IsNullOrWhiteSpace(releasePageUrl) ? ReleasesPageUrl : releasePageUrl)
            {
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception exception)
        {
            AppLog.Warn("MainWindow", $"릴리즈 페이지 열기 실패 url={releasePageUrl ?? ReleasesPageUrl}", exception);
            return false;
        }
    }

    private static bool TryOpenLogFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo(AppLog.LogDirectoryPath)
            {
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception exception)
        {
            AppLog.Warn("MainWindow", "로그 폴더 열기 실패", exception);
            return false;
        }
    }

    private static bool TryOpenIssueCreationPage()
    {
        var currentVersion = GetApplicationVersionText();
        var title = Uri.EscapeDataString("[Bug] 비정상 종료 보고");
        const string redactedLogDirectoryHint = @"%LocalAppData%\DesktopAudioController\logs";
        var body = Uri.EscapeDataString(
            $"## 요약\n이전 실행이 정상 종료되지 않았습니다.\n\n## 환경\n- 버전: {currentVersion}\n- 로그 폴더: {redactedLogDirectoryHint}\n\n## 확인 사항\n- 앱 실행 중 강제 종료/재부팅/블루스크린/크래시 가능성\n- 최신 로그 파일을 첨부해 주세요.\n");

        return TryOpenReleasePage($"https://github.com/TailFox-Forge/DesktopAudioController/issues/new?labels=bug&title={title}&body={body}");
    }

    /// <summary>
    /// 시스템 트레이 아이콘과 컨텍스트 메뉴를 생성합니다.
    /// </summary>
    private Forms.NotifyIcon CreateNotifyIcon()
    {
        // notifyIcon은 창이 숨겨진 상태에서도 사용자가 앱을 복원하거나 종료할 수 있게 해줍니다.
        var notifyIcon = new Forms.NotifyIcon
        {
            Text = "DesktopAudioController",
            Icon = TryLoadTrayIcon() ?? SystemIcons.Application,
            Visible = true
        };

        notifyIcon.ContextMenuStrip = new Forms.ContextMenuStrip();
        notifyIcon.DoubleClick += (_, _) => RestoreFromTray();
        return notifyIcon;
    }

    private void ApplyStartupStatus()
    {
        if (string.IsNullOrWhiteSpace(_startupWarningMessage))
        {
            StartupStatusText.Visibility = Visibility.Collapsed;
            StartupStatusText.Text = string.Empty;
            return;
        }

        StartupStatusText.Text = _startupWarningMessage;
        StartupStatusText.Visibility = Visibility.Visible;
    }

    internal void ShowAutomaticStartupRecoveryNotification(TimeSpan delay)
    {
        try
        {
            var totalSeconds = Math.Max(1, (int)Math.Ceiling(delay.TotalSeconds));
            _notifyIcon.BalloonTipTitle = "DesktopAudioController";
            _notifyIcon.BalloonTipText = $"오디오 초기화가 지연되어 {totalSeconds}초 뒤 자동으로 다시 시작합니다.";
            _notifyIcon.ShowBalloonTip(5000);
            AppLog.Info("MainWindow", $"자동 재시작 안내 표시 delaySeconds={totalSeconds}");
        }
        catch (Exception exception)
        {
            AppLog.Warn("MainWindow", "자동 재시작 안내 표시 실패", exception);
        }
    }

    private void ShowPreviousRunIncidentIfNeeded()
    {
        if (!_previousRunIncident.Detected)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            var startedAtText = _previousRunIncident.StartedAtUtc == default
                ? string.Empty
                : $"\n이전 시작 시각: {_previousRunIncident.StartedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";

            var result = System.Windows.MessageBox.Show(
                this,
                $"{_previousRunIncident.Message}{startedAtText}\n\n예: 로그 폴더 열기\n아니오: GitHub 이슈 작성\n취소: 닫기",
                "비정상 종료 감지",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                if (!TryOpenLogFolder())
                {
                    System.Windows.MessageBox.Show(
                        this,
                        "로그 폴더를 열지 못했습니다.",
                        "로그 폴더 열기 실패",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                return;
            }

            if (result == MessageBoxResult.No && !TryOpenIssueCreationPage())
            {
                System.Windows.MessageBox.Show(
                    this,
                    "브라우저에서 GitHub 이슈 페이지를 열지 못했습니다.",
                    "이슈 페이지 열기 실패",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }));
    }

    private static Icon? TryLoadTrayIcon()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return null;
            }

            using var extractedIcon = System.Drawing.Icon.ExtractAssociatedIcon(executablePath);
            return extractedIcon?.Clone() as Icon;
        }
        catch (Exception exception)
        {
            AppLog.Warn("MainWindow", "트레이 아이콘 로드 실패", exception);
            return null;
        }
    }

    /// <summary>
    /// 메인 창을 작업 표시줄에서 숨기고 트레이만 남깁니다.
    /// </summary>
    private void HideToTray()
    {
        ShowInTaskbar = false;
        Hide();
    }

    /// <summary>
    /// 트레이에 숨겨둔 창을 다시 화면과 작업 표시줄로 복원합니다.
    /// </summary>
    private void RestoreFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void RestoreFromExternalActivationCore()
    {
        _externalActivationRequested = false;
        AppLog.Info("MainWindow", "외부 실행 요청으로 기존 창 복원 및 초기 위치 원복");
        ApplyPrimaryMonitorBounds();
        RestoreFromTray();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    /// <summary>
    /// 트레이 메뉴의 종료 명령으로 앱을 정상 종료합니다.
    /// </summary>
    private void ExitApplication()
    {
        RunOnUiThread(() =>
        {
            if (_isExitRequested)
            {
                return;
            }

            AppLog.Info("MainWindow", "트레이 종료 요청 처리");
            _isExitRequested = true;
            _viewModel.FlushPendingProgramPreferenceSave();
            _notifyIcon.Visible = false;
            Close();
        });
    }

    /// <summary>
    /// 현재 설정상 최소화/닫기 동작을 트레이로 보내야 하는지 계산합니다.
    /// </summary>
    private bool ShouldMinimizeToTray()
    {
        return _minimizeToTray;
    }

    /// <summary>
    /// 현재 보이는 장치 기준으로 트레이 메뉴를 다시 구성합니다.
    /// </summary>
    private void RefreshTrayMenu(bool force = false)
    {
        if (_notifyIcon.ContextMenuStrip is null)
        {
            return;
        }

        var trayMenuSignature = BuildTrayMenuSignature();
        if (!force && string.Equals(_lastTrayMenuSignature, trayMenuSignature, StringComparison.Ordinal))
        {
            return;
        }

        _lastTrayMenuSignature = trayMenuSignature;

        // trayMenu는 현재 상태 기준으로 완전히 다시 만드는 트레이 메뉴입니다.
        var trayMenu = _notifyIcon.ContextMenuStrip;
        trayMenu.Items.Clear();

        var defaultDevice = _viewModel.VisibleDevices.FirstOrDefault(device => device.IsDefault);
        _notifyIcon.Text = defaultDevice is null
            ? "DesktopAudioController"
            : $"DesktopAudioController - 기본: {TrimNotifyText(defaultDevice.Name)}";

        var statusItem = new Forms.ToolStripMenuItem(
            defaultDevice is null
                ? "현재 기본 장치: 없음"
                : $"현재 기본 장치: {defaultDevice.Name}")
        {
            Enabled = false
        };

        trayMenu.Items.Add(statusItem);
        if (_minimizeToTray)
        {
            trayMenu.Items.Add(new Forms.ToolStripMenuItem("닫기 버튼 -> 트레이 최소화")
            {
                Enabled = false
            });
        }

        trayMenu.Items.Add("창 열기", null, (_, _) => RunOnUiThread(RestoreFromTray));
        trayMenu.Items.Add("설정 열기", null, (_, _) => RunOnUiThread(() =>
        {
            RestoreFromTray();
            _ = OpenSettingsInternalAsync();
        }));
        trayMenu.Items.Add("장치 다시 읽기", null, (_, _) => RunOnUiThread(() =>
        {
            _ = RefreshStateViewAsync("tray_refresh");
        }));

        if (_viewModel.VisibleDevices.Count > 0)
        {
            trayMenu.Items.Add(new Forms.ToolStripSeparator());
        }

        var deviceMenu = new Forms.ToolStripMenuItem("기본 출력 바꾸기");
        var muteMenu = new Forms.ToolStripMenuItem("장치 음소거");
        foreach (var device in _viewModel.VisibleDevices)
        {
            // localDevice는 foreach 캡처 안전성을 위한 지역 참조입니다.
            var localDevice = device;
            var menuLabel = BuildTrayDeviceMenuLabel(localDevice.Name, localDevice.IsConnected);
            var defaultItem = new Forms.ToolStripMenuItem(menuLabel)
            {
                Checked = localDevice.IsDefault,
                Enabled = localDevice.IsConnected
            };

            defaultItem.Click += (_, _) => RunOnUiThread(() =>
            {
                try
                {
                    TrySetDefaultDevice(localDevice);
                }
                catch (Exception exception)
                {
                    HandleSetDefaultFailure(localDevice, exception);
                }
            });

            var muteItem = new Forms.ToolStripMenuItem(menuLabel)
            {
                Checked = localDevice.IsMuted,
                Enabled = localDevice.IsConnected
            };

            muteItem.Click += (_, _) => RunOnUiThread(() =>
            {
                try
                {
                    localDevice.IsMuted = !localDevice.IsMuted;
                    RefreshTrayMenu(force: true);
                }
                catch
                {
                    // 트레이 토글 실패는 다음 새로고침에서 복구합니다.
                }
            });

            deviceMenu.DropDownItems.Add(defaultItem);
            muteMenu.DropDownItems.Add(muteItem);
        }

        trayMenu.Items.Add(deviceMenu);
        trayMenu.Items.Add(muteMenu);
        trayMenu.Items.Add(new Forms.ToolStripSeparator());
        trayMenu.Items.Add("앱 종료", null, (_, _) => ExitApplication());
    }

    /// <summary>
    /// NotifyIcon 텍스트 길이 제한에 맞춰 장치 이름을 적당히 줄입니다.
    /// </summary>
    private static string TrimNotifyText(string text)
    {
        return text.Length <= 32 ? text : $"{text[..29]}...";
    }

    private static string BuildTrayDeviceMenuLabel(string deviceName, bool isConnected)
    {
        return isConnected ? deviceName : $"{deviceName} (연결 안 됨)";
    }

    /// <summary>
    /// 트레이 메뉴처럼 UI 스레드가 아닐 수 있는 호출 지점을 WPF Dispatcher로 정규화합니다.
    /// </summary>
    private void RunOnUiThread(Action action)
    {
        if (Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _ = Dispatcher.InvokeAsync(action);
    }

    /// <summary>
    /// GitHub 릴리즈 기준 새 버전이 있는지 백그라운드에서 확인하고, 있을 때만 안내 문구를 표시합니다.
    /// </summary>
    private async Task CheckForUpdateInBackgroundAsync()
    {
        // currentVersion은 현재 실행 중인 앱 버전 문자열입니다.
        var currentVersion = GetApplicationVersionText();

        await Dispatcher.InvokeAsync(() =>
        {
            UpdateStatusText.Visibility = Visibility.Collapsed;
            UpdateStatusText.Text = string.Empty;
            UpdateButton.Visibility = Visibility.Collapsed;
        });

        UpdateCheckResult updateCheckResult;
        try
        {
            // updateCheckResult는 새 버전 존재 여부와 최신 버전 문자열을 담은 결과입니다.
            updateCheckResult = await _updateCheckService.CheckForUpdateAsync(currentVersion, _includePreReleaseUpdates);
            _latestUpdateCheckResult = updateCheckResult;
        }
        catch (Exception exception)
        {
            AppLog.Warn("MainWindow", "업데이트 확인 실패", exception);
            updateCheckResult = new UpdateCheckResult
            {
                HadError = true,
                StatusMessage = "업데이트 확인에 실패했습니다."
            };
        }

        await Dispatcher.InvokeAsync(() =>
        {
            if (updateCheckResult.HadError)
            {
                UpdateStatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xB3, 0x5A, 0x00));
                UpdateStatusText.Text = updateCheckResult.StatusMessage ?? "업데이트 확인에 실패했습니다.";
                UpdateStatusText.Visibility = Visibility.Visible;
                return;
            }

            if (!updateCheckResult.IsUpdateAvailable || string.IsNullOrWhiteSpace(updateCheckResult.LatestVersion))
            {
                UpdateStatusText.Visibility = Visibility.Collapsed;
                UpdateStatusText.Text = string.Empty;
                UpdateButton.Visibility = Visibility.Collapsed;
                return;
            }

            UpdateStatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xAA, 0x66));
            var publishedDateText = updateCheckResult.PublishedAtUtc.HasValue
                ? $" · {updateCheckResult.PublishedAtUtc.Value.ToLocalTime():yyyy-MM-dd} 공개"
                : string.Empty;
            var preReleaseText = updateCheckResult.IsPreRelease ? " (프리릴리즈)" : string.Empty;
            UpdateStatusText.Text = $"새 버전 {updateCheckResult.LatestVersion}{preReleaseText} 다운로드 가능{publishedDateText}";
            UpdateStatusText.Visibility = Visibility.Visible;
            UpdateButton.Visibility = Visibility.Visible;
        });
    }

    /// <summary>
    /// 현재 실행 중인 어셈블리에서 사용자에게 보여줄 버전 문자열을 계산합니다.
    /// </summary>
    private static string GetApplicationVersionText()
    {
        // assembly는 현재 실행 중인 WPF 앱의 진입 어셈블리입니다.
        var assembly = Assembly.GetExecutingAssembly();

        // informationalVersion은 prerelease 접미사를 포함한 사람이 읽기 좋은 버전 문자열입니다.
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            // SourceLink 등이 붙여준 build metadata는 화면에 불필요하므로 '+' 뒤는 잘라냅니다.
            var metadataSeparatorIndex = informationalVersion.IndexOf('+');
            return metadataSeparatorIndex >= 0
            ? informationalVersion[..metadataSeparatorIndex]
                : informationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "알 수 없음";
    }

    /// <summary>
    /// 큐에 모인 오디오 변경 이벤트를 배치 단위로 처리합니다.
    /// 새 이벤트가 이어지는 동안만 루프를 반복하고, 큐가 비면 즉시 빠져나옵니다.
    /// </summary>
    private async Task ProcessQueuedNotificationRefreshAsync()
    {
        while (true)
        {
            AudioNotificationChangeKind firstObservedKind;
            lock (_notificationRefreshSyncRoot)
            {
                firstObservedKind = _pendingNotificationKind;
                _isNotificationRefreshQueued = false;
                _isNotificationRefreshProcessing = true;
            }

            // 상태 변경은 매우 자주 들어오므로 잠시 모은 뒤 마지막 상태만 반영합니다.
            if (firstObservedKind == AudioNotificationChangeKind.State)
            {
                await Task.Delay(StateRefreshCoalescingDelay);
            }

            NotificationRefreshBatch batch;
            lock (_notificationRefreshSyncRoot)
            {
                batch = DrainPendingNotificationBatchLocked();
            }

            LogNotificationBatch(batch);

            if (batch.Kind == AudioNotificationChangeKind.Topology)
            {
                await ReloadViewModelAsync("topology_event");
            }
            else
            {
                await RefreshStateViewAsync();
            }

            lock (_notificationRefreshSyncRoot)
            {
                if (!_isNotificationRefreshQueued)
                {
                    _isNotificationRefreshProcessing = false;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 기본 장치 변경 후 이벤트가 오지 않는 장치를 위해 짧은 지연 뒤 한 번 더 상태를 동기화합니다.
    /// </summary>
    private async Task RefreshAfterDefaultDeviceChangeAsync(string deviceId)
    {
        await Task.Delay(DefaultDeviceRefreshDelay);
        AppLog.Info("MainWindow", $"기본 장치 변경 후 지연 재동기화 deviceId={deviceId}");
        await RefreshStateViewAsync($"default_device_resync deviceId={deviceId}");
    }

    /// <summary>
    /// 전체 장치/세션 스냅샷을 백그라운드에서 다시 읽고 UI에 반영합니다.
    /// </summary>
    private async Task ReloadViewModelAsync(string reason)
    {
        await _viewRefreshSemaphore.WaitAsync();
        try
        {
            if (!string.Equals(reason, "topology_event", StringComparison.Ordinal))
            {
                AppLog.Debug("MainWindow", $"전체 새로고침 시작 reason={reason}");
            }

            await _viewModel.LoadAsync().WaitAsync(FullRefreshOperationTimeout);
            UpdateEmptyState();
            RefreshTrayMenu();

            if (!string.Equals(reason, "topology_event", StringComparison.Ordinal))
            {
                AppLog.Debug("MainWindow", $"전체 새로고침 완료 reason={reason}");
            }
        }
        catch (TimeoutException exception)
        {
            _viewModel.InvalidatePendingSnapshots();
            AppLog.Error("MainWindow", $"전체 새로고침 타임아웃 reason={reason}", exception);
        }
        catch (Exception exception)
        {
            AppLog.Error("MainWindow", $"전체 새로고침 실패 reason={reason}", exception);
        }
        finally
        {
            _viewRefreshSemaphore.Release();
        }
    }

    /// <summary>
    /// 현재 보이는 장치 구조는 유지하고 상태 값만 백그라운드 조회 후 반영합니다.
    /// </summary>
    private async Task RefreshStateViewAsync(string reason = "state_refresh")
    {
        await _viewRefreshSemaphore.WaitAsync();
        try
        {
            if (!string.Equals(reason, "state_refresh", StringComparison.Ordinal))
            {
                AppLog.Debug("MainWindow", $"상태 부분 새로고침 시작 reason={reason}");
            }

            await _viewModel.RefreshStateOnlyAsync().WaitAsync(StateRefreshOperationTimeout);
            RefreshTrayMenu();

            if (!string.Equals(reason, "state_refresh", StringComparison.Ordinal))
            {
                AppLog.Debug("MainWindow", $"상태 부분 새로고침 완료 reason={reason}");
            }
        }
        catch (TimeoutException exception)
        {
            _viewModel.InvalidatePendingSnapshots();
            AppLog.Error("MainWindow", $"상태 부분 새로고침 타임아웃 reason={reason}", exception);
        }
        catch (Exception exception)
        {
            AppLog.Error("MainWindow", $"상태 부분 새로고침 실패 reason={reason}", exception);
        }
        finally
        {
            _viewRefreshSemaphore.Release();
        }
    }

    /// <summary>
    /// 기본 장치 변경 직후 다음 새로고침이 늦더라도 버튼 상태가 반대로 굳지 않도록 UI만 먼저 맞춥니다.
    /// </summary>
    private void PromoteDefaultDeviceLocally(string selectedDeviceId)
    {
        foreach (var device in _viewModel.VisibleDevices)
        {
            device.UpdateSnapshot(
                device.Name,
                device.Id == selectedDeviceId,
                device.IsConnected,
                device.Volume,
                device.IsMuted);
        }

        RefreshTrayMenu(force: true);
    }

    private string BuildTrayMenuSignature()
    {
        return string.Join(
            "\n",
            _viewModel.VisibleDevices.Select(device =>
                $"{device.Id}|{device.Name}|{device.IsDefault}|{device.IsConnected}|{device.IsMuted}"));
    }

    private NotificationRefreshBatch DrainPendingNotificationBatchLocked()
    {
        var batch = new NotificationRefreshBatch(
            _pendingNotificationKind,
            _pendingStateNotificationCount,
            _pendingTopologyNotificationCount);

        _pendingNotificationKind = AudioNotificationChangeKind.State;
        _pendingStateNotificationCount = 0;
        _pendingTopologyNotificationCount = 0;
        return batch;
    }

    private static void LogNotificationBatch(NotificationRefreshBatch batch)
    {
        if (batch.Kind == AudioNotificationChangeKind.Topology)
        {
            AppLog.Debug(
                "MainWindow",
                $"오디오 변경 병합 처리 kind=Topology stateEvents={batch.StateEventCount} topologyEvents={batch.TopologyEventCount}");
            return;
        }

        if (batch.TotalEventCount <= 1)
        {
            return;
        }

        AppLog.Debug(
            "MainWindow",
            $"오디오 변경 병합 처리 kind=State stateEvents={batch.StateEventCount} topologyEvents={batch.TopologyEventCount}");
    }
}
