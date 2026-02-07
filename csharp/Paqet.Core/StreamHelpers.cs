namespace Paqet.Core;

public static class StreamHelpers
{
    public static async ValueTask ReadExactAsync(IStream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of stream.");
            }
            offset += read;
        }
    }
}
