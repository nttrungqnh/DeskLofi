using Microsoft.Win32;

namespace DeskLofi.Services;
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static void SetEnabled(bool enabled)
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey); if (enabled) key.SetValue("DeskLofi", $"\"{Environment.ProcessPath}\""); else key.DeleteValue("DeskLofi", false); } catch { }
    }
}
