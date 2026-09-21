using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Numerics;

namespace NetIPConfig;

/// <summary>
/// Parsing and sanity checks for the dotted-quad values typed into the form.
/// <see cref="IPAddress.TryParse(string, out IPAddress)"/> is deliberately not used
/// on its own because it accepts shorthand such as "10.1" that users rarely mean.
/// </summary>
internal static class IPv4Text
{
    /// <summary>Strict dotted-quad parse: exactly four decimal octets, each 0-255.</summary>
    public static bool TryParse(string? text, out IPAddress address)
    {
        address = IPAddress.None;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] parts = text.Trim().Split('.');
        if (parts.Length != 4)
        {
            return false;
        }

        byte[] octets = new byte[4];
        for (int i = 0; i < 4; i++)
        {
            string part = parts[i];
            if (part.Length is < 1 or > 3)
            {
                return false;
            }

            foreach (char c in part)
            {
                if (c is < '0' or > '9')
                {
                    return false;
                }
            }

            if (!byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out octets[i]))
            {
                return false;
            }
        }

        address = new IPAddress(octets);
        return true;
    }

    public static uint ToUInt32(IPAddress address) =>
        BinaryPrimitives.ReadUInt32BigEndian(address.GetAddressBytes());

    public static IPAddress FromUInt32(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return new IPAddress(bytes);
    }

    /// <summary>A subnet mask must be a non-zero, contiguous run of leading 1 bits.</summary>
    public static bool IsValidMask(IPAddress mask, out int prefixLength)
    {
        prefixLength = 0;
        uint value = ToUInt32(mask);
        if (value == 0)
        {
            return false;
        }

        uint inverted = ~value;
        // Contiguous only if the inverted mask is of the form 2^n - 1.
        if ((inverted & (inverted + 1)) != 0)
        {
            return false;
        }

        prefixLength = BitOperations.PopCount(value);
        return true;
    }

    public static IPAddress MaskFromPrefixLength(int prefixLength)
    {
        if (prefixLength is < 0 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(prefixLength));
        }

        uint value = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
        return FromUInt32(value);
    }

    /// <summary>Default mask for the address class, used to prefill the mask box.</summary>
    public static IPAddress ClassfulMaskFor(IPAddress address)
    {
        byte first = address.GetAddressBytes()[0];
        return first switch
        {
            < 128 => IPAddress.Parse("255.0.0.0"),
            < 192 => IPAddress.Parse("255.255.0.0"),
            _ => IPAddress.Parse("255.255.255.0"),
        };
    }

    public static bool SameSubnet(IPAddress a, IPAddress b, IPAddress mask)
    {
        uint m = ToUInt32(mask);
        return (ToUInt32(a) & m) == (ToUInt32(b) & m);
    }

    public static bool IsNetworkAddress(IPAddress address, IPAddress mask)
    {
        uint m = ToUInt32(mask);
        return (ToUInt32(address) & ~m) == 0;
    }

    public static bool IsBroadcastAddress(IPAddress address, IPAddress mask)
    {
        uint m = ToUInt32(mask);
        return (ToUInt32(address) | m) == uint.MaxValue;
    }

    /// <summary>
    /// Rejects addresses that can never be assigned to an interface as a host address:
    /// 0.x, loopback, multicast and the 255.x reserved range.
    /// </summary>
    public static bool IsAssignableHostAddress(IPAddress address, out string reason)
    {
        byte first = address.GetAddressBytes()[0];
        switch (first)
        {
            case 0:
                reason = "addresses starting with 0 are not valid host addresses";
                return false;
            case 127:
                reason = "127.x.x.x is reserved for loopback";
                return false;
            case >= 224 and <= 239:
                reason = "224-239.x.x.x is reserved for multicast";
                return false;
            case >= 240:
                reason = "240.x.x.x and above are reserved";
                return false;
            default:
                reason = "";
                return true;
        }
    }
}
