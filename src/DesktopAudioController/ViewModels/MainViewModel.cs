using System.Collections.ObjectModel;
using System.Linq;
using DesktopAudioController.Infrastructure;
using DesktopAudioController.Services;

namespace DesktopAudioController.ViewModels;

/// <summary>
/// 메인 화면에 표시할 장치 목록과 첫 실행 상태를 관리하는 뷰모델입니다.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    // 저장된 사용자 설정을 읽기 위한 서비스입니다.
    private readonly ISettingsService _settingsService;

    // 현재 출력 장치 상태를 조회하기 위한 서비스입니다.
    private readonly IAudioDeviceCatalogService _audioDeviceCatalogService;

    // 사용자가 한 번이라도 표시 장치를 설정했는지 여부입니다.
    private bool _hasConfiguredDevices;

    /// <summary>
    /// 메인 화면이 사용할 서비스들을 주입받습니다.
    /// </summary>
    public MainViewModel(
        ISettingsService settingsService,
        IAudioDeviceCatalogService audioDeviceCatalogService)
    {
        _settingsService = settingsService;
        _audioDeviceCatalogService = audioDeviceCatalogService;
    }

    /// <summary>
    /// 메인 화면에 실제로 렌더링될 장치 카드 목록입니다.
    /// </summary>
    public ObservableCollection<VisibleDeviceViewModel> VisibleDevices { get; } = [];

    /// <summary>
    /// 표시 장치가 한 번이라도 저장되었는지 나타냅니다.
    /// </summary>
    public bool HasConfiguredDevices
    {
        get => _hasConfiguredDevices;
        private set => SetProperty(ref _hasConfiguredDevices, value);
    }

    /// <summary>
    /// 설정 파일과 장치 목록을 조합해 메인 화면 상태를 다시 계산합니다.
    /// </summary>
    public void Load()
    {
        // 저장된 표시 대상 장치 목록을 읽습니다.
        // 파일에서 읽어온 현재 사용자 설정입니다.
        var settings = _settingsService.Load();

        // 현재 시스템에서 보이는 장치 상태를 조회합니다.
        var devices = _audioDeviceCatalogService.GetAvailableOutputDevices();

        // 조회 성능을 위해 선택된 ID 목록을 해시셋으로 변환합니다.
        var selectedIds = settings.VisibleDeviceIds.ToHashSet();

        // 메인 화면에는 사용자가 선택한 장치만 남기고 필요 시 연결된 장치만 필터링합니다.
        var visibleDevices = devices
            .Where(device => selectedIds.Contains(device.Id))
            .Where(device => !settings.ShowOnlyConnectedDevices || device.IsConnected)
            .Select(device => new VisibleDeviceViewModel
            {
                Id = device.Id,
                Name = device.Name,
                IsConnected = device.IsConnected,
                IsDefault = device.IsDefault,
                IsMuted = device.IsMuted,
                Volume = device.Volume
            })
            .ToList();

        // 새 결과를 화면 컬렉션에 반영합니다.
        VisibleDevices.Clear();
        foreach (var device in visibleDevices)
        {
            // 루프 안의 device는 메인 화면에 실제 표시할 장치 카드 한 개입니다.
            VisibleDevices.Add(device);
        }

        // 선택된 장치가 하나라도 있으면 첫 실행 안내를 띄우지 않도록 상태를 갱신합니다.
        HasConfiguredDevices = selectedIds.Count > 0;
    }
}
