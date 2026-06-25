namespace CuteSpace.Models;

public sealed class InstalledAppOption
{
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public string? IconPath { get; set; }
    public string Source { get; set; } = "";

    public override string ToString() => string.IsNullOrWhiteSpace(Source) ? Name : $"{Name} - {Source}";
}
