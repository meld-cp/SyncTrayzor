using System;
using System.Windows.Controls;

namespace SyncTrayzor.Xaml
{
    public class ScrollToEndBehaviour : DetachingBehaviour<TextBox>
    {
        protected override void AttachHandlers()
        {
            AssociatedObject.TextChanged += SomethingChanged;
            AssociatedObject.SizeChanged += SomethingChanged;

            AssociatedObject.ScrollToEnd();
        }

        protected override void DetachHandlers()
        {
            AssociatedObject.TextChanged -= SomethingChanged;
            AssociatedObject.SizeChanged -= SomethingChanged;
        }

        private void SomethingChanged(object sender, EventArgs e)
        {
            AssociatedObject.ScrollToEnd();
        }
    }
}
