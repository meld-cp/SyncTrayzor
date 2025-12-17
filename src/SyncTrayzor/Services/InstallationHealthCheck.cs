using System;
using System.IO;
using Stylet;

namespace SyncTrayzor.Services;

public interface IInstallationHealthCheck
{
    void CheckOutdatedV1Installation();
}

public class InstallationHealthCheck(IWindowManager windowManager) : IInstallationHealthCheck
{
    public void CheckOutdatedV1Installation()
    {
        var programDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var v1File = Path.Combine(programDirectory, "Pri.LongPath.dll");
        if (File.Exists(v1File))
        {
            windowManager.ShowMessageBox(
                "It looks like this SyncTrayzor installation contains files from SyncTrayzor v1. This will likely cause SyncTrayzor to crash. Please uninstall and re-install SyncTrayzor to remove v1 installation files. If you are running the portable version, you can move the \"data\" folder from your existing install to keep user data.",
                "Mixed v1/v2 Installation Detected", System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }
}