namespace DesktopAudioController.Services;

public sealed record DiagnosticIssueDraft(
    string Title,
    string Body,
    string IssueUrl,
    string DiagnosticPackagePath);
