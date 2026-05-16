using System.Diagnostics;
using System.IO;
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
        AppLog.Info("SettingsWindow", "설정 저장 시도");
        try
        {
            _viewModel.Save();
            AppLog.Info("SettingsWindow", "설정 저장 성공");
            DialogResult = true;
            Close();
        }
        catch (SettingsPersistenceException exception)
        {
            AppLog.Error("SettingsWindow", "설정 파일 저장 실패", exception);
            System.Windows.MessageBox.Show(
                this,
                $"설정을 파일에 저장하지 못했습니다.\n\n저장 경로: {exception.SettingsFilePath}\n원인: {exception.InnerException?.Message ?? exception.Message}\n\n경로 접근 권한과 디스크 상태를 확인한 뒤 다시 시도하세요.",
                "설정 저장 실패",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (StartupRegistrationException exception)
        {
            AppLog.Error("SettingsWindow", "자동 실행 레지스트리 반영 실패", exception);
            System.Windows.MessageBox.Show(
                this,
                $"Windows 자동 실행 옵션을 적용하지 못했습니다.\n\n레지스트리 경로: {exception.RegistryPath}\n값 이름: {exception.ValueName}\n원인: {exception.InnerException?.Message ?? exception.Message}\n\n자동 실행을 끈 상태로 먼저 사용한 뒤 나중에 다시 시도해도 됩니다.",
                "자동 실행 설정 실패",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception exception)
        {
            AppLog.Error("SettingsWindow", "설정 저장 중 예상하지 못한 오류", exception);
            System.Windows.MessageBox.Show(
                this,
                $"설정을 저장하는 중 예상하지 못한 오류가 발생했습니다.\n\n원인: {exception.Message}\n\n같은 문제가 반복되면 settings.json 경로와 로그를 함께 확인하세요.",
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
        AppLog.Info("SettingsWindow", "설정 저장 취소");
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// 로그 폴더를 파일 탐색기로 열어 사용자가 바로 첨부 파일을 찾을 수 있게 합니다.
    /// </summary>
    private void OpenLogFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppLog.LogDirectoryPath);
            Process.Start(new ProcessStartInfo(AppLog.LogDirectoryPath)
            {
                UseShellExecute = true
            });

            AppLog.Info("SettingsWindow", "로그 폴더 열기 성공");
        }
        catch (Exception exception)
        {
            AppLog.Error("SettingsWindow", "로그 폴더 열기 실패", exception);
            System.Windows.MessageBox.Show(
                this,
                "로그 폴더를 열지 못했습니다. 같은 문제가 반복되면 %LocalAppData%\\DesktopAudioController\\logs 경로를 직접 열어 주세요.",
                "로그 폴더 열기 실패",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
