namespace CuteSpace.Models;

public sealed class AppSettings
{
    public string LanguageCode { get; set; } = "es";
    public string PinnedSection { get; set; } = "modes";
    public bool StartWithWindows { get; set; }
    public bool FirstRunCompleted { get; set; }
    public bool StyleFirstRunCompleted { get; set; }
    public bool PlayCuteSounds { get; set; } = true;
    public string VisualStyle { get; set; } = "cute";
    public string PetStyle { get; set; } = "classic";
    public int BubbleX { get; set; } = -1;
    public int BubbleY { get; set; } = -1;
    public int PanelWidth { get; set; } = 560;
    public int PanelHeight { get; set; } = 720;
    public bool BubbleHidden { get; set; }
    public string? StartupModeId { get; set; }
    public List<BrowserOption> SavedBrowsers { get; set; } = [];
}
