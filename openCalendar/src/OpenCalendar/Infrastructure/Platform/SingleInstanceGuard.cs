using System.IO.Pipes;
using System.Text.Json;

namespace OpenCalendar.Infrastructure.Platform;

/// <summary>
/// Ensures one running instance per user. Later launches (e.g. a .opencalendar file)
/// forward their command-line arguments to the running instance over a named pipe.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private static readonly string Suffix = StableHash(Environment.UserDomainName + "\\" + Environment.UserName);
    private static readonly string MutexName = $@"Local\openCalendar.SingleInstance.{Suffix}";
    private static readonly string PipeName = $"openCalendar.Ipc.{Suffix}";

    private readonly Mutex _mutex;

    private SingleInstanceGuard(Mutex mutex) => _mutex = mutex;

    /// <returns>The guard if this is the first instance, otherwise <c>null</c>.</returns>
    public static SingleInstanceGuard? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew) return new SingleInstanceGuard(mutex);

        mutex.Dispose();
        return null;
    }

    public static bool SendToPrimary(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(3000);
            using var writer = new StreamWriter(client);
            writer.Write(JsonSerializer.Serialize(args));
            writer.Flush();
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Listens for arguments from other instances; <paramref name="onArguments"/> is called on a background thread.</summary>
    public void StartListening(Action<string[]> onArguments, CancellationToken cancellationToken) =>
        _ = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync(cancellationToken);

                    using var reader = new StreamReader(server);
                    var payload = await reader.ReadToEndAsync(cancellationToken);
                    var args = JsonSerializer.Deserialize<string[]>(payload) ?? Array.Empty<string>();
                    onArguments(args);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex) when (ex is IOException or JsonException)
                {
                    // Broken client – keep listening.
                }
            }
        }, cancellationToken);

    /// <summary>FNV-1a: string.GetHashCode() is randomized per process, so it can't be used for shared names.</summary>
    private static string StableHash(string value)
    {
        var hash = 2166136261u;
        foreach (var c in value.ToUpperInvariant())
        {
            hash ^= c;
            hash *= 16777619u;
        }
        return hash.ToString("X8");
    }

    public void Dispose()
    {
        try { _mutex.ReleaseMutex(); }
        catch (ApplicationException) { /* not owned by this thread */ }
        _mutex.Dispose();
    }
}
