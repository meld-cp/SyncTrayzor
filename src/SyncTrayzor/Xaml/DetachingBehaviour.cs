using System;
using System.Windows;
using System.Windows.Data;
using Microsoft.Xaml.Behaviors;

namespace SyncTrayzor.Xaml
{
    // Adapted from http://dotnetbyexample.blogspot.co.uk/2011/04/safe-event-detachment-pattern-for.html
    public abstract class DetachingBehaviour<T> : Behavior<T> where T : FrameworkElement
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Initialized += AssociatedObjectInitialized;
            AssociatedObject.Unloaded += AssociatedObjectUnloaded;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            Cleanup();
        }

        private bool isCleanedUp;
        private void Cleanup()
        {
            if (!isCleanedUp)
            {
                isCleanedUp = true;

                AssociatedObject.Initialized -= AssociatedObjectInitialized;
                AssociatedObject.Unloaded -= AssociatedObjectUnloaded;
                BindingOperations.ClearAllBindings(this); // This was a surprise...
                DetachHandlers();
            }
        }

        private void AssociatedObjectInitialized(object sender, EventArgs e)
        {
            AttachHandlers();
        }

        private void AssociatedObjectUnloaded(object sender, RoutedEventArgs e)
        {
            Cleanup();
        }

        protected virtual void AttachHandlers() { }
        protected virtual void DetachHandlers() { }
    }
}
