using System;
using System.Windows;
using System.Windows.Data;

namespace SyncTrayzor.Xaml
{
    public class NoSizeBelowScreenBehaviour : DetachingBehaviour<Window>
    {
        private const int taskbarHeight = 40; // Max height

        private bool haveSet = false;

        private static readonly DependencyProperty WindowLeftProperty =
            DependencyProperty.Register("WindowTop", typeof(double), typeof(NoSizeBelowScreenBehaviour), new PropertyMetadata(Double.NaN, (d, e) =>
            {
                ((NoSizeBelowScreenBehaviour)d).ScreenTopChanged((double)e.NewValue);
            }));

        private void ScreenTopChanged(double topValue)
        {
            // This is set twice in succession when loaded - the first time the height is NaN, the second time it isn't
            // We only want to set this once, one the second time that top is set

            if (!Double.IsNaN(AssociatedObject.Height) && !Double.IsNaN(topValue) && !haveSet)
            {
                AssociatedObject.MaxHeight = SystemParameters.VirtualScreenHeight - topValue - taskbarHeight;
                haveSet = true;
            }
        }

        protected override void AttachHandlers()
        {
            var topBinding = new Binding("WindowTop")
            {
                Source = this,
                Mode = BindingMode.TwoWay,
            };
            BindingOperations.SetBinding(AssociatedObject, Window.TopProperty, topBinding);
        }
    }
}
