using Stylet;
using System;

namespace SyncTrayzor.Pages
{
    public class NewVersionAlertViewModel : Screen
    {
        public bool CanInstall { get; set; }
        public Version Version { get; set; }
        public string Changelog { get; set; }
        public bool ShowUacBadge { get; set; }

        public bool DontRemindMe { get; private set; }

        public void Download()
        {
            RequestClose(true);
        }

        public void Install()
        {
            RequestClose(true);
        }

        public void RemindLater()
        {
            RequestClose(false);
        }

        public void DontRemind()
        {
            DontRemindMe = true;
            RequestClose(false);
        }
    }
}
