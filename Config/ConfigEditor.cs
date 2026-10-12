namespace RelayClient.Config;

using System.ComponentModel;
using System.Diagnostics;

public static class ConfigEditor
{
    public static void Open(string path)
    {
        ProcessStartInfo startInfo;
        if (OperatingSystem.IsWindows())
        {
            startInfo = new ProcessStartInfo(path) { UseShellExecute = true };
        }
        else
        {
            startInfo = new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open");
            if (OperatingSystem.IsMacOS())
                startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add(path);
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (!OperatingSystem.IsWindows())
            {
                if (process is null)
                    throw new InvalidOperationException("The default editor could not be started.");

                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException(
                        $"The default editor launcher exited with code {process.ExitCode}. Configure a default text editor for JSON files.");
            }
        }
        catch (Win32Exception ex) when (OperatingSystem.IsWindows() && ex.NativeErrorCode == 1155)
        {
            // Windows may not have a default application registered for JSON files.
            var fallback = new ProcessStartInfo("notepad.exe");
            fallback.ArgumentList.Add(path);
            using var process = Process.Start(fallback);
        }
    }
}
