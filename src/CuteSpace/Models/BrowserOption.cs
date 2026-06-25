namespace CuteSpace.Models;

public sealed class BrowserOption
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Source { get; set; } = "";
    public string Icon { get; set; } = "🌐";

    public override string ToString() => string.IsNullOrWhiteSpace(Source) ? Name : $"{Icon} {Name} - {Source}";
}
