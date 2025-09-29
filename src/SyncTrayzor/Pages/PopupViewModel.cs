namespace SyncTrayzor.Pages
{
    using System.Windows;

    using Stylet;

    using SyncTrayzor.Pages.Tray;

    public partial class PopupViewModel : Screen
    {

        private const int PopupOffsetX = -80;

        public FileTransfersTrayViewModel FileTransfersViewModel { get; private set; }

        public PopupViewModel(FileTransfersTrayViewModel fileTransfersViewModel)
        {
            FileTransfersViewModel = fileTransfersViewModel;
            FileTransfersViewModel.ShowTitle = false;
            FileTransfersViewModel.ActivateWith(this);
            FileTransfersViewModel.DeactivateWith(this);

            DisplayName = "SyncTrayzor";
            Activated += PopupViewModel_Activated;
        }

        private void PopupViewModel_Activated(object sender, ActivationEventArgs e)
        {
            var mousePos = WpfScreenHelper.MouseHelper.MousePosition;
            SetViewStartPosition(mousePos);
        }

        private void SetViewStartPosition(Point popupPos)
        {

            if (View is not Window w)
            {
                return;
            }

            var popupScreen = WpfScreenHelper.Screen.FromPoint(popupPos);

            // horizontal position
            var left = popupPos.X + PopupOffsetX;
            var right = left + w.Width;

            if (right > popupScreen.Bounds.Right)
            {
                left = popupScreen.Bounds.Right - w.Width;
            }
            w.Left = left;

            // vertical position
            var taskbarHeight = SystemParameters.PrimaryScreenHeight - SystemParameters.WorkArea.Height;
            var top = popupScreen.Bounds.Bottom - taskbarHeight - w.Height;

            w.Top = top;

        }
    }
}
