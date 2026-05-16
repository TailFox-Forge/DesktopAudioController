using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopAudioController.Services;

/// <summary>
/// 앱을 단일 인스턴스로 제한하고, 후속 실행이 기존 인스턴스를 활성화할 수 있게 돕습니다.
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    private const string ActivationCommand = "activate";
    private readonly string _mutexName;
    private readonly string _pipeName;
    private Mutex? _mutex;
    private CancellationTokenSource? _listenerCancellationTokenSource;
    private Task? _listenerTask;
    private bool _ownsPrimaryInstance;

    public SingleInstanceService(string instanceKey = "TailFoxForge.DesktopAudioController")
    {
        if (string.IsNullOrWhiteSpace(instanceKey))
        {
            throw new ArgumentException("Instance key must not be empty.", nameof(instanceKey));
        }

        var sanitizedKey = SanitizeInstanceKey(instanceKey);
        _mutexName = OperatingSystem.IsWindows() ? $@"Local\{sanitizedKey}" : sanitizedKey;
        _pipeName = $"{sanitizedKey}.activate";
    }

    public bool TryAcquirePrimaryInstance()
    {
        if (_mutex is not null)
        {
            return _ownsPrimaryInstance;
        }

        var mutex = new Mutex(initiallyOwned: true, _mutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return false;
        }

        _mutex = mutex;
        _ownsPrimaryInstance = true;
        return true;
    }

    public void StartActivationListener(Func<Task> activationHandler)
    {
        ArgumentNullException.ThrowIfNull(activationHandler);

        if (!_ownsPrimaryInstance)
        {
            throw new InvalidOperationException("Primary instance must be acquired before starting the activation listener.");
        }

        if (_listenerTask is not null)
        {
            return;
        }

        _listenerCancellationTokenSource = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenForActivationRequestsAsync(activationHandler, _listenerCancellationTokenSource.Token));
    }

    public async Task<bool> TryNotifyExistingInstanceAsync(TimeSpan timeout)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            using var cancellationTokenSource = new CancellationTokenSource(timeout);
            await client.ConnectAsync((int)timeout.TotalMilliseconds, cancellationTokenSource.Token).ConfigureAwait(false);
            using var writer = new StreamWriter(client, Encoding.UTF8, bufferSize: 1024, leaveOpen: false)
            {
                AutoFlush = true
            };
            await writer.WriteLineAsync(ActivationCommand).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        try
        {
            _listenerCancellationTokenSource?.Cancel();
            _listenerTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
        }
        finally
        {
            _listenerCancellationTokenSource?.Dispose();
            _listenerCancellationTokenSource = null;
            _listenerTask = null;
        }

        if (_ownsPrimaryInstance && _mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
            }
        }

        _mutex?.Dispose();
        _mutex = null;
        _ownsPrimaryInstance = false;
    }

    private async Task ListenForActivationRequestsAsync(Func<Task> activationHandler, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            try
            {
                using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: false);
                var command = await reader.ReadLineAsync().ConfigureAwait(false);
                if (string.Equals(command, ActivationCommand, StringComparison.Ordinal))
                {
                    await activationHandler().ConfigureAwait(false);
                }
            }
            catch
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private static string SanitizeInstanceKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '.');
        }

        return builder.ToString();
    }
}
