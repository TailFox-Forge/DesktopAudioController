using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using DesktopAudioController.Services;
using DesktopAudioController.ViewModels;
using System.Threading.Tasks;
using Forms = System.Windows.Forms;

namespace DesktopAudioController.Views;

/// <summary>
/// 장치 마스터 볼륨 카드 목록을 보여주는 메인 창입니다.
/// </summary>
public partial class MainWindow : Window
{
    // 메인 화면 데이터와 바인딩되는 뷰모델입니다.
    private readonly MainViewModel _viewModel;

    // 설정 창을 열 때마다 새 설정 뷰모델을 만들기 위한 팩토리입니다.
    private readonly Func<SettingsViewModel> _settingsViewModelFactory;

    // Core Audio 이벤트를 받아 메인 화면 갱신을 트리거하는 서비스입니다.
    private readonly IAudioNotificationService _audioNotificationService;

    // 시작 최소화, 트레이 최소화 같은 창 동작 옵션을 읽는 설정 서비스입니다.
    private readonly ISettingsService _settingsService;

    // GitHub 릴리즈 기준 새 버전 존재 여부를 확인하는 서비스입니다.
    private readonly IUpdateCheckService _updateCheckService;

    // 현재 실행이 Windows 자동 실행으로 시작된 경우에만 true입니다.
    private readonly bool _isStartupLaunch;

    // 첫 실행처럼 장치 설정이 필요한 경우 창을 강제로 보이게 유지하기 위한 플래그입니다.
    private readonly bool _forceVisibleOnStartup;

    // 시스템 트레이 영역에 표시할 아이콘 인스턴스입니다.
    private readonly Forms.NotifyIcon _notifyIcon;

    // 오디오 콜백 스레드와 UI 스레드가 함께 접근하는 새로고침 큐 상태를 직렬화하는 잠금 객체입니다.
    private readonly object _notificationRefreshSyncRoot = new();

    // 같은 틱에서 중복 새로고침 요청이 들어올 때 한 번으로 합치기 위한 플래그입니다.
    private bool _isNotificationRefreshQueued;

    // 같은 틱 안에서 여러 이벤트가 섞이면 더 큰 범위의 갱신 종류를 보존하기 위한 필드입니다.
    private AudioNotificationChangeKind _pendingNotificationKind = AudioNotificationChangeKind.State;

    // 사용자 의도로 종료하는 중인지 여부입니다. false면 닫기를 트레이 최소화로 전환합니다.
    private bool _isExitRequested;

    // 사용자를 GitHub 릴리즈 목록으로 보내는 고정 URL입니다.
    private static readonly string ReleasesPageUrl = "https://github.com/TailFox-Forge/DesktopAudioController/releases";

    // 상태 이벤트 폭주 시 즉시 재열거하지 않도록 짧게 모아두는 지연 시간입니다.
    private static readonly TimeSpan StateRefreshCoalescingDelay = TimeSpan.FromMilliseconds(120);

    // 일부 가상 장치는 기본 장치 변경 후 기본 장치 이벤트를 늦게 보내거나 보내지 않아, 짧게 기다린 뒤 상태를 한 번 더 맞춥니다.
    private static readonly TimeSpan DefaultDeviceRefreshDelay = TimeSpan.FromMilliseconds(250);

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
        bool forceVisibleOnStartup)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsViewModelFactory = settingsViewModelFactory;
        _audioNotificationService = audioNotificationService;
        _settingsService = settingsService;
        _updateCheckService = updateCheckService;
        _isStartupLaunch = isStartupLaunch;
        _forceVisibleOnStartup = forceVisibleOnStartup;
        _notifyIcon = CreateNotifyIcon();
        RefreshTrayMenu();
        DataContext = _viewModel;
        _audioNotificationService.Changed += AudioNotificationService_OnChanged;
        Loaded += MainWindow_OnLoaded;
        StateChanged += MainWindow_OnStateChanged;
        Closing += MainWindow_OnClosing;
        VersionText.Text = $"버전 {GetApplicationVersionText()}";
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
        OpenSettingsInternal();
    }

    /// <summary>
    /// 상단 설정 버튼 클릭 이벤트입니다.
    /// </summary>
    private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        OpenSettingsInternal();
    }

    /// <summary>
    /// 상단 릴리즈 버튼 클릭 시 GitHub 릴리즈 페이지를 기본 브라우저로 엽니다.
    /// </summary>
    private void OpenReleasePageButton_OnClick(object sender, RoutedEventArgs e)
    {
        TryOpenReleasePage();
    }

    /// <summary>
    /// 상단 새로고침 버튼 클릭 시 장치 목록을 다시 읽습니다.
    /// </summary>
    private void RefreshButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Load();
        UpdateEmptyState();
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
    private void OpenSettingsInternal()
    {
        // 설정 창에서 사용할 새 뷰모델 인스턴스입니다.
        var settingsViewModel = _settingsViewModelFactory();
        settingsViewModel.Load();

        // 설정 창은 메인 창을 소유자로 두어 모달 형태로 엽니다.
        var settingsWindow = new SettingsWindow(settingsViewModel)
        {
            Owner = this
        };

        // 저장 성공 시에만 메인 화면을 다시 읽습니다.
        var result = settingsWindow.ShowDialog();
        if (result == true)
        {
            _viewModel.Load();
            UpdateEmptyState();
            RefreshTrayMenu();
        }
    }

    /// <summary>
    /// 창 로드가 끝난 시점에 시작 최소화 옵션을 적용합니다.
    /// </summary>
    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        // 창 표시 이후에 백그라운드 업데이트 확인을 시작해, 오프라인 상태여도 UI가 멈추지 않게 합니다.
        _ = CheckForUpdateInBackgroundAsync();

        // 수동 실행 또는 첫 실행 설정이 필요한 경우에는 시작 최소화를 적용하지 않습니다.
        if (!_isStartupLaunch || _forceVisibleOnStartup)
        {
            return;
        }

        // settings는 사용자가 마지막으로 저장한 창 동작 옵션입니다.
        var settings = _settingsService.Load();
        if (!settings.StartMinimized)
        {
            return;
        }

        if (settings.MinimizeToTray)
        {
            HideToTray();
            return;
        }

        WindowState = WindowState.Minimized;
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
    /// 닫기 버튼이 눌렸을 때 종료 대신 트레이 최소화로 바꿀지 결정합니다.
    /// </summary>
    private void MainWindow_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isExitRequested || !ShouldMinimizeToTray())
        {
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
        AppLog.Debug("MainWindow", $"오디오 변경 이벤트 수신 kind={e.Kind}");
        // shouldScheduleRefresh는 이번 콜백이 Dispatcher 작업을 새로 예약해야 하는지 여부입니다.
        var shouldScheduleRefresh = false;

        // 오디오 콜백은 서로 다른 스레드에서 거의 동시에 들어올 수 있으므로
        // 읽기-수정-쓰기 전체를 lock으로 감싸 원자적으로 처리합니다.
        lock (_notificationRefreshSyncRoot)
        {
            if (_isNotificationRefreshQueued)
            {
                // 이미 예약된 작업이 있으면 더 큰 범위의 Topology 변경만 승격 저장합니다.
                if (e.Kind == AudioNotificationChangeKind.Topology)
                {
                    _pendingNotificationKind = AudioNotificationChangeKind.Topology;
                }

                return;
            }

            _isNotificationRefreshQueued = true;
            _pendingNotificationKind = e.Kind;
            shouldScheduleRefresh = true;
        }

        if (!shouldScheduleRefresh)
        {
            return;
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
            $"기본 출력 장치를 변경하지 못했습니다.\n\n원인 추정: {failureReason}\n대상 장치: {device.Name}\n상세 메시지: {exception.Message}\n\n예: 한 번 더 시도\n아니오: Windows 소리 설정 열기\n취소: 닫기",
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
                    $"재시도에도 실패했습니다.\n\n원인 추정: {ClassifyDefaultDeviceFailure(retryException)}\n상세 메시지: {retryException.Message}",
                    "기본 장치 변경 재시도 실패",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }

            return;
        }

        if (result == System.Windows.MessageBoxResult.No)
        {
            TryOpenWindowsSoundSettings();
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
    private static void TryOpenWindowsSoundSettings()
    {
        try
        {
            // ms-settings URI는 Windows 설정 앱의 사운드 페이지를 직접 엽니다.
            Process.Start(new ProcessStartInfo("ms-settings:sound")
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // 설정 앱 실행까지 실패하면 추가 예외를 만들지 않고 조용히 종료합니다.
        }
    }

    /// <summary>
    /// GitHub 릴리즈 페이지를 기본 브라우저로 엽니다.
    /// </summary>
    private static void TryOpenReleasePage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(ReleasesPageUrl)
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // 브라우저 실행 실패는 치명 오류가 아니므로 조용히 무시합니다.
        }
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
            Icon = SystemIcons.Application,
            Visible = true
        };

        notifyIcon.ContextMenuStrip = new Forms.ContextMenuStrip();
        notifyIcon.DoubleClick += (_, _) => RestoreFromTray();
        return notifyIcon;
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

    /// <summary>
    /// 트레이 메뉴의 종료 명령으로 앱을 정상 종료합니다.
    /// </summary>
    private void ExitApplication()
    {
        RunOnUiThread(() =>
        {
            AppLog.Info("MainWindow", "트레이 종료 요청 처리");
            _isExitRequested = true;
            _notifyIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        });
    }

    /// <summary>
    /// 현재 설정상 최소화/닫기 동작을 트레이로 보내야 하는지 계산합니다.
    /// </summary>
    private bool ShouldMinimizeToTray()
    {
        // settings는 현재 파일에 저장된 최신 사용자 옵션입니다.
        var settings = _settingsService.Load();
        return settings.MinimizeToTray;
    }

    /// <summary>
    /// 현재 보이는 장치 기준으로 트레이 메뉴를 다시 구성합니다.
    /// </summary>
    private void RefreshTrayMenu()
    {
        if (_notifyIcon.ContextMenuStrip is null)
        {
            return;
        }

        // trayMenu는 현재 상태 기준으로 완전히 다시 만드는 트레이 메뉴입니다.
        var trayMenu = _notifyIcon.ContextMenuStrip;
        trayMenu.Items.Clear();

        var defaultDevice = _viewModel.VisibleDevices.FirstOrDefault(device => device.IsDefault);
        _notifyIcon.Text = defaultDevice is null
            ? "DesktopAudioController"
            : $"DesktopAudioController - 기본: {TrimNotifyText(defaultDevice.Name)}";

        var statusItem = new Forms.ToolStripMenuItem(
            defaultDevice is null
                ? "기본 장치: 없음"
                : $"기본 장치: {defaultDevice.Name}")
        {
            Enabled = false
        };

        trayMenu.Items.Add(statusItem);
        trayMenu.Items.Add("열기", null, (_, _) => RunOnUiThread(RestoreFromTray));
        trayMenu.Items.Add("설정", null, (_, _) => RunOnUiThread(() =>
        {
            RestoreFromTray();
            OpenSettingsInternal();
        }));
        trayMenu.Items.Add("새로고침", null, (_, _) => RunOnUiThread(() =>
        {
            _viewModel.Load();
            UpdateEmptyState();
            RefreshTrayMenu();
        }));

        if (_viewModel.VisibleDevices.Count > 0)
        {
            trayMenu.Items.Add(new Forms.ToolStripSeparator());
        }

        var deviceMenu = new Forms.ToolStripMenuItem("기본 장치 빠른 전환");
        var muteMenu = new Forms.ToolStripMenuItem("장치 음소거 토글");
        foreach (var device in _viewModel.VisibleDevices)
        {
            // localDevice는 foreach 캡처 안전성을 위한 지역 참조입니다.
            var localDevice = device;
            var defaultItem = new Forms.ToolStripMenuItem(localDevice.Name)
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

            var muteItem = new Forms.ToolStripMenuItem(localDevice.Name)
            {
                Checked = localDevice.IsMuted,
                Enabled = localDevice.IsConnected
            };

            muteItem.Click += (_, _) => RunOnUiThread(() =>
            {
                try
                {
                    localDevice.IsMuted = !localDevice.IsMuted;
                    RefreshTrayMenu();
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
        trayMenu.Items.Add("종료", null, (_, _) => ExitApplication());
    }

    /// <summary>
    /// NotifyIcon 텍스트 길이 제한에 맞춰 장치 이름을 적당히 줄입니다.
    /// </summary>
    private static string TrimNotifyText(string text)
    {
        return text.Length <= 32 ? text : $"{text[..29]}...";
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

        // updateCheckResult는 새 버전 존재 여부와 최신 버전 문자열을 담은 결과입니다.
        var updateCheckResult = await _updateCheckService.CheckForUpdateAsync(currentVersion);
        if (!updateCheckResult.IsUpdateAvailable || string.IsNullOrWhiteSpace(updateCheckResult.LatestVersion))
        {
            return;
        }

        await Dispatcher.InvokeAsync(() =>
        {
            UpdateStatusText.Text = $"새 버전 {updateCheckResult.LatestVersion} 사용 가능";
            UpdateStatusText.Visibility = Visibility.Visible;
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
    /// 큐에 모인 오디오 변경 이벤트를 한 번만 처리합니다.
    /// </summary>
    private async Task ProcessQueuedNotificationRefreshAsync()
    {
        AudioNotificationChangeKind firstObservedKind;
        lock (_notificationRefreshSyncRoot)
        {
            firstObservedKind = _pendingNotificationKind;
        }

        // 상태 변경은 매우 자주 들어오므로 잠시 모은 뒤 마지막 상태만 반영합니다.
        if (firstObservedKind == AudioNotificationChangeKind.State)
        {
            await Task.Delay(StateRefreshCoalescingDelay);
        }

        AudioNotificationChangeKind pendingKind;
        lock (_notificationRefreshSyncRoot)
        {
            pendingKind = _pendingNotificationKind;
            _pendingNotificationKind = AudioNotificationChangeKind.State;
            _isNotificationRefreshQueued = false;
        }

        if (pendingKind == AudioNotificationChangeKind.Topology)
        {
            AppLog.Debug("MainWindow", "토폴로지 전체 새로고침 수행");
            _viewModel.Load();
            UpdateEmptyState();
            RefreshTrayMenu();
            return;
        }

        AppLog.Debug("MainWindow", "상태 부분 새로고침 수행");
        _viewModel.RefreshStateOnly();
        RefreshTrayMenu();
    }

    /// <summary>
    /// 기본 장치 변경 후 이벤트가 오지 않는 장치를 위해 짧은 지연 뒤 한 번 더 상태를 동기화합니다.
    /// </summary>
    private async Task RefreshAfterDefaultDeviceChangeAsync(string deviceId)
    {
        await Task.Delay(DefaultDeviceRefreshDelay);

        await Dispatcher.InvokeAsync(() =>
        {
            AppLog.Info("MainWindow", $"기본 장치 변경 후 지연 재동기화 deviceId={deviceId}");
            _viewModel.Load();
            UpdateEmptyState();
            RefreshTrayMenu();
        });
    }
}
