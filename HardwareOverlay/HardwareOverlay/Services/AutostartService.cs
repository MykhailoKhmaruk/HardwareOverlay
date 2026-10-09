using System.Diagnostics;

namespace HardwareOverlay.Services;

/// <summary>
/// Autostart via Task Scheduler (schtasks). The registry Run key won't work here:
/// the manifest requires requireAdministrator and Windows silently skips such
/// entries at logon. A task with /rl highest starts elevated without a UAC prompt.
/// </summary>
public static class AutostartService
{
    private const string TaskName = "HardwareOverlay_Autostart";

    public static void Apply(bool enabled)
    {
        try
        {
            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe))
                    return;

                // /f overwrites an existing task, keeping the exe path current.
                Run($"/create /tn \"{TaskName}\" /sc onlogon /rl highest /f /tr \"\\\"{exe}\\\"\"");
            }
            else
            {
                Run($"/delete /tn \"{TaskName}\" /f");
            }
        }
        catch
        {
            // Autostart isn't critical — swallow errors.
        }
    }

    private static void Run(string arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(info);
        process?.WaitForExit(5000);
    }
}
