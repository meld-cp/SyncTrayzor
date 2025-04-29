using System;
using System.Net;

namespace SyncTrayzor.Syncthing.Devices
{
    public class Device : IEquatable<Device>
    {
        private readonly object syncRoot = new();

        public string DeviceId { get; }

        private string _shortDeviceId;
        public string ShortDeviceId => _shortDeviceId ?? (_shortDeviceId = DeviceId.Substring(0, DeviceId.IndexOf('-')));

        public string Name { get; }

        public bool IsConnected
        {
            get { lock (syncRoot) { return _address != null; } }
        }

        private IPEndPoint _address;
        public IPEndPoint Address
        {
            get { lock (syncRoot) { return _address; } }
            private set { lock(syncRoot) { _address = value; } }
        }

        private bool _paused;
        public bool Paused
        {
            get { lock(syncRoot) { return _paused; } }
            private set { lock(syncRoot) { _paused = value; } }
        }

        public Device(string deviceId, string name)
        {
            DeviceId = deviceId;
            Name = name;
        }

        public void SetConnected(IPEndPoint address)
        {
            Address = address;
        }

        public void SetDisconnected()
        {
            Address = null;
        }

        public void SetPaused()
        {
            Paused = true;
        }

        public void SetResumed()
        {
            Paused = false;
        }

        public bool Equals(Device other)
        {
            if (ReferenceEquals(this, other))
                return true;
            if (ReferenceEquals(other, null))
                return false;

            return DeviceId == other.DeviceId;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Device);
        }

        public override int GetHashCode()
        {
            return DeviceId.GetHashCode();
        }
    }
}
