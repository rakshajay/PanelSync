using System;
using System.IO;
using System.Text;

namespace PanelSync.Core.IO
{
    //[08/27/2025]:Raksha- Atomic file writer: write -> .tmp, then rename/replace.
    public static class AtomicWriter
    {
        // Write all text to a file atomically. 
        public static void WriteAllText(string path, string content, Encoding enc = null)
        {
            var bytes = (enc ?? new UTF8Encoding(false)).GetBytes(content); //[08/27/2025]:Raksha- UTF8 no BOM
            WriteAllBytes(path, bytes);
        }

        public static void WriteAllBytes(string path, byte[] data)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tmpBase = path + ".tmp";
            var tmp = MakeUniqueTmp(tmpBase);

            // write temp
            using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                fs.Write(data, 0, data.Length);
                fs.Flush(true);
            }

            try
            {
                if (File.Exists(path))
                    File.Replace(tmp, path, null, true);
                else
                    File.Move(tmp, path);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        private static string MakeUniqueTmp(string baseTmp)
        {
            var dir = Path.GetDirectoryName(baseTmp) ?? ".";
            var name = Path.GetFileName(baseTmp);
            for (int i = 0; i < 5; i++)
            {
                var candidate = Path.Combine(dir, name + "." + Environment.TickCount + "." + i);
                if (!File.Exists(candidate)) return candidate;
            }
            return baseTmp + "." + Guid.NewGuid().ToString("N");
        }
    }
}
