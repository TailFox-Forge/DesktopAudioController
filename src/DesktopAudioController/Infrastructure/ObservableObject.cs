using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DesktopAudioController.Infrastructure;

/// <summary>
/// WPF 바인딩 갱신을 위한 최소 MVVM 기반 클래스입니다.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <summary>
    /// 속성 값이 바뀌었음을 UI에 알리는 이벤트입니다.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 필드 값이 실제로 바뀐 경우에만 변경 알림을 발생시킵니다.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        // 이전 값과 같으면 불필요한 UI 갱신을 막습니다.
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        // 내부 필드를 갱신한 뒤 바인딩 변경 이벤트를 올립니다.
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// 지정한 속성의 변경 이벤트를 직접 발생시킵니다.
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
