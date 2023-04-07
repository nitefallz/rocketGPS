using System;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Runtime;

namespace RocketGPSTracker
{
    public class MyScanCallback : ScanCallback
    {
        private readonly Action<BluetoothDevice> _onDeviceFound;
        private bool _deviceFound = false;
        public MyScanCallback(Action<BluetoothDevice> onDeviceFound)
        {
            _onDeviceFound = onDeviceFound;
        }
        
        public override void OnScanResult([GeneratedEnum] ScanCallbackType callbackType, ScanResult result)
        {
            try
            {
                base.OnScanResult(callbackType, result);

                BluetoothDevice device = result.Device;
                if (!_deviceFound && device.Name != null && device.Name.Contains("ESP32_GPS"))
                {
                    _deviceFound = true;
                    _onDeviceFound?.Invoke(device);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

    }
}