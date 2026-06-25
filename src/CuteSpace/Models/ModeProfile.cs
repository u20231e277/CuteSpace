namespace CuteSpace.Models;

public sealed class ModeProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string IconGlyph { get; set; } = "🌸";
    public List<LaunchItem> Items { get; set; } = [];

    public override string ToString() => Name;
}
