using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PanelSync.Core.IO
{
    //[08/27/2025]:Raksha- Detect when a file has settled.
    public static class FileStability
    {
        public static async Task<bool> WaitUntilStableAsync(
            string path, TimeSpan stableFor, TimeSpan poll, CancellationToken ct)
        {
            if (!File.Exists(path)) return false;

            long lastSize = -1;
            DateTime lastChange = DateTime.UtcNow;

            while (!ct.IsCancellationRequested)
            {
                if (!File.Exists(path)) return false;

                long size = GetSizeSafe(path);
                if (size != lastSize)
                {
                    lastSize = size;
                    lastChange = DateTime.UtcNow;
                }

                if (DateTime.UtcNow - lastChange >= stableFor && CanOpenExclusively(path))
                    return true;

                await Task.Delay(poll, ct);
            }
            return false;
        }

        private static long GetSizeSafe(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return -1; }
        }

        private static bool CanOpenExclusively(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                { var _ = fs.Length; }
                return true;
            }
            catch { return false; }
        }
    }
}
