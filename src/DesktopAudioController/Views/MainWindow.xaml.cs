using System.Windows;
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

    /// <summary>
    /// 메인 창을 초기화하고 데이터 바인딩을 연결합니다.
    /// </summary>
    public MainWindow(
        MainViewModel viewModel,
        Func<SettingsViewModel> settingsViewModelFactory)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsViewModelFactory = settingsViewModelFactory;
        DataContext = _viewModel;
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

        device.SetAsDefault();
        _viewModel.Load();
        UpdateEmptyState();
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
}
