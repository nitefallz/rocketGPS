using System;
using Android.Bluetooth;
using Android.Util;

namespace RocketGPSTracker
{
    public class MyGattCallback : BluetoothGattCallback
    {
        private readonly MainActivity _activity;
        
        public MyGattCallback(MainActivity activity)
        {
            _activity = activity;
        }

        public override void OnConnectionStateChange(BluetoothGatt gatt, GattStatus status, ProfileState newState)
        {
            try
            {
                base.OnConnectionStateChange(gatt, status, newState);

                Log.Debug("MyGattCallback", $"OnConnectionStateChange: status={status}, newState={newState}");

                if (newState == ProfileState.Connected)
                {
                    _activity.IsConnected = true;
                    gatt.DiscoverServices();
                }
                else if (newState == ProfileState.Disconnected)
                {
                    _activity.IsConnected = false;
                }

                _activity.UpdateConnectionStatusText();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        public override void OnServicesDiscovered(BluetoothGatt gatt, GattStatus status)
        {
            base.OnServicesDiscovered(gatt, status);
            if (status == GattStatus.Success)
            {
                _activity.OnServicesDiscovered();
            }
        }

        public override void OnCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic)
        {
            base.OnCharacteristicChanged(gatt, characteristic);
            _activity.OnCharacteristicChanged(characteristic);
        }

        public override void OnDescriptorWrite(BluetoothGatt gatt, BluetoothGattDescriptor descriptor,
            GattStatus status)
        {
            base.OnDescriptorWrite(gatt, descriptor, status);
        }
    }
}