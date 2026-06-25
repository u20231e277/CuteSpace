namespace CuteSpace.Models;

public sealed class AppState
{
    public AppSettings Settings { get; set; } = new();
    public List<ModeProfile> Modes { get; set; } = [];
    public List<LaunchItem> Shortcuts { get; set; } = [];
    public List<ClipboardEntry> ClipboardHistory { get; set; } = [];
}
