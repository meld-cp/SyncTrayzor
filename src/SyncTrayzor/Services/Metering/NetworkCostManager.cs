using System;
using System.Net;
using System.Threading.Tasks;
using Windows.Networking.Connectivity;
using NLog;

namespace SyncTrayzor.Services.Metering
{
    public interface INetworkCostManager
    {
        bool IsSupported { get; }

        // TODO: Unused
        event EventHandler NetworkCostsChanged;
        event EventHandler NetworksChanged;

        Task<bool> IsConnectionMetered(IPAddress address);
    }

    public class NetworkCostManager : INetworkCostManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public bool IsSupported => true;

        public event EventHandler NetworkCostsChanged;
        public event EventHandler NetworksChanged;

        public NetworkCostManager()
        {
            NetworkInformation.NetworkStatusChanged += NetworkStatusChanged;
        }

        public async Task<bool> IsConnectionMetered(IPAddress address)
        {
            var profile = await NetworkUtils.GetNetworkProfileForIpAddress(address);
            if (profile == null)
            {
                return false;
            }

            var cost = profile.GetConnectionCost();
            // A network is "metered" if it is not unrestricted or unknown:
            return cost.NetworkCostType != NetworkCostType.Unrestricted && cost.NetworkCostType != NetworkCostType.Unknown;
        }

        private void NetworkStatusChanged(object sender)
        {
            NetworksChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}