namespace DesktopAudioController.ViewModels;

/// <summary>
/// 설정창의 프로필 선택 콤보박스에 표시할 얇은 항목 모델입니다.
/// </summary>
public sealed class AudioProfileSelectionViewModel
{
    public AudioProfileSelectionViewModel(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }

    public string Name { get; }
}
