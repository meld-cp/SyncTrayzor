using NLog;
using SyncTrayzor.Utils;
using System;
using System.Diagnostics;
using System.Timers;

namespace SyncTrayzor.Services
{
    public class MemoryUsageLogger
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly TimeSpan pollInterval = TimeSpan.FromMinutes(5);

        private readonly Timer timer;

        public bool Enabled
        {
            get => timer.Enabled;
            set => timer.Enabled = value;
        }

        public MemoryUsageLogger()
        {
            timer = new Timer()
            {
                AutoReset = true,
                Interval = pollInterval.TotalMilliseconds,
            };
            timer.Elapsed += (o, e) =>
            {
                var process = Process.GetCurrentProcess();
                var workingSet = process.WorkingSet64;
                var privateBytes = process.PrivateMemorySize64;

                var gcInfo = GC.GetGCMemoryInfo();

                var heapSize = gcInfo.HeapSizeBytes;
                var fragmented = gcInfo.FragmentedBytes;
                var committed = gcInfo.TotalCommittedBytes;
                var pendingFinalizers = gcInfo.FinalizationPendingCount;

                // Number of collection runs since process start
                var gen0Collections = GC.CollectionCount(0);
                var gen1Collections = GC.CollectionCount(1);
                var gen2Collections = GC.CollectionCount(2);

                logger.Info(
                    "WS {WorkingSet} | Private {Private} | Heap {Heap} (frag {Frag}) | Commit {Commit} | Pending Finalizers {Pending} | GC collection counts 0:{Gen0} 1:{Gen1} 2:{Gen2}",
                    FormatUtils.BytesToHuman(workingSet),
                    FormatUtils.BytesToHuman(privateBytes),
                    FormatUtils.BytesToHuman(heapSize),
                    FormatUtils.BytesToHuman(fragmented),
                    FormatUtils.BytesToHuman(committed),
                    pendingFinalizers,
                    gen0Collections, gen1Collections, gen2Collections);
            };
        }
    }
}