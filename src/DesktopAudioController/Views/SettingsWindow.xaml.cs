using System.Windows;
using DesktopAudioController.Services;
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
        try
        {
            _viewModel.Save();
            DialogResult = true;
            Close();
        }
        catch (SettingsPersistenceException exception)
        {
            System.Windows.MessageBox.Show(
                this,
                $"설정을 저장하지 못했습니다.\n\n저장 경로: {exception.SettingsFilePath}\n원인: {exception.InnerException?.Message ?? exception.Message}",
                "설정 저장 실패",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (StartupRegistrationException exception)
        {
            System.Windows.MessageBox.Show(
                this,
                $"Windows 자동 실행 옵션을 적용하지 못했습니다.\n\n레지스트리 경로: {exception.RegistryPath}\n값 이름: {exception.ValueName}\n원인: {exception.InnerException?.Message ?? exception.Message}",
                "자동 실행 설정 실패",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                this,
                $"설정을 저장하는 중 예상하지 못한 오류가 발생했습니다.\n\n원인: {exception.Message}",
                "설정 저장 실패",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
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
