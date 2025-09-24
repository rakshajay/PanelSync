//[09/23/2025]:Raksha- Inventor entrypoint: sets up JobWatcher over hot-folder Jobs.
using System;
using System.IO;
using System.Runtime.InteropServices;
using Inventor;
using PanelSync.Core.Logging;

namespace PanelSync.InventorAddIn
{
	[ComVisible(true)]
	[Guid("B2C7C23E-18B0-4A11-9B0B-8C6B16E30F11")]
	public class AddInServer : ApplicationAddInServer
	{
		private Application _inv;
		private JobWatcher _watcher;

		public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
		{
			_inv = addInSiteObject.Application;

			//[09/23/2025]:Raksha- Desktop\PanelSyncHot\Jobs, \logs as single source of truth
			var desktop = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "OneDrive", "Desktop");
			var jobsDir = System.IO.Path.Combine(desktop, "PanelSyncHot", "Jobs");
			var logsDir = System.IO.Path.Combine(desktop, "PanelSyncHot", "logs");
			Directory.CreateDirectory(jobsDir);
			Directory.CreateDirectory(logsDir);

			var logPath = System.IO.Path.Combine(logsDir, "inventor-addin.log");
			var logger = new SimpleFileLogger(logPath);

			_watcher = new JobWatcher(_inv, logger, jobsDir);
			//logger.Info("Add-in activated");
			//logger.Info("Desktop path => " + desktop);
		}

		public void Deactivate()
		{
			try { _watcher?.Dispose(); } catch { }
			_inv = null; _watcher = null;
		}

		public void ExecuteCommand(int commandID) { }
		public object Automation { get { return null; } }
	}
}
