using System.Windows;
using DesktopAudioController.ViewModels;

namespace DesktopAudioController.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly Func<SettingsViewModel> _settingsViewModelFactory;

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

    public void OpenSettingsOnFirstRun()
    {
        OpenSettingsInternal();
    }

    private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        OpenSettingsInternal();
    }

    private void RefreshButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Load();
        UpdateEmptyState();
    }

    private void OpenSettingsInternal()
    {
        var settingsViewModel = _settingsViewModelFactory();
        settingsViewModel.Load();

        var settingsWindow = new SettingsWindow(settingsViewModel)
        {
            Owner = this
        };

        var result = settingsWindow.ShowDialog();
        if (result == true)
        {
            _viewModel.Load();
            UpdateEmptyState();
        }
    }

    private void UpdateEmptyState()
    {
        var hasDevices = _viewModel.VisibleDevices.Count > 0;
        EmptyStateText.Visibility = hasDevices ? Visibility.Collapsed : Visibility.Visible;
        DevicesItemsControl.Visibility = hasDevices ? Visibility.Visible : Visibility.Collapsed;
    }
}
