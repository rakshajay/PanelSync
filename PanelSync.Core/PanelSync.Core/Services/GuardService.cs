using System;
using System.IO;
using PanelSync.Core.Logging;

namespace PanelSync.Core.Services
{
    //[08/27/2025]:Raksha- Guards for quick validations.
    public class GuardService
    {
        private readonly ILog _log;
        public GuardService(ILog log) { _log = log; }

        public void EnsureFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                _log.Info("Creating folder -> " + path);
                Directory.CreateDirectory(path);
            }
        }

        public bool ProjectMatches(string expected, string incoming)
        {
            var ok = string.Equals(expected, incoming, StringComparison.OrdinalIgnoreCase);
            if (!ok) _log.Warn("ProjectId mismatch: " + incoming + " != " + expected);
            return ok;
        }
    }
}
