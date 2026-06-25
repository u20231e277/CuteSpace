using System.Diagnostics;
using CuteSpace.Models;

namespace CuteSpace.Services;

public sealed class LaunchService
{
    public async Task LaunchModeAsync(ModeProfile mode, Action<string>? status = null)
    {
        foreach (var item in mode.Items.OrderBy(x => x.Order))
        {
            status?.Invoke(item.Name);
            await LaunchAsync(item);
            await Task.Delay(120);
        }
    }

    public Task LaunchAsync(LaunchItem item)
    {
        return Task.Run(() =>
        {
            try
            {
                foreach (var startInfo in BuildStartInfos(item))
                {
                    Process.Start(startInfo);
                }
            }
            catch (Exception ex)
            {
                SafeLog.Write(nameof(LaunchService), $"{item.Name}: {ex}");
            }
        });
    }

    private static IEnumerable<ProcessStartInfo> BuildStartInfos(LaunchItem item)
    {
        if (item.Type == LaunchItemType.Url)
        {
            return BuildUrlStartInfos(item);
        }

        return [BuildStartInfo(item)];
    }

    private static ProcessStartInfo BuildStartInfo(LaunchItem item)
    {
        if (item.Type is LaunchItemType.File or LaunchItemType.Folder or LaunchItemType.WindowsSetting)
        {
            return new ProcessStartInfo
            {
                FileName = item.Target,
                UseShellExecute = true
            };
        }

        var target = string.IsNullOrWhiteSpace(item.Target) ? item.Name : item.Target;
        return new ProcessStartInfo
        {
            FileName = target,
            Arguments = item.Arguments,
            WorkingDirectory = File.Exists(target) ? Path.GetDirectoryName(target) : null,
            UseShellExecute = !File.Exists(target)
        };
    }

    private static IEnumerable<ProcessStartInfo> BuildUrlStartInfos(LaunchItem item)
    {
        var urls = SplitUrls(item.Target).ToList();
        if (urls.Count == 0 && !string.IsNullOrWhiteSpace(item.Target))
        {
            urls.Add(NormalizeUrl(item.Target));
        }

        if (!string.IsNullOrWhiteSpace(item.BrowserExecutablePath) && File.Exists(item.BrowserExecutablePath))
        {
            var quotedUrls = string.Join(" ", urls.Select(Quote));
            var args = string.IsNullOrWhiteSpace(item.Arguments)
                ? quotedUrls
                : $"{item.Arguments} {quotedUrls}";

            return
            [
                new ProcessStartInfo
                {
                    FileName = item.BrowserExecutablePath,
                    Arguments = args,
                    WorkingDirectory = Path.GetDirectoryName(item.BrowserExecutablePath),
                    UseShellExecute = false
                }
            ];
        }

        return urls.Select(url => new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private static IEnumerable<string> SplitUrls(string value)
    {
        return value
            .Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeUrl)
            .Where(x => !string.IsNullOrWhiteSpace(x));
    }

    private static string NormalizeUrl(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Contains("://", StringComparison.Ordinal) ? trimmed : $"https://{trimmed}";
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}
