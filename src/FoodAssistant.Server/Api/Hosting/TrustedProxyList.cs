using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace FoodAssistant.Server.Api.Hosting;

/// <summary>Parses <see cref="HostingOptions.TrustedProxies"/>.</summary>
internal sealed record TrustedProxyList(
    IReadOnlyList<IPAddress> Addresses,
    IReadOnlyList<IPNetwork> Networks,
    IReadOnlyList<string> InvalidEntries)
{
    private static readonly char[] Separators = [',', ';', ' '];

    public static TrustedProxyList Parse(string? value)
    {
        var addresses = new List<IPAddress>();
        var networks = new List<IPNetwork>();
        var invalid = new List<string>();
        foreach (var entry in (value ?? string.Empty).Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (entry.Contains('/', StringComparison.Ordinal))
            {
                if (TryParseNetwork(entry, out var network))
                {
                    networks.Add(network);
                }
                else
                {
                    invalid.Add(entry);
                }
            }
            else if (TryParseAddress(entry, out var address))
            {
                addresses.Add(address);
            }
            else
            {
                invalid.Add(entry);
            }
        }

        return new TrustedProxyList(addresses, networks, invalid);
    }

    /// <summary>
    /// Only canonical IPv4 (<c>172.16.0.0</c>) or IPv6: <see cref="IPAddress.TryParse(string?, out IPAddress?)"/>
    /// also accepts shorthand (<c>172.16</c> = 172.0.0.16), octal (<c>172.016.0.0</c>) and hex,
    /// all likely typos for another network.
    /// </summary>
    private static bool TryParseAddress(string text, [NotNullWhen(true)] out IPAddress? address) =>
        IPAddress.TryParse(text, out address)
        && (address.AddressFamily == AddressFamily.InterNetworkV6 || address.ToString() == text);

    /// <summary>
    /// Accepts host bits in the address (<c>172.18.0.1/16</c>, as <c>docker network inspect</c>
    /// shows the gateway) and masks them; <see cref="IPNetwork.TryParse(string?, out IPNetwork)"/> rejects those.
    /// </summary>
    private static bool TryParseNetwork(string entry, out IPNetwork network)
    {
        network = default;
        var parts = entry.Split('/');
        if (parts.Length != 2
            || !TryParseAddress(parts[0], out var address)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var prefixLength))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        if (prefixLength > bytes.Length * 8)
        {
            return false;
        }

        for (var bit = prefixLength; bit < bytes.Length * 8; bit++)
        {
            bytes[bit / 8] &= (byte)~(0x80 >> (bit % 8));
        }

        network = new IPNetwork(new IPAddress(bytes), prefixLength);
        return true;
    }
}
