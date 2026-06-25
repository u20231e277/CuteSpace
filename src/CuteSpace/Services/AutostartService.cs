using Microsoft.Win32;

namespace CuteSpace.Services;

public sealed class AutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CuteSpace";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return !string.IsNullOrWhiteSpace(key?.GetValue(ValueName)?.ToString());
        }
        catch (Exception ex)
        {
            SafeLog.Write(nameof(AutostartService), ex.ToString());
            return false;
        }
    }

    public bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, true);

            if (enabled)
            {
                key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }

            return true;
        }
        catch (Exception ex)
        {
            SafeLog.Write(nameof(AutostartService), ex.ToString());
            return false;
        }
    }
}
