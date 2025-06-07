using Stylet;
using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace SyncTrayzor.Xaml
{
    public class PopupConductorBehaviour : DetachingBehaviour<Popup>
    {
        public object DataContext
        {
            get => GetValue(DataContextProperty);
            set => SetValue(DataContextProperty, value);
        }

        public static readonly DependencyProperty DataContextProperty =
            DependencyProperty.Register("DataContext", typeof(object), typeof(PopupConductorBehaviour), new PropertyMetadata(null));

        protected override void AttachHandlers()
        {
            AssociatedObject.Opened += Opened;
            AssociatedObject.Closed += Closed;
            AssociatedObject.PreviewMouseUp += ManualClose;
        }

        protected override void DetachHandlers()
        {
            AssociatedObject.Opened -= Opened;
            AssociatedObject.Closed -= Closed;
            AssociatedObject.PreviewMouseUp -= ManualClose;
        }

        private void Opened(object sender, EventArgs e)
        {
            if (DataContext is IScreenState screenState)
                screenState.Activate();
        }

        private void Closed(object sender, EventArgs e)
        {
            if (DataContext is IScreenState screenState)
                screenState.Close();
        }

        private void ManualClose(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                AssociatedObject.IsOpen = false;
            }
        }
    }
}
