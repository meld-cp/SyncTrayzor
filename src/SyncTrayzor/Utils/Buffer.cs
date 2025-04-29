using System;
using System.Collections.Generic;
using System.Threading;
using System.Timers;

namespace SyncTrayzor.Utils
{
    public class BufferDeliveredEventArgs<T>
    {
        public IEnumerable<T> Items { get; }

        public BufferDeliveredEventArgs (IEnumerable<T> items)
	    {
            Items = items;
	    }
    }

    public class Buffer<T>
    {
        private readonly object lockObject = new();
        private readonly SynchronizationContext synchronizationContext;
        private readonly TimeSpan maximumBackoff;
        private readonly System.Timers.Timer maximumBackoffTimer;
        private readonly System.Timers.Timer timer;

        public event EventHandler<BufferDeliveredEventArgs<T>> Delivered;
        
        private List<T> items;

        public Buffer(TimeSpan backoff, TimeSpan maximumBackoff, SynchronizationContext synchronizationContext = null)
        {
            this.synchronizationContext = synchronizationContext ?? SynchronizationContext.Current;
            this.maximumBackoff = maximumBackoff;

            maximumBackoffTimer = new System.Timers.Timer()
            {
                AutoReset = false,
                Interval = maximumBackoff.TotalMilliseconds,
            };
            maximumBackoffTimer.Elapsed += TimerElapsed;

            timer = new System.Timers.Timer()
            {
                AutoReset = false,
                Interval = backoff.TotalMilliseconds,
            };
            timer.Elapsed += TimerElapsed;

            items = new List<T>();
        }

        public void Add(T item)
        {
            lock (lockObject)
            {
                items.Add(item);

                timer.Stop();
                timer.Start();

                maximumBackoffTimer.Enabled = true;
            }
        }

        private void Deliver()
        {
            List<T> items;

            lock (lockObject)
            {
                // Early-exit in case nothing's been logged since the last timer
                if (this.items.Count == 0)
                    return;

                items = this.items;
                this.items = new List<T>();
            }

            var handler = Delivered;
            if (handler != null)
            {
                if (synchronizationContext == null)
                    handler(this, new BufferDeliveredEventArgs<T>(items));
                else
                    synchronizationContext.Post(_ => handler(this, new BufferDeliveredEventArgs<T>(items)), null);
            }
        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            Deliver();
        }
    }
}
