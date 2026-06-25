using System.Text.Json;
using CuteSpace.Models;

namespace CuteSpace.Services;

public sealed class JsonDataStore
{
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string DataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CuteSpace");

    private string DataPath => Path.Combine(DataFolder, "data.json");

    public async Task<AppState> LoadAsync()
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            if (!File.Exists(DataPath))
            {
                var seeded = SeedState();
                await SaveAsync(seeded);
                return seeded;
            }

            await using var stream = File.OpenRead(DataPath);
            return await JsonSerializer.DeserializeAsync<AppState>(stream, _options) ?? SeedState();
        }
        catch (Exception ex)
        {
            SafeLog.Write(nameof(JsonDataStore), ex.ToString());
            BackupBrokenDataFile();
            return SeedState();
        }
    }

    public async Task SaveAsync(AppState state)
    {
        await _saveGate.WaitAsync();
        try
        {
            Directory.CreateDirectory(DataFolder);
            var temp = Path.Combine(DataFolder, $"data.{Guid.NewGuid():N}.tmp");
            await using (var stream = File.Create(temp))
            {
                await JsonSerializer.SerializeAsync(stream, state, _options);
            }

            File.Move(temp, DataPath, true);
        }
        catch (Exception ex)
        {
            SafeLog.Write(nameof(JsonDataStore), ex.ToString());
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private void BackupBrokenDataFile()
    {
        try
        {
            if (File.Exists(DataPath))
            {
                File.Copy(DataPath, Path.Combine(DataFolder, $"data.broken.{DateTimeOffset.Now:yyyyMMddHHmmss}.json"));
            }
        }
        catch
        {
            // Best effort only.
        }
    }

    private static AppState SeedState()
    {
        return new AppState
        {
            Modes =
            [
                new ModeProfile
                {
                    Name = "Modo Trabajo",
                    IconGlyph = "💼",
                    Items =
                    [
                        new LaunchItem
                        {
                            Name = "Correo o dashboard",
                            Type = LaunchItemType.Url,
                            Target = "https://outlook.office.com",
                            IconGlyph = "✉️",
                            Order = 0
                        },
                        new LaunchItem
                        {
                            Name = "Calculadora",
                            Type = LaunchItemType.Tool,
                            Target = "calc.exe",
                            IconGlyph = "🧮",
                            Order = 1
                        }
                    ]
                },
                new ModeProfile
                {
                    Name = "Modo Juego",
                    IconGlyph = "🎮",
                    Items =
                    [
                        new LaunchItem
                        {
                            Name = "Playlist del navegador",
                            Type = LaunchItemType.Url,
                            Target = "https://music.youtube.com",
                            IconGlyph = "🎵",
                            Order = 0
                        }
                    ]
                }
            ],
            Shortcuts =
            [
                new LaunchItem
                {
                    Name = "Administrador de tareas",
                    Type = LaunchItemType.Tool,
                    Target = "taskmgr.exe",
                    IconGlyph = "⚙️",
                    Order = 0
                },
                new LaunchItem
                {
                    Name = "Configuracion de Windows",
                    Type = LaunchItemType.WindowsSetting,
                    Target = "ms-settings:",
                    IconGlyph = "🪟",
                    Order = 1
                }
            ]
        };
    }
}
