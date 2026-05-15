using System.Diagnostics;
using System.Windows;
using DesktopAudioController.Services;
using DesktopAudioController.ViewModels;

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

    // 같은 틱에서 중복 새로고침 요청이 들어올 때 한 번으로 합치기 위한 플래그입니다.
    private bool _isNotificationRefreshQueued;

    /// <summary>
    /// 메인 창을 초기화하고 데이터 바인딩을 연결합니다.
    /// </summary>
    public MainWindow(
        MainViewModel viewModel,
        Func<SettingsViewModel> settingsViewModelFactory,
        IAudioNotificationService audioNotificationService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsViewModelFactory = settingsViewModelFactory;
        _audioNotificationService = audioNotificationService;
        DataContext = _viewModel;
        _audioNotificationService.Changed += AudioNotificationService_OnChanged;
        UpdateEmptyState();
    }

    /// <summary>
    /// 첫 실행 시 설정 창을 강제로 여는 진입점입니다.
    /// </summary>
    public void OpenSettingsOnFirstRun()
    {
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

        try
        {
            // 선택된 장치를 Windows 기본 출력 장치로 변경합니다.
            device.SetAsDefault();
            _viewModel.Load();
            UpdateEmptyState();
        }
        catch (Exception exception)
        {
            // 예외가 발생하면 앱을 종료하지 않고 사용자에게 원인을 알려줍니다.
            MessageBox.Show(
                this,
                $"기본 출력 장치를 변경하지 못했습니다.\n\n{exception.Message}\n\nWindows 소리 설정 화면을 열어 수동으로 변경할 수 있습니다.",
                "기본 장치 변경 실패",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            TryOpenWindowsSoundSettings();
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
        }
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
    private void AudioNotificationService_OnChanged(object? sender, EventArgs e)
    {
        if (_isNotificationRefreshQueued)
        {
            return;
        }

        _isNotificationRefreshQueued = true;

        // Core Audio 콜백은 UI 스레드가 아닐 수 있으므로 Dispatcher를 통해 화면 갱신을 예약합니다.
        Dispatcher.InvokeAsync(() =>
        {
            _isNotificationRefreshQueued = false;
            _viewModel.Load();
            UpdateEmptyState();
        });
    }

    /// <summary>
    /// 창이 닫힐 때 오디오 이벤트 구독을 해제합니다.
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        _audioNotificationService.Changed -= AudioNotificationService_OnChanged;
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
}
