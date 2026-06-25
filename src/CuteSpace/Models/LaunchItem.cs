namespace CuteSpace.Models;

public sealed class LaunchItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public LaunchItemType Type { get; set; } = LaunchItemType.App;
    public string Target { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string IconGlyph { get; set; } = "📦";
    public string? BrowserExecutablePath { get; set; }
    public string? IconFileName { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string? FullIconPath => IconFileName != null
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CuteSpace", "icons", IconFileName)
        : null;

    [System.Text.Json.Serialization.JsonIgnore]
    public Microsoft.UI.Xaml.Visibility GlyphVisibility => string.IsNullOrEmpty(IconFileName)
        ? Microsoft.UI.Xaml.Visibility.Visible
        : Microsoft.UI.Xaml.Visibility.Collapsed;

    [System.Text.Json.Serialization.JsonIgnore]
    public Microsoft.UI.Xaml.Visibility ImageVisibility => string.IsNullOrEmpty(IconFileName)
        ? Microsoft.UI.Xaml.Visibility.Collapsed
        : Microsoft.UI.Xaml.Visibility.Visible;

    public int Order { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayTargetText { get; set; } = "";

    [System.Text.Json.Serialization.JsonIgnore]
    public string TypeLabelText { get; set; } = "";

    [System.Text.Json.Serialization.JsonIgnore]
    public int UrlTabCount => Type == LaunchItemType.Url
        ? Target.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length
        : 0;

    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayTarget => Type == LaunchItemType.Url && UrlTabCount > 1
        ? $"{UrlTabCount} tabs"
        : Target;

    [System.Text.Json.Serialization.JsonIgnore]
    public string TypeLabel => Type switch
    {
        LaunchItemType.App => "App",
        LaunchItemType.File => "File",
        LaunchItemType.Folder => "Folder",
        LaunchItemType.Url => "Web",
        LaunchItemType.WindowsSetting => "Windows",
        LaunchItemType.Tool => "Tool",
        _ => "Item"
    };
}
