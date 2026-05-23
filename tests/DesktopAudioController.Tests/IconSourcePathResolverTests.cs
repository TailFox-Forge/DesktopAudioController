using DesktopAudioController.Services;

namespace DesktopAudioController.Tests;

public sealed class IconSourcePathResolverTests
{
    [Fact]
    public void ResolvePreferredIconSourcePath_FallsBackToExecutable_WhenSessionIconIsPackageResource()
    {
        using var tempDirectory = new TemporaryDirectory();
        var executablePath = Path.Combine(tempDirectory.DirectoryPath, "StoreAppHost.exe");
        File.WriteAllText(executablePath, string.Empty);

        var resolvedPath = IconSourcePathResolver.ResolvePreferredIconSourcePath(
            "@{Package?ms-resource://StoreApp/Files/Assets/AppIcon.png}",
            executablePath);

        Assert.Equal(executablePath, resolvedPath);
    }

    [Fact]
    public void ResolvePackageIconSourcePath_ResolvesPackagedAssetVariant()
    {
        using var tempDirectory = new TemporaryDirectory();
        var packageDirectory = Path.Combine(tempDirectory.DirectoryPath, "Contoso.App_1.0.0.0_x64__abc123");
        var assetPath = Path.Combine(packageDirectory, "Assets", "AppIcon.scale-200.png");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        File.WriteAllText(assetPath, string.Empty);

        var resolvedPath = IconSourcePathResolver.ResolvePackageIconSourcePath(
            "@{Contoso.App_abc123?ms-resource://Contoso.App/Files/Assets/AppIcon.png}",
            [tempDirectory.DirectoryPath]);

        Assert.Equal(assetPath, resolvedPath);
    }

    [Fact]
    public void ResolvePreferredIconSourcePath_UsesPackagedIconBeforeExecutableFallback()
    {
        using var tempDirectory = new TemporaryDirectory();
        var packageDirectory = Path.Combine(tempDirectory.DirectoryPath, "Contoso.App_1.0.0.0_x64__abc123");
        var assetPath = Path.Combine(packageDirectory, "Assets", "AppIcon.targetsize-32.png");
        var executablePath = Path.Combine(tempDirectory.DirectoryPath, "ApplicationFrameHost.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        File.WriteAllText(assetPath, string.Empty);
        File.WriteAllText(executablePath, string.Empty);

        var resolvedPath = IconSourcePathResolver.ResolvePreferredIconSourcePath(
            "@{Contoso.App_abc123?ms-resource://Contoso.App/Files/Assets/AppIcon.png}",
            executablePath,
            [tempDirectory.DirectoryPath]);

        Assert.Equal(assetPath, resolvedPath);
    }

    [Fact]
    public void ResolvePreferredIconSourcePath_PrefersSessionFileIcon_WhenResourceIndexIsPresent()
    {
        using var tempDirectory = new TemporaryDirectory();
        var iconLibraryPath = Path.Combine(tempDirectory.DirectoryPath, "AppIcon.dll");
        var executablePath = Path.Combine(tempDirectory.DirectoryPath, "App.exe");
        File.WriteAllText(iconLibraryPath, string.Empty);
        File.WriteAllText(executablePath, string.Empty);

        var resolvedPath = IconSourcePathResolver.ResolvePreferredIconSourcePath(
            $"\"{iconLibraryPath}\",-123",
            executablePath);

        Assert.Equal(iconLibraryPath, resolvedPath);
    }

    [Fact]
    public void NormalizeFileIconSourcePath_ReturnsNull_WhenPathIsPackageResource()
    {
        var resolvedPath = IconSourcePathResolver.NormalizeFileIconSourcePath(
            "@{Package?ms-resource://StoreApp/Files/Assets/AppIcon.png}");

        Assert.Null(resolvedPath);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                "DesktopAudioController.Tests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, recursive: true);
                }
            }
            catch
            {
                // 테스트 정리 실패는 본문 검증보다 우선순위가 낮습니다.
            }
        }
    }
}
