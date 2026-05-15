using System.Windows;
using DesktopAudioController.Services;
using DesktopAudioController.ViewModels;
using DesktopAudioController.Views;

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

    /// <summary>
    /// 앱 시작 시 서비스 초기화, 메인 뷰모델 로드, 메인 창 표시를 수행합니다.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Phase 2부터는 실제 Core Audio 서비스로 장치와 세션을 조회합니다.
        _settingsService = new SettingsService();
        _audioDeviceCatalogService = new NativeAudioDeviceCatalogService();
        _processMetadataCacheService = new CachedProcessMetadataService();
        _audioSessionService = new NativeAudioSessionService(_processMetadataCacheService);
        _audioNotificationService = new NativeAudioNotificationService();
        _appIconService = new CachedAppIconService();
        _audioNotificationService.Start();

        // 메인 화면에서 사용할 뷰모델을 만들고 저장된 설정 기준으로 데이터를 채웁니다.
        var mainViewModel = new MainViewModel(
            _settingsService,
            _audioDeviceCatalogService,
            _audioSessionService,
            _appIconService);
        mainViewModel.Load();

        // 메인 창은 설정 창 팩토리를 받아 필요할 때마다 새 설정 뷰모델을 생성합니다.
        var mainWindow = new MainWindow(
            mainViewModel,
            CreateSettingsViewModel,
            _audioNotificationService,
            _settingsService);
        MainWindow = mainWindow;
        mainWindow.Show();

        // 첫 실행처럼 표시할 장치가 아직 없으면 설정 창을 바로 띄웁니다.
        if (!mainViewModel.HasConfiguredDevices)
        {
            mainWindow.OpenSettingsOnFirstRun();
        }
    }

    /// <summary>
    /// 앱 종료 시 네이티브 오디오 서비스가 잡고 있는 COM 리소스를 정리합니다.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        (_audioDeviceCatalogService as IDisposable)?.Dispose();
        (_audioSessionService as IDisposable)?.Dispose();
        _audioNotificationService?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// 설정 창 전용 뷰모델을 새로 생성합니다.
    /// </summary>
    private SettingsViewModel CreateSettingsViewModel()
    {
        return new SettingsViewModel(
            _settingsService ?? throw new InvalidOperationException("Settings service is not initialized."),
            _audioDeviceCatalogService ?? throw new InvalidOperationException("Audio device service is not initialized."));
    }
}
