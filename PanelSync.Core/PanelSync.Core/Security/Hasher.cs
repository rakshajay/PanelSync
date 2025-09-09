using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PanelSync.Core.Security
{
    //[08/27/2025]:Raksha- SHA256 helpers for files/strings (C# 7.3 safe).
    public static class Hasher
    {
        public static string FileSha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var hash = sha.ComputeHash(fs);
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        public static string StringSha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }
    }
}
