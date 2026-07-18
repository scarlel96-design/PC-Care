namespace SmartPerformanceDoctor.AstraVault.Durable;

public sealed class Av3DurableWriteHandle : IDisposable
{
    internal Av3DurableWriteHandle(string absolutePath, string relativePath)
    {
        AbsolutePath = absolutePath;
        RelativePath = relativePath;
        Stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None);
    }

    public string AbsolutePath { get; }
    public string RelativePath { get; }
    internal FileStream Stream { get; }

    public void Write(ReadOnlySpan<byte> data) => Stream.Write(data);

    public void Dispose()
    {
        Stream.Dispose();
    }
}