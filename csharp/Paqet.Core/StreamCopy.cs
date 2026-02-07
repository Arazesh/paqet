namespace Paqet.Core;

public static class StreamCopy
{
    public static async Task CopyAsync(IStream source, IStream destination, int bufferSize = 16 * 1024, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[bufferSize];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }
}
