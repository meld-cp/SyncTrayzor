using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Timers;

namespace SyncTrayzor.Services
{
    public class DirectoryChangedEventArgs : EventArgs
    {
        public string DirectoryPath { get; }
        public string SubPath { get; }

        public DirectoryChangedEventArgs(string directoryPath, string subPath)
        {
            DirectoryPath = directoryPath;
            SubPath = subPath;
        }
    }

    public class PreviewDirectoryChangedEventArgs : DirectoryChangedEventArgs
    {
        public bool Cancel { get; set; }

        public bool PathExists { get; }

        public PreviewDirectoryChangedEventArgs(string directoryPath, string subPath, bool pathExists)
            : base(directoryPath, subPath)
        {
            PathExists = pathExists;
        }
    }

    public interface IDirectoryWatcherFactory
    {
        DirectoryWatcher Create(string directory, TimeSpan backoffInterval, TimeSpan existenceCheckingInterval);
    }

    public class DirectoryWatcherFactory : IDirectoryWatcherFactory
    {
        private readonly IFilesystemProvider filesystem;

        public DirectoryWatcherFactory(IFilesystemProvider filesystem)
        {
            this.filesystem = filesystem;
        }

        public DirectoryWatcher Create(string directory, TimeSpan backoffInterval, TimeSpan existenceCheckingInterval)
        {
            return new DirectoryWatcher(filesystem, directory, backoffInterval, existenceCheckingInterval);
        }
    }

    public class DirectoryWatcher : FileWatcher
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly Timer backoffTimer;

        private readonly object currentNotifyingSubPathLock = new();
        private string currentNotifyingSubPath;

        public event EventHandler<PreviewDirectoryChangedEventArgs> PreviewDirectoryChanged;
        public event EventHandler<DirectoryChangedEventArgs> DirectoryChanged;

        public DirectoryWatcher(IFilesystemProvider filesystem, string directory, TimeSpan backoffInterval, TimeSpan existenceCheckingInterval)
            : base(filesystem, FileWatcherMode.All, directory, existenceCheckingInterval)
        {
            if (backoffInterval.Ticks < 0)
                throw new ArgumentException("backoffInterval must be >= 0");

            backoffTimer = new Timer()
            {
                AutoReset = false,
                Interval = backoffInterval.TotalMilliseconds,
            };
            backoffTimer.Elapsed += (o, e) =>
            {
                string currentNotifyingSubPath;
                lock (currentNotifyingSubPathLock)
                {
                    currentNotifyingSubPath = this.currentNotifyingSubPath;
                    this.currentNotifyingSubPath = null;
                }
                OnDirectoryChanged(currentNotifyingSubPath);
            };
        }

        public override void OnPathChanged(string subPath, bool pathExists)
        {
            base.OnPathChanged(subPath, pathExists);

            if (OnPreviewDirectoryChanged(subPath, pathExists))
                return;

            backoffTimer.Stop();
            lock (currentNotifyingSubPathLock)
            {
                if (currentNotifyingSubPath == null)
                    currentNotifyingSubPath = subPath;
                else
                    currentNotifyingSubPath = FindCommonPrefix(currentNotifyingSubPath, subPath);
            }

            backoffTimer.Start();
        }

        private string FindCommonPrefix(string path1, string path2)
        {
            // Easy...
            if (path1 == path2)
                return path1;

            var parts1 = path1.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            var parts2 = path2.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            var result = new List<string>();
            for (int i = 0; i < Math.Min(parts1.Length, parts2.Length); i++)
            {
                if (!String.Equals(parts1[i], parts2[i], StringComparison.OrdinalIgnoreCase))
                    break;

                result.Add(parts1[i]);
            }

            return String.Join(Path.DirectorySeparatorChar.ToString(), result);
        }

        // Return true to cancel
        private bool OnPreviewDirectoryChanged(string subPath, bool pathExists)
        {
            var handler = PreviewDirectoryChanged;
            if (handler != null)
            {
                var ea = new PreviewDirectoryChangedEventArgs(Directory, subPath, pathExists);
                handler(this, ea);
                logger.Trace("PreviewDirectoryChanged with path {0}. Cancelled: {1}", Path.Combine(Directory, subPath), ea.Cancel);
                return ea.Cancel;
            }
            return false;
        }

        private void OnDirectoryChanged(string subPath)
        {
            logger.Debug("Path Changed: {0}", Path.Combine(Directory, subPath));
            DirectoryChanged?.Invoke(this, new DirectoryChangedEventArgs(Directory, subPath));
        }

        public override void Dispose()
        {
            base.Dispose();

            backoffTimer.Stop();
            backoffTimer.Dispose();
        }
    }
}
