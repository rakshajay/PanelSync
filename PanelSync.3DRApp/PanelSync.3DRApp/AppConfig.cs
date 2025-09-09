using System;
using System.IO;
using System.Text.Json;

namespace PanelSync._3DRApp
{
    internal sealed class AppConfig
    {
        public string ProjectId { get; set; } = Guid.NewGuid().ToString("N");
        public string Zone { get; set; } = "ZONEA";
        public string ThreeDRExportDxf { get; set; } = "";
        public string InventorExportObj { get; set; } = "";
        public string LogsRoot { get; set; } = "";
        public string ThreeDRFilePath { get; set; } = "";

        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PanelSync", "3DRApp", "appsettings.json");

        public static AppConfig Load()
        {
            try { if (File.Exists(ConfigPath)) return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath)) ?? new AppConfig(); }
            catch { }
            var cfg = new AppConfig();
            var (dxf, obj, logs, _) = HotFolders.EnsureDefaults();
            cfg.ThreeDRExportDxf = dxf;
            cfg.InventorExportObj = obj;
            cfg.LogsRoot = logs;
            return cfg;
        }

        public void Save()
        {
            var dir = Path.GetDirectoryName(ConfigPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
