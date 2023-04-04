using System;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Runtime;

namespace RocketGPSTracker
{
    public class MyScanCallback : ScanCallback
    {
        private readonly Action<BluetoothDevice> _onDeviceFound;

        public MyScanCallback(Action<BluetoothDevice> onDeviceFound)
        {
            _onDeviceFound = onDeviceFound;
        }

        public override void OnScanResult([GeneratedEnum] ScanCallbackType callbackType, ScanResult result)
        {
            base.OnScanResult(callbackType, result);

            BluetoothDevice device = result.Device;
            if (device.Name != null && device.Name.Contains("EPS32_GPS")) // Replace with your device name
            {
                _onDeviceFound?.Invoke(device);
            }
        }
    }
}