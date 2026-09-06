using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace DlssSwapper.Linux.Gui;

internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _name;
    private readonly CancellationTokenSource _stop = new();
    private Task? _listener;
    public bool IsPrimary { get; }

    public SingleInstance(string? identity = null)
    {
        identity ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _name = "LLE.GUI." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..24];
        _mutex = new Mutex(false, _name);
        try { IsPrimary = _mutex.WaitOne(0); }
        catch (AbandonedMutexException) { IsPrimary = true; }
    }

    public void Listen(Action activate)
    {
        if (!IsPrimary || _listener is not null) throw new InvalidOperationException("Only the primary instance can listen.");
        _listener = Task.Run(async () =>
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    await using var pipe = new NamedPipeServerStream(_name, PipeDirection.InOut, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(_stop.Token);
                    var request = new byte[1];
                    using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                    requestTimeout.CancelAfter(TimeSpan.FromSeconds(3));
                    try
                    {
                        if (await pipe.ReadAsync(request, requestTimeout.Token) == 1 && request[0] == 1)
                        {
                            activate();
                            await pipe.WriteAsync(new byte[] { 1 }, requestTimeout.Token);
                        }
                    }
                    catch (IOException) { /* A departing secondary must not stop activation for later launches. */ }
                    catch (OperationCanceledException) when (!_stop.IsCancellationRequested) { }
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        });
    }

    public void ActivateExisting()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var pipe = new NamedPipeClientStream(".", _name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        pipe.ConnectAsync(timeout.Token).GetAwaiter().GetResult();
        pipe.WriteAsync(new byte[] { 1 }, timeout.Token).AsTask().GetAwaiter().GetResult();
        var response = new byte[1];
        if (pipe.ReadAsync(response, timeout.Token).AsTask().GetAwaiter().GetResult() != 1 || response[0] != 1)
            throw new IOException("The running application did not acknowledge activation.");
    }

    public void Dispose()
    {
        _stop.Cancel();
        try { _listener?.GetAwaiter().GetResult(); }
        finally
        {
            if (IsPrimary) _mutex.ReleaseMutex();
            _mutex.Dispose(); _stop.Dispose();
        }
    }
}
