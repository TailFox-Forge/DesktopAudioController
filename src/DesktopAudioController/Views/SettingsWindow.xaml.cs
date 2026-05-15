using System.Windows;
using DesktopAudioController.ViewModels;

namespace DesktopAudioController.Views;

/// <summary>
/// 표시할 장치와 표시 옵션을 선택하는 설정 창입니다.
/// </summary>
public partial class SettingsWindow : Window
{
    // 설정 창에 바인딩되는 뷰모델입니다.
    private readonly SettingsViewModel _viewModel;

    /// <summary>
    /// 설정 창을 초기화하고 뷰모델을 연결합니다.
    /// </summary>
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    /// <summary>
    /// 저장 버튼 클릭 시 설정을 저장하고 대화상자를 성공 상태로 닫습니다.
    /// </summary>
    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Save();
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// 취소 버튼 클릭 시 변경 내용을 버리고 창을 닫습니다.
    /// </summary>
    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
