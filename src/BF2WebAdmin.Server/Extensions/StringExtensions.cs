using System.Net;
using System.Net.Sockets;
using System.Text;

namespace BF2WebAdmin.Server.Extensions
{
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
            // Allow both hostname and IP address string
            var addressType = Uri.CheckHostName(address);
            if (addressType == UriHostNameType.Dns)
            {
                var result = await Dns.GetHostAddressesAsync(address);
                return result.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            }

            return IPAddress.Parse(address).MapToIPv4();
        }
    }
}
