/*using Android.Bluetooth;
using Android.Content;
using Android.Widget;
using System;

namespace RocketGPSTracker
{
    public class BluetoothDeviceReceiver : BroadcastReceiver
    {
        public delegate void DeviceFoundHandler(string deviceAddress);
        public event DeviceFoundHandler OnDeviceFound;

        public override void OnReceive(Context context, Intent intent)
        {
            string action = intent.Action;
            try
            {
                if (BluetoothDevice.ActionFound.Equals(action))
                {
                    BluetoothDevice device = (BluetoothDevice)intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);

                    if (device.Name == "ESP32_GPS") // Use the same name as in the ESP32 code
                    {
                        if (device.BondState != Bond.Bonded)
                        {
                            device.CreateBond();
                        }

                        // Trigger the event when the device is found
                        OnDeviceFound?.Invoke(device.Address);
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private bool AdapterContainsItem(ArrayAdapter<string> adapter, string item)
        {
            for (int i = 0; i < adapter.Count; i++)
            {
                if (adapter.GetItem(i) == item)
                {
                    return true;
                }
            }
            return false;
        }
    }
}*/