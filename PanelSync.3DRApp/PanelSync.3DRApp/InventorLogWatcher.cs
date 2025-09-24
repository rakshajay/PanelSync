using System;
using System.IO;

namespace PanelSync._3DRApp
{
    internal sealed class InventorLogWatcher : IDisposable
    {
        private readonly string _logFile;
        private readonly Action<string> _onMessage;
        private FileSystemWatcher? _watcher;
        private long _lastSize = 0;

        public InventorLogWatcher(Action<string> onMessage)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var logsDir = Path.Combine(desktop, "PanelSyncHot", "logs");
            Directory.CreateDirectory(logsDir);

            _logFile = Path.Combine(logsDir, "inventor-addin.log");
            _onMessage = onMessage;

            // start watching
            _watcher = new FileSystemWatcher(logsDir, "inventor-addin.log")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnChanged;

            if (File.Exists(_logFile))
                _lastSize = new FileInfo(_logFile).Length;
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                using var fs = new FileStream(_logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);

                fs.Seek(_lastSize, SeekOrigin.Begin);
                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    string clean = line;
                    int idx = clean.IndexOf("] ");
                    if (idx >= 0 && idx + 2 < clean.Length)
                        clean = clean.Substring(idx + 2).Trim();

                    if (clean.Contains("✅ IGES import complete:"))
                        _onMessage("🎉 " + clean);

                    if (clean.Contains("Exported OBJ ->"))
                        _onMessage("📦 " + clean);

                    if (clean.Contains("OBJ will export"))
                        _onMessage("📊 " + clean);

                    // forward all warnings
                    if (line.Contains("[WARN]"))
                        _onMessage("⚠️ " + clean);
                }

                _lastSize = fs.Length;
            }
            catch
            {
                // ignore transient IO issues
            }
        }

        public void Dispose()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }
        }
    }
}
