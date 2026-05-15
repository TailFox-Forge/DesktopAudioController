using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopAudioController.Services;

/// <summary>
/// 실행 파일 경로별로 앱 아이콘을 캐싱해 반복 조회 비용을 줄이는 서비스입니다.
/// </summary>
public sealed class CachedAppIconService : IAppIconService
{
    // 실행 파일 경로를 키로 사용해 한 번 읽은 아이콘을 재사용합니다.
    private readonly ConcurrentDictionary<string, ImageSource?> _iconCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 실행 파일 경로의 아이콘을 조회하고, 이미 읽은 경로라면 캐시된 값을 반환합니다.
    /// </summary>
    public ImageSource? GetIcon(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        if (!File.Exists(executablePath))
        {
            return null;
        }

        return _iconCache.GetOrAdd(executablePath, static path => LoadIcon(path));
    }

    /// <summary>
    /// 실제 파일에서 아이콘을 읽어 WPF ImageSource로 변환합니다.
    /// </summary>
    private static ImageSource? LoadIcon(string executablePath)
    {
        try
        {
            // shellIcon은 실행 파일의 기본 아이콘 핸들을 가진 GDI 리소스입니다.
            using Icon? shellIcon = Icon.ExtractAssociatedIcon(executablePath);
            if (shellIcon is null)
            {
                return null;
            }

            // bitmapSource는 WPF에서 직접 바인딩할 수 있는 비트맵 표현입니다.
            var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                shellIcon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(16, 16));
            bitmapSource.Freeze();
            return bitmapSource;
        }
        catch
        {
            // 권한 문제나 특수 경로 문제로 아이콘을 읽지 못하면 아이콘 없이 표시합니다.
            return null;
        }
    }
}
