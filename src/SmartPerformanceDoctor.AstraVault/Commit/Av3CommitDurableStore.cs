using SmartPerformanceDoctor.AstraVault.Durable;
using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.WriterDesign;

namespace SmartPerformanceDoctor.AstraVault.Commit;

/// <summary>
/// Isolated-root durable store: temp write + <see cref="FileStream.Flush(bool)"/> + rename (E-6 harness).
/// </summary>
public sealed class Av3CommitDurableStore : IAv3DurableStore
{
    private readonly string _root;
    private readonly Av3CommitSimulationOptions _simulation;

    public Av3CommitDurableStore(string vaultRoot, Av3CommitSimulationOptions simulation)
    {
        Av3WriterAccessGate.EnsureIsolatedRoot(vaultRoot);
        _root = vaultRoot;
        _simulation = simulation;
    }

    public async ValueTask<Av3DurableStoreWriteResult> WriteTempThenCommitAsync(
        string relativePath,
        ReadOnlyMemory<byte> payload,
        Av3DurableCommitOptions options,
        CancellationToken cancellationToken = default)
    {
        _ = options;
        Av3TestStorage.ValidateRelativePath(relativePath);
        var finalPath = Path.Combine(_root, relativePath);
        var tempPath = finalPath + ".pending";
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

        await File.WriteAllBytesAsync(tempPath, payload.ToArray(), cancellationToken).ConfigureAwait(false);

        if (_simulation.PartialWriteTruncate && File.Exists(tempPath))
        {
            await using var trunc = new FileStream(tempPath, FileMode.Open, FileAccess.Write);
            trunc.SetLength(Math.Min(8, trunc.Length));
        }

        try
        {
            await FlushFileAsync(tempPath, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return new Av3DurableStoreWriteResult { Durable = false, PublicErrorClass = "flush_failed" };
        }

        if (File.Exists(finalPath))
        {
            File.Delete(finalPath);
        }

        File.Move(tempPath, finalPath);
        return new Av3DurableStoreWriteResult { Durable = true, PublicErrorClass = "ok" };
    }

    public async ValueTask FlushDirectoryAsync(string relativeDirectory, CancellationToken cancellationToken = default)
    {
        Av3TestStorage.ValidateRelativePath(relativeDirectory.TrimEnd('/'));
        var path = Path.Combine(_root, relativeDirectory);
        if (!Directory.Exists(path))
        {
            return;
        }

        await Task.Run(() =>
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var fs = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                fs.Flush(flushToDisk: true);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public byte[]? RereadRelative(string relativePath)
    {
        Av3TestStorage.ValidateRelativePath(relativePath);
        var path = Path.Combine(_root, relativePath);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    internal static async Task FlushFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        cancellationToken.ThrowIfCancellationRequested();
        fs.Flush(flushToDisk: true);
    }

    internal void MaybeFailFlush(Av3CommitPipelineStep step)
    {
        if (_simulation.FailFlushAtStep == step)
        {
            throw new InvalidOperationException("simulated_flush_failure");
        }
    }
}