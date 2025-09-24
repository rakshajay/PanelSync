namespace PanelSync.Core.Models
{
    //[09/14/2025]:Raksha- Configuration for LinkMap (paths, naming rules, axes).
    public sealed class LinkMap
    {
        public string ProjectId { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string Units { get; set; } = "mm";
        public Axes Axes { get; set; } = new Axes();
        public Paths Paths { get; set; } = new Paths();
        public NamingRules Naming { get; set; } = new NamingRules();
    }
    public sealed class Axes { public string Up { get; set; } = "Z"; public string Front { get; set; } = "Y"; }
    public sealed class Paths
    {
        public string ToInventorRefs { get; set; } = "";
        public string ToReshaperPanels { get; set; } = "";
        public string Archive { get; set; } = "";
        public string Logs { get; set; } = "";
    }
    public sealed class NamingRules
    {
        public string PanelModel { get; set; } = "{projectId}_{panelId}_{rev}_{stamp}.{ext}";
        public string PanelMeta { get; set; } = "{projectId}_{panelId}_{rev}_{stamp}_meta.json";
    }
}
