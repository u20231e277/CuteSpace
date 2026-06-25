using CuteSpace.Models;
using Microsoft.Win32;

namespace CuteSpace.Services;

public sealed class AppDiscoveryService
{
    public Task<IReadOnlyList<InstalledAppOption>> FindInstalledAppsAsync()
    {
        return Task.Run<IReadOnlyList<InstalledAppOption>>(() =>
        {
            var results = new Dictionary<string, InstalledAppOption>(StringComparer.OrdinalIgnoreCase);
            AddStartMenuApps(results);
            AddRegistryApps(results);
            return results.Values
                .Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.Target))
                .OrderBy(x => x.Name)
                .Take(500)
                .ToList();
        });
    }

    private static void AddStartMenuApps(Dictionary<string, InstalledAppOption> results)
    {
        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        };

        foreach (var folder in folders.Where(Directory.Exists))
        {
            List<string> shortcuts;
            try
            {
                shortcuts = Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories).ToList();
            }
            catch (Exception ex)
            {
                SafeLog.Write(nameof(AppDiscoveryService), ex.ToString());
                continue;
            }

            foreach (var shortcut in shortcuts)
            {
                var name = Path.GetFileNameWithoutExtension(shortcut);
                results.TryAdd(shortcut, new InstalledAppOption
                {
                    Name = name,
                    Target = shortcut,
                    IconPath = shortcut,
                    Source = "Inicio"
                });
            }
        }
    }

    private static void AddRegistryApps(Dictionary<string, InstalledAppOption> results)
    {
        var roots = new[]
        {
            (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall")
        };

        foreach (var (root, path) in roots)
        {
            using var key = root.OpenSubKey(path);
            if (key is null)
            {
                continue;
            }

            foreach (var name in key.GetSubKeyNames())
            {
                using var appKey = key.OpenSubKey(name);
                var displayName = appKey?.GetValue("DisplayName")?.ToString();
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                var target = PickTarget(appKey);
                if (string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                results.TryAdd(displayName, new InstalledAppOption
                {
                    Name = displayName,
                    Target = target,
                    IconPath = appKey?.GetValue("DisplayIcon")?.ToString(),
                    Source = "Instalado"
                });
            }
        }
    }

    private static string PickTarget(RegistryKey? appKey)
    {
        var displayIcon = appKey?.GetValue("DisplayIcon")?.ToString() ?? "";
        var installLocation = appKey?.GetValue("InstallLocation")?.ToString() ?? "";

        var exeFromIcon = CleanExecutablePath(displayIcon);
        if (File.Exists(exeFromIcon))
        {
            return exeFromIcon;
        }

        if (Directory.Exists(installLocation))
        {
            try
            {
                var exe = Directory.EnumerateFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly)
                    .OrderBy(x => x.Length)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(exe))
                {
                    return exe;
                }
            }
            catch (Exception ex)
            {
                SafeLog.Write(nameof(AppDiscoveryService), ex.ToString());
            }
        }

        return "";
    }

    private static string CleanExecutablePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var cleaned = value.Trim().Trim('"');
        var exeIndex = cleaned.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exeIndex >= 0 ? cleaned[..(exeIndex + 4)] : cleaned;
    }
}
