using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace SyncTrayzor.Utils
{
    public class ObservableQueue<T> : IEnumerable<T>, INotifyCollectionChanged
    {
        private readonly Queue<T> queue = new();

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public int Count => queue.Count;

        public void Enqueue(T item)
        {
            queue.Enqueue(item);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, queue.Count - 1));
        }

        public T Dequeue()
        {
            T item = queue.Dequeue();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, 0));
            return item;
        }

        private void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            CollectionChanged?.Invoke(this, e);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return queue.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return queue.GetEnumerator();
        }
    }
}
