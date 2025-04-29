using System;

namespace SyncTrayzor.Syncthing
{
    public class TransientWrapperValueChangedEventArgs<T> : EventArgs
    {
        public T Value { get; }

        public TransientWrapperValueChangedEventArgs(T value)
        {
            Value = value;
        }
    }

    public class TransientWrapper<T> where T : class
    {
        public event EventHandler<TransientWrapperValueChangedEventArgs<T>> ValueCreated;
        public event EventHandler<TransientWrapperValueChangedEventArgs<T>> ValueDestroyed;

        protected T _value;
        public virtual T Value
        {
            get => _value;
            set
            {
                var oldValue = _value;
                _value = value;

                RaiseEvents(oldValue, value);
            }
        }

        public TransientWrapper()
        {
        }

        public TransientWrapper(T value)
        {
            Value = value;
        }

        protected void RaiseEvents(T oldValue, T newValue)
        {
            if (oldValue != null && newValue == null)
                OnValueDestroyed(oldValue);
            else if (oldValue == null && newValue != null)
                OnValueCreated(newValue);
        }

        private void OnValueCreated(T value)
        {
            ValueCreated?.Invoke(this, new TransientWrapperValueChangedEventArgs<T>(value));
        }

        private void OnValueDestroyed(T value)
        {
            ValueDestroyed?.Invoke(this, new TransientWrapperValueChangedEventArgs<T>(value));
        }
    }

    public class SynchronizedTransientWrapper<T> : TransientWrapper<T> where T : class
    {
        private readonly object _lockObject;
        public object LockObject => _lockObject;

        public override T Value
        {
            get
            {
                lock (_lockObject)
                {
                    return base.Value;
                }
            }
            set
            {
                T oldValue;
                lock (_lockObject)
                {
                    oldValue = _value;
                    _value = value;
                }

                RaiseEvents(oldValue, value);
            }
        }

        public T UnsynchronizedValue
        {
            get => base.Value;
            set => base.Value = value;
        }

        public SynchronizedTransientWrapper()
        {
            _lockObject = new object();
        }

        public SynchronizedTransientWrapper(object lockObject)
        {
            _lockObject = lockObject;
        }

        public SynchronizedTransientWrapper(T value)
        {
            _lockObject = new object();
            Value = value;
        }

        public SynchronizedTransientWrapper(T value, object lockObject)
        {
            _lockObject = lockObject;
            Value = value;
        }

        public T GetAsserted()
        {
            lock (_lockObject)
            {
                if (base.Value == null)
                    throw new InvalidOperationException("Synchronized value is null");

                return base.Value;
            }
        }
    }
}
