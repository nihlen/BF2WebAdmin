using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace BF2WebAdmin.Server
{
    public class BF2Rcon
    {
        private const string DigestSeedResponse = "### Digest seed: ";
        private const string NotAuthenticatedResponse = "error: not authenticated: you can only invoke 'login'";
        private const string AuthenticationFailedResponse = "Authentication failed.";
        private const string AuthenticationSuccessResponse = "Authentication successful, rcon ready.";

        public static string SendCommand(IPAddress ipAddress, int port, string password, string command)
        {
            using (var stream = GetStream(ipAddress, port))
            using (var reader = new StreamReader(stream))
            using (var writer = new BinaryWriter(stream))
            {
                while (true)
                {
                    var message = reader.ReadLine();
                    Console.WriteLine($"> '{message}'");
                    if (string.IsNullOrWhiteSpace(message))
                        continue;

                    if (message.StartsWith(DigestSeedResponse))
                    {
                        var hash = GetMD5Hash(message.Substring(17), password);
                        writer.Write(GetLoginBytes("login " + hash));
                    }
                    else if (message.StartsWith(NotAuthenticatedResponse))
                    {
                        throw new Exception("Not authenticated");
                    }
                    else if (message.StartsWith(AuthenticationFailedResponse))
                    {
                        throw new Exception("Authentication failed");
                    }
                    else if (message.StartsWith(AuthenticationSuccessResponse))
                    {
                        Console.WriteLine("Success");
                        break;
                    }
                }

                // Authenticated - Send command
                writer.Write(GetCommandBytes(command));
                var response = ReadCommandResponse(stream);
                Console.WriteLine($"> '{response}'");
                return response;
            }
        }

        private static NetworkStream GetStream(IPAddress ipAddress, int port)
        {
            var client = new TcpClient();
            client.Connect(new IPEndPoint(ipAddress, port));
            return client.GetStream();
        }

        private static string GetMD5Hash(string seed, string password)
        {
            var hash = ComputeHash(seed, password);
            var hex = ToHexString(hash);
            return hex;
        }

        private static byte[] ComputeHash(string seed, string password)
        {
            var bytes = Encoding.UTF8.GetBytes(seed + password);
            return MD5.Create().ComputeHash(bytes);
        }

        private static string ToHexString(byte[] bytes)
        {
            var hex = BitConverter.ToString(bytes);
            return hex.Replace("-", "").ToLower();
        }

        private static byte[] GetLoginBytes(string command)
        {
            return Encoding.UTF8.GetBytes(command + "\n");
        }

        private static byte[] GetCommandBytes(string command)
        {
            return Encoding.UTF8.GetBytes("\x02" + command + "\n");
        }

        private static string ReadCommandResponse(Stream stream)
        {
            var sb = new StringBuilder();
            while (true)
            {
                var ch = stream.ReadByte();
                if ((ch == -1) || (ch == 4))
                    return sb.ToString();

                sb.Append((char)ch);
            }
        }
    }
}
