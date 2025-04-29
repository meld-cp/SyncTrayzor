using SyncTrayzor.Syncthing.ApiClient;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SyncTrayzor.Syncthing
{
    public class ConnectionStatsChangedEventArgs : EventArgs
    {
        public SyncthingConnectionStats TotalConnectionStats { get; }

        public ConnectionStatsChangedEventArgs(SyncthingConnectionStats totalConnectionStats)
        {
            TotalConnectionStats = totalConnectionStats;
        }
    }

    public interface ISyncthingConnectionsWatcher : ISyncthingPoller
    {
        event EventHandler<ConnectionStatsChangedEventArgs> TotalConnectionStatsChanged;
    }

    public class SyncthingConnectionsWatcher : SyncthingPoller, ISyncthingConnectionsWatcher
    {
        private readonly SynchronizedTransientWrapper<ISyncthingApiClient> apiClientWrapper;
        private ISyncthingApiClient apiClient;
        
        private DateTime lastPollCompletion;
        private Connections prevConnections;

        public event EventHandler<ConnectionStatsChangedEventArgs> TotalConnectionStatsChanged;

        public SyncthingConnectionsWatcher(SynchronizedTransientWrapper<ISyncthingApiClient> apiClient)
            : base(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10))
        {
            apiClientWrapper = apiClient;
        }

        protected override void OnStart()
        {
            apiClient = apiClientWrapper.Value;
            prevConnections = null;
        }

        protected override void OnStop()
        {
            apiClient = null;

            // Send an update with zero transfer rate, since that's what we're now doing
            Update(prevConnections);
        }

        protected override async Task PollAsync(CancellationToken cancellationToken)
        {
            var connections = await apiClient.FetchConnectionsAsync(cancellationToken);

            // We can be stopped in the time it takes this to complete
            cancellationToken.ThrowIfCancellationRequested();

            Update(connections);
        }

        private void Update(Connections connections)
        {
            var elapsed = DateTime.UtcNow - lastPollCompletion;
            lastPollCompletion = DateTime.UtcNow;

            if (prevConnections != null)
            {
                // Just do the total for now
                var total = connections.Total;
                var prevTotal = prevConnections.Total;

                double inBytesPerSecond = (total.InBytesTotal - prevTotal.InBytesTotal) / elapsed.TotalSeconds;
                double outBytesPerSecond = (total.OutBytesTotal - prevTotal.OutBytesTotal) / elapsed.TotalSeconds;

                var totalStats = new SyncthingConnectionStats(total.InBytesTotal, total.OutBytesTotal, inBytesPerSecond, outBytesPerSecond);
                OnTotalConnectionStatsChanged(totalStats);
            }
            prevConnections = connections;
        }

        private void OnTotalConnectionStatsChanged(SyncthingConnectionStats connectionStats)
        {
            TotalConnectionStatsChanged?.Invoke(this, new ConnectionStatsChangedEventArgs(connectionStats));
        }
    }
}
