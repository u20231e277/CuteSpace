using System.Text.Json;

namespace CuteSpace.Services;

public sealed class LocalizationService
{
    private Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);

    public string CurrentLanguage { get; private set; } = "es";

    public IReadOnlyDictionary<string, string> SupportedLanguages { get; } = new Dictionary<string, string>
    {
        ["es"] = "Español",
        ["en"] = "English",
        ["pt"] = "Português",
        ["fr"] = "Français",
        ["de"] = "Deutsch",
        ["it"] = "Italiano",
        ["ja"] = "日本語"
    };

    public string this[string key] => _strings.TryGetValue(key, out var value) ? value : key;

    public async Task LoadAsync(string languageCode)
    {
        CurrentLanguage = SupportedLanguages.ContainsKey(languageCode) ? languageCode : "es";
        _strings = await LoadFileAsync("es");

        if (CurrentLanguage != "es")
        {
            foreach (var pair in await LoadFileAsync(CurrentLanguage))
            {
                _strings[pair.Key] = pair.Value;
            }
        }
    }

    private static async Task<Dictionary<string, string>> LoadFileAsync(string languageCode)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", "Languages", $"{languageCode}.json");
            if (!File.Exists(path))
            {
                path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Resources", "Languages", $"{languageCode}.json");
            }

            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream)
                   ?? new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            SafeLog.Write(nameof(LocalizationService), ex.ToString());
            return new Dictionary<string, string>();
        }
    }
}
