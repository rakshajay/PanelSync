using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PanelSync.Core.Logging;
using PanelSync.Core.IO;

namespace PanelSync.Core.Services
{
    //[08/27/2025]:Raksha- FileSystemWatcher + stability gate.
    public class WatchService : IDisposable
    {
        private readonly FileSystemWatcher _fsw;
        private readonly ILog _log;

        public WatchService(string folder, string filter, ILog log)
        {
            _log = log;
            _fsw = new FileSystemWatcher(folder, filter) { IncludeSubdirectories = false, EnableRaisingEvents = true };
        }

        public void OnCreated(Func<string, Task> stableHandler)
        {
            _fsw.Created += async (s, e) =>
            {
                _log.Info("Detected new file -> " + e.FullPath);
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var ok = await FileStability.WaitUntilStableAsync(e.FullPath, TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(120), cts.Token); // :contentReference[oaicite:14]{index=14}
                if (ok) await stableHandler(e.FullPath);
            };
        }

        public void Dispose() { _fsw.Dispose(); }
    }
}
