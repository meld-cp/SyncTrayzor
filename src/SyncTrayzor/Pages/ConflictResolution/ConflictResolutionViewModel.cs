using Stylet;
using SyncTrayzor.Services.Conflicts;
using SyncTrayzor.Syncthing;
using System;
using System.Linq;
using System.Threading;
using System.Collections.Specialized;
using SyncTrayzor.Localization;
using System.Windows;
using SyncTrayzor.Properties;
using SyncTrayzor.Services;
using SyncTrayzor.Services.Config;
using System.Reactive.Linq;

namespace SyncTrayzor.Pages.ConflictResolution
{
    public class ConflictResolutionViewModel : Screen
    {
        private readonly ISyncthingManager syncthingManager;
        private readonly IConflictFileManager conflictFileManager;
        private readonly IProcessStartProvider processStartProvider;
        private readonly IConflictFileWatcher conflictFileWatcher;
        private readonly IWindowManager windowManager;
        private readonly IConfigurationProvider configurationProvider;
        private readonly Func<SingleConflictResolutionViewModel> singleConflictResolutionViewModelFactory;
        private readonly Func<MultipleConflictsResolutionViewModel> multipleConflictsResolutionViewModelFactory;

        private bool wasConflictFileWatcherEnabled;

        private CancellationTokenSource loadingCts { get; set; }

        public bool IsLoading => loadingCts != null;
        public BindableCollection<ConflictViewModel> Conflicts { get; } = new();
        public bool IsLoadingAndNoConflictsFound => IsLoading && Conflicts.Count == 0;
        public bool HasFinishedLoadingAndNoConflictsFound => !IsSyncthingStopped && !IsLoading && Conflicts.Count == 0;
        public bool IsSyncthingStopped { get; private set; }

        public IScreen ResolutionViewModel { get; private set; }

        public bool DeleteToRecycleBin { get; set; }

        public ConflictResolutionViewModel(
            ISyncthingManager syncthingManager,
            IConflictFileManager conflictFileManager,
            IProcessStartProvider processStartProvider,
            IConflictFileWatcher conflictFileWatcher,
            IWindowManager windowManager,
            IConfigurationProvider configurationProvider,
            Func<SingleConflictResolutionViewModel> singleConflictResolutionViewModelFactory,
            Func<MultipleConflictsResolutionViewModel> multipleConflictsResolutionViewModelFactory)
        {
            this.syncthingManager = syncthingManager;
            this.conflictFileManager = conflictFileManager;
            this.processStartProvider = processStartProvider;
            this.conflictFileWatcher = conflictFileWatcher;
            this.configurationProvider = configurationProvider;
            this.windowManager = windowManager;
            this.singleConflictResolutionViewModelFactory = singleConflictResolutionViewModelFactory;
            this.multipleConflictsResolutionViewModelFactory = multipleConflictsResolutionViewModelFactory;

            DeleteToRecycleBin = this.configurationProvider.Load().ConflictResolverDeletesToRecycleBin;
            this.Bind(s => s.DeleteToRecycleBin, (o, e) => this.configurationProvider.AtomicLoadAndSave(c => c.ConflictResolverDeletesToRecycleBin = e.NewValue));

            Conflicts.CollectionChanged += (o, e) =>
            {
                if ((e.Action == NotifyCollectionChangedAction.Add && (e.OldItems?.Count ?? 0) == 0) ||
                    (e.Action == NotifyCollectionChangedAction.Remove && (e.NewItems?.Count ?? 0) == 0) ||
                    (e.Action == NotifyCollectionChangedAction.Reset))
                {
                    NotifyOfPropertyChange(nameof(Conflicts));
                    NotifyOfPropertyChange(nameof(IsLoadingAndNoConflictsFound));
                    NotifyOfPropertyChange(nameof(HasFinishedLoadingAndNoConflictsFound));

                    if (!Conflicts.Any(x => x.IsSelected) && Conflicts.Count > 0)
                        Conflicts[0].IsSelected = true;
                }
            };
        }

        private void SyncthingDataLoaded(object sender, EventArgs e)
        {
            IsSyncthingStopped = false;
            Load();
        }

        protected override void OnInitialActivate()
        {
            // This is hacky
            wasConflictFileWatcherEnabled = conflictFileWatcher.IsEnabled;
            conflictFileWatcher.IsEnabled = false;

            if (syncthingManager.State != SyncthingState.Running || !syncthingManager.IsDataLoaded)
            {
                IsSyncthingStopped = true;
                syncthingManager.DataLoaded += SyncthingDataLoaded;
            }
            else
            {
                IsSyncthingStopped = false;
                Load();
            }
        }

        protected override void OnClose()
        {
            loadingCts?.Cancel();
            if (wasConflictFileWatcherEnabled)
                conflictFileWatcher.IsEnabled = true;
            syncthingManager.DataLoaded -= SyncthingDataLoaded;
        }

        private async void Load()
        {
            if (loadingCts != null)
            {
                loadingCts.Cancel();
                loadingCts = null;
            }

            loadingCts = new CancellationTokenSource();
            var ct = loadingCts.Token;
            try
            {
                Conflicts.Clear();
                foreach (var folder in syncthingManager.Folders.FetchAll())
                {
                    try
                    {
                        await conflictFileManager.FindConflicts(folder.Path)
                            .ObserveOn(SynchronizationContext.Current!)
                            .ForEachAsync(conflict => Conflicts.Add(new ConflictViewModel(conflict, folder.Label)), ct);
                    }
                    catch (OperationCanceledException) { }
                }
            }
            finally
            {
                loadingCts = null;
            }
        }

        public void Cancel()
        {
            loadingCts.Cancel();
        }

        public void ChooseOriginal(ConflictViewModel conflict)
        {
            if (!ResolveConflict(conflict.ConflictSet, conflict.ConflictSet.File.FilePath))
                return;

            // The conflict will no longer exist, so remove it
            Conflicts.Remove(conflict);
        }

        public void ChooseConflictFile(ConflictViewModel conflict, ConflictOptionViewModel conflictOption)
        {
            if (!ResolveConflict(conflict.ConflictSet, conflictOption.ConflictOption.FilePath))
                return;

            // The conflict will no longer exist, so remove it
            Conflicts.Remove(conflict);
        }

        private bool ResolveConflict(ConflictSet conflictSet, string filePath)
        {
            // This can happen e.g. if the file chosen no longer exists
            try
            {
                conflictFileManager.ResolveConflict(conflictSet, filePath, DeleteToRecycleBin);
                return true;
            }
            catch (Exception e)
            {
                // So far I've seen IOExeption (no longer exists) and UnauthorizedAccessException
                // Just in case there are any others, be pokemon
                windowManager.ShowMessageBox(
                    Localizer.F(Resources.ConflictResolutionView_Dialog_Failed_Message, e.Message),
                    Resources.ConflictResolutionView_Dialog_Failed_Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return false;
            }
        }

        public void SelectionChanged()
        {
            var selected = Conflicts.Where(x => x.IsSelected).ToList();
            if (selected.Count == 0)
            {
                ResolutionViewModel = null;
            }
            else if (selected.Count == 1)
            {
                var vm = singleConflictResolutionViewModelFactory();
                vm.Delegate = this;
                vm.Conflict = selected[0];
                ResolutionViewModel = vm;
            }
            else
            {
                var vm = multipleConflictsResolutionViewModelFactory();
                vm.Delegate = this;
                vm.Conflicts = selected;
                ResolutionViewModel = vm;
            }
        }

        public void ListViewDoubleClick(object sender, RoutedEventArgs e)
        {
            // Check that we were called on a row, not on a header
            if ((e.OriginalSource as FrameworkElement)?.DataContext is ConflictViewModel vm)
                ShowFileInFolder(vm);
        }

        public void ShowFileInFolder(ConflictViewModel conflict)
        {
            var filePath = conflict.Deleted ? conflict.ConflictSet.Conflicts.First().FilePath : conflict.FilePath;
            processStartProvider.ShowFileInExplorer(filePath);
        }

        public void Close()
        {
            RequestClose(true);
        }
    }
}
