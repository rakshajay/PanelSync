// SimpleFileLogger.cs
using System;
using System.IO;
using System.Text;

namespace PanelSync.Core.Logging
{
    //[08/27/2025]:Raksha- Thread-safe rolling file logger.
    public class SimpleFileLogger : ILog, IDisposable
    {
        private readonly string _logPath;
        private readonly long _maxBytes;
        private readonly object _gate = new object();
        private readonly Encoding _utf8 = new UTF8Encoding(false);
        private bool _disposed;

        public SimpleFileLogger(string logPath, long maxBytes = 2_000_000)
        {
            var dir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            _logPath = logPath;
            _maxBytes = maxBytes;
        }

        public void Info(string message) { Write("INFO", message); }
        public void Warn(string message) { Write("WARN", message); }
        public void Debug(string message) { Write("DEBUG", message); }

        public void Error(string message, Exception ex = null)
        {
            Write("ERROR", ex == null ? message : message + Environment.NewLine + ex);
        }

        private void Write(string level, string message)
        {
            if (_disposed) return;
            var line = DateTime.UtcNow.ToString("O") + " [" + level + "] " + message + Environment.NewLine;
            lock (_gate)
            {
                RollIfNeeded_NoLock();
                File.AppendAllText(_logPath, line, _utf8);
            }
        }

        private void RollIfNeeded_NoLock()
        {
            try
            {
                if (File.Exists(_logPath) && new FileInfo(_logPath).Length > _maxBytes)
                {
                    var rolled = _logPath + ".1";
                    if (File.Exists(rolled)) File.Delete(rolled);
                    File.Move(_logPath, rolled);
                }
            }
            catch { /* Logging must not crash. */ }
        }

        public void Dispose() { _disposed = true; }
    }
}
