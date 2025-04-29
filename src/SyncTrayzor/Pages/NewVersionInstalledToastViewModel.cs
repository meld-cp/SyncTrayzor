using Stylet;
using System;

namespace SyncTrayzor.Pages
{
    public class NewVersionInstalledToastViewModel : Screen
    {
        public Version Version { get; set; }
        public string VersionString => Version.ToString(3);
    }
}
