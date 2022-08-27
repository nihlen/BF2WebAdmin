using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace BF2WebAdmin.Common;

public static class Compression
{
    /// <summary>
    /// Compress a string using GZIP
    /// Reference: https://gigi.nullneuron.net/gigilabs/compressing-strings-using-gzip-in-c/
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static string CompressText(string text)
    {
        var inputBytes = Encoding.UTF8.GetBytes(text);

        using var outputStream = new MemoryStream();
        using (var gZipStream = new GZipStream(outputStream, CompressionMode.Compress))
        {
            gZipStream.Write(inputBytes, 0, inputBytes.Length);
        }

        var outputBytes = outputStream.ToArray();
        return Convert.ToBase64String(outputBytes);
    }

    /// <summary>
    /// Decompress a string using GZIP
    /// Reference: https://gigi.nullneuron.net/gigilabs/compressing-strings-using-gzip-in-c/
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static string DecompressText(string text)
    {
        var inputBytes = Convert.FromBase64String(text);

        using var inputStream = new MemoryStream(inputBytes);
        using var gZipStream = new GZipStream(inputStream, CompressionMode.Decompress);
        using var streamReader = new StreamReader(gZipStream);

        return streamReader.ReadToEnd();
    }
}