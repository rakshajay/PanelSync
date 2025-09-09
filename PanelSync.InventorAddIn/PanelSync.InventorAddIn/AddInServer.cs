//[08/28/2025]:Raksha- PanelSync Inventor Add-in (v1)
using System;
using System.IO;
using System.Runtime.InteropServices;
using Inventor;
//[08/28/2025]:Raksha- Disambiguate System types
//[08/28/2025]:Raksha- Disambiguate System types from Inventor COM types
using SysEnv = System.Environment;
using IOPath = System.IO.Path;
using IOFile = System.IO.File;

namespace PanelSync.InventorAddin
{
    //[08/28/2025]:Raksha- Make the add-in class visible to COM and give it a stable GUID
    [ComVisible(true)]
    [Guid("B2C7C23E-18B0-4A11-9B0B-8C6B16E30F11")]
    public class AddInServer : ApplicationAddInServer
    {
        private Application _inv;
        private JobWatcher _watcher;

        public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
        {
            _inv = addInSiteObject.Application;

            var desktop = SysEnv.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory);
            var jobsDir = IOPath.Combine(desktop, "PanelSyncHot", "Jobs");
            var logsDir = IOPath.Combine(desktop, "PanelSyncHot", "logs");
            Directory.CreateDirectory(jobsDir);
            Directory.CreateDirectory(logsDir);

            var logPath = IOPath.Combine(logsDir, "inventor-addin.log");
            var logger = new SimpleFileLogger(logPath);

            _watcher = new JobWatcher(_inv, jobsDir, logger);
            logger.Info("//[08/28/2025]:Raksha- Add-in activated.");
        }

        public void Deactivate()
        {
            try { if (_watcher != null) _watcher.Dispose(); } catch { }
            _inv = null;
            _watcher = null;
        }

        public void ExecuteCommand(int commandID) { }
        public object Automation { get { return null; } }
    }
}
