namespace DesktopAudioController.Services;

internal sealed class AudioSessionOutputChangeResult
{
    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }
}
