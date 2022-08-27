using System.Net;
using System.Net.Sockets;
using System.Text;

namespace BF2WebAdmin.Server.Extensions;

public static class StringExtensions
{
    public static string ReplacePlaceholders(this string text, Dictionary<string, string> replacements)
    {
        var sb = new StringBuilder(text);
        foreach (var replacement in replacements)
            sb.Replace(replacement.Key, replacement.Value);

        return sb.ToString();
    }

    public static async Task<IPAddress> GetIpAddressAsync(this string address)
    {
        var isIpAddress = IPAddress.TryParse(address, out var ipResult);

        // Allow both hostname and IP address string
        var addressType = Uri.CheckHostName(address);
        if (addressType == UriHostNameType.Dns || !isIpAddress)
        {
            var result = await Dns.GetHostAddressesAsync(address);
            var firstIPv4 = result.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            if (firstIPv4 != null)
                return firstIPv4;
        }

        if (isIpAddress) 
            return ipResult!.MapToIPv4();

        throw new Exception("Invalid address: " + address);
    }
}