using Stylet;
using SyncTrayzor.Pages;
using System;

namespace SyncTrayzor.Services
{
    public interface IApplicationWindowState : IDisposable
    {
        event EventHandler<ActivationEventArgs> RootWindowActivated;
        event EventHandler<DeactivationEventArgs> RootWindowDeactivated;
        event EventHandler<CloseEventArgs> RootWindowClosed;

        ScreenState ScreenState { get; }

        void Setup(ShellViewModel rootViewModel);

        void CloseToTray();
        void EnsureInForeground();
    }

    public class ApplicationWindowState : IApplicationWindowState
    {
        private ShellViewModel rootViewModel;

        public void Setup(ShellViewModel rootViewModel)
        {
            this.rootViewModel = rootViewModel;

            this.rootViewModel.Activated += OnRootWindowActivated;
            this.rootViewModel.Deactivated += OnRootWindowDeactivated;
            this.rootViewModel.Closed += OnRootWindowClosed;
        }

        public event EventHandler<ActivationEventArgs> RootWindowActivated;
        public event EventHandler<DeactivationEventArgs> RootWindowDeactivated;
        public event EventHandler<CloseEventArgs> RootWindowClosed;

        private void OnRootWindowActivated(object sender, ActivationEventArgs e)
        {
            RootWindowActivated?.Invoke(this, e);
        }

        private void OnRootWindowDeactivated(object sender, DeactivationEventArgs e)
        {
            RootWindowDeactivated?.Invoke(this, e);
        }

        private void OnRootWindowClosed(object sender, CloseEventArgs e)
        {
            RootWindowClosed?.Invoke(this, e);
        }

        public ScreenState ScreenState => rootViewModel.ScreenState;

        public void CloseToTray()
        {
            rootViewModel.CloseToTray();
        }

        public void EnsureInForeground()
        {
            rootViewModel.EnsureInForeground();
        }

        public void Dispose()
        {
            if (rootViewModel != null)
            {
                rootViewModel.Activated -= OnRootWindowActivated;
                rootViewModel.Deactivated -= OnRootWindowDeactivated;
                rootViewModel.Closed -= OnRootWindowClosed;
            }
        }
    }
}
