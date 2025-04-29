using Stylet;
using System.Collections.Generic;

namespace SyncTrayzor.Pages.BarAlerts
{
    public class PausedDevicesFromMeteringViewModel : Screen, IBarAlert
    {
        public AlertSeverity Severity => AlertSeverity.Info;

        public BindableCollection<string> PausedDeviceNames { get; } = new();

        public PausedDevicesFromMeteringViewModel(IEnumerable<string> pausedDeviceNames)
        {
            PausedDeviceNames.AddRange(pausedDeviceNames);
        }
    }
}
