using System;

namespace PanelSync.Core.Models
{
    public sealed class PanelMeta
    {
        public string ProjectId { get; set; } = "";
        public string PanelId { get; set; } = "";
        public string Rev { get; set; } = "A";
        public string Author { get; set; } = "";
        public string Units { get; set; } = "mm";
        public SourceInfo Source { get; set; } = new SourceInfo();
        public ModelInfo Model { get; set; } = new ModelInfo();
        public double[] BBoxMin { get; set; } = new double[3];
        public double[] BBoxMax { get; set; } = new double[3];
        public string Notes { get; set; } = "";
        public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;
    }
    public sealed class SourceInfo { public string App { get; set; } = ""; public string Version { get; set; } = ""; }
    public sealed class ModelInfo { public string File { get; set; } = ""; public string Hash { get; set; } = ""; }
}
