namespace Paqet.Core;

public sealed record Address(string Host, int Port)
{
    public override string ToString() => $"{Host}:{Port}";

    public static Address Parse(string value)
    {
        var lastColon = value.LastIndexOf(':');
        if (lastColon <= 0 || lastColon == value.Length - 1)
        {
            throw new FormatException($"Invalid address: {value}");
        }

        var host = value[..lastColon];
        var port = int.Parse(value[(lastColon + 1)..]);
        return new Address(host, port);
    }
}
