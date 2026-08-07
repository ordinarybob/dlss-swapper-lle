using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace DLSS_Swapper;

internal static class AppDataReset
{
    const string ResetArgument = "--reset-all-local-app-data";
    const string CredentialResource = "DLSS Swapper";
    const string CredentialUserName = "proxy";

    internal static void RunHelperIfRequested()
    {
        var arguments = Environment.GetCommandLineArgs();
        if (arguments.Length != 3 ||
            string.Equals(arguments[1], ResetArgument, StringComparison.Ordinal) == false)
        {
            return;
        }

        try
        {
            if (int.TryParse(arguments[2], NumberStyles.None, CultureInfo.InvariantCulture, out var parentProcessId) == false)
            {
                throw new InvalidOperationException("The reset request did not contain a valid parent process ID.");
            }

            WaitForParentToExit(parentProcessId);

            ClearProxyCredentials();

            var storagePath = GetExpectedStoragePath();
            ValidateExactPath(storagePath, GetExpectedStoragePath(), "application data");
            DeleteDirectoryWithRetries(storagePath);

#if !PORTABLE
            var tempPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DLSS Swapper"));
            ValidateExactPath(tempPath, Path.Combine(Path.GetTempPath(), "DLSS Swapper"), "temporary data");
            DeleteDirectoryWithRetries(tempPath);
#endif

            StartApplication();
        }
        catch (Exception ex)
        {
            ShowResetError(ex.Message);
        }

        Environment.Exit(0);
    }

    internal static bool TryStart(out string? errorMessage)
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new InvalidOperationException("The application executable could not be located.");
            }

            var startInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory,
            };
            startInfo.ArgumentList.Add(ResetArgument);
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));

            if (Process.Start(startInfo) is null)
            {
                throw new InvalidOperationException("The reset process could not be started.");
            }

            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unable to start the local app data reset.");
            errorMessage = ex.Message;
            return false;
        }
    }

    static void WaitForParentToExit(int parentProcessId)
    {
        try
        {
            using var parentProcess = Process.GetProcessById(parentProcessId);
            if (parentProcess.WaitForExit(TimeSpan.FromSeconds(60)) == false)
            {
                throw new TimeoutException("DLSS Swapper did not close within 60 seconds. No app data was deleted.");
            }
        }
        catch (ArgumentException)
        {
            // The process exited before the reset helper opened it.
        }
    }

    static string GetExpectedStoragePath()
    {
#if PORTABLE && DEBUG
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "StoredData", "DEBUG"));
#elif PORTABLE
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "StoredData"));
#elif DEBUG
        return Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DLSS Swapper", "DEBUG"));
#else
        return Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DLSS Swapper"));
#endif
    }

    static void ValidateExactPath(string actualPath, string expectedPath, string description)
    {
        var normalizedActualPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(actualPath));
        var normalizedExpectedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(expectedPath));
        if (string.Equals(normalizedActualPath, normalizedExpectedPath, StringComparison.OrdinalIgnoreCase) == false)
        {
            throw new InvalidOperationException($"Refusing to delete an unexpected {description} path: {normalizedActualPath}");
        }
    }

    static void DeleteDirectoryWithRetries(string path)
    {
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                return;
            }
            catch (IOException) when (attempt < 10)
            {
                Thread.Sleep(250);
            }
            catch (UnauthorizedAccessException) when (attempt < 10)
            {
                Thread.Sleep(250);
            }
        }
    }

    static void ClearProxyCredentials()
    {
        try
        {
            var vault = new Windows.Security.Credentials.PasswordVault();
            var credentials = vault.Retrieve(CredentialResource, CredentialUserName);
            vault.Remove(credentials);
        }
        catch (COMException ex) when (ex.HResult == -2147023728)
        {
            // The credential does not exist.
        }
    }

    static void StartApplication()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("The application executable could not be located for restart.");
        }

        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        Process.Start(startInfo);
    }

    static void ShowResetError(string errorMessage)
    {
        MessageBox(
            IntPtr.Zero,
            $"DLSS Swapper could not completely reset its local app data.\n\n{errorMessage}",
            "Reset local app data failed",
            0x00000010);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
