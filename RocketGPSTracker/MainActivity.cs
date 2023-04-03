using Android;
using Android.App;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using Google.Android.Material.Snackbar;
using Java.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Android.Content.PM;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using AlertDialog = AndroidX.AppCompat.App.AlertDialog;
using Exception = System.Exception;
using Toolbar = AndroidX.AppCompat.Widget.Toolbar;
using Xamarin.Essentials;
using Android.Locations;

namespace RocketGPSTracker
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, IOnMapReadyCallback, ILocationListener

    {
        private BluetoothAdapter _bluetoothAdapter;
        private BluetoothDevice _bluetoothDevice;
        private BluetoothGatt _bluetoothGatt;
        private MapView _mapView;
        private GoogleMap _googleMap;
        private const int REQUEST_BLUETOOTH_PERMISSIONS = 2;
        private bool _autoCenter = false;
        private ImageButton _toggleCenter;
        private ScanCallback _scanCallback;
        public bool isConnected = false;
        private BluetoothGattCharacteristic _coordinateCharacteristic;
        private static readonly UUID COORDINATE_SERVICE_UUID = UUID.FromString("4fafc201-1fb5-459e-8fcc-c5c9c331914b");
        private static readonly UUID COORDINATE_CHARACTERISTIC_UUID = UUID.FromString("beb5483e-36e1-4688-b7f5-ea07361b26a8");

        private double _initialLatitude;
        private double _initialLongitude;
        private AlertDialog _deviceListDialog;
        private BluetoothDeviceReceiver _deviceReceiver;
        private TextView _bleDataTextView;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            try
            {
                base.OnCreate(savedInstanceState);
                Xamarin.Essentials.Platform.Init(this, savedInstanceState);
                SetContentView(Resource.Layout.activity_main);
                Toolbar toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
                SetSupportActionBar(toolbar);

                BluetoothManager bluetoothManager = (BluetoothManager)GetSystemService(Context.BluetoothService);
                _bluetoothAdapter = bluetoothManager.Adapter;
                _bleDataTextView = FindViewById<TextView>(Resource.Id.bleDataTextView);


                _initialLatitude = 40.1630475; //40.1630475,-76.3007722
                _initialLongitude = -76.3007722;

                ImageButton mapTypeToggleButton = FindViewById<ImageButton>(Resource.Id.mapTypeToggleButton);

                mapTypeToggleButton.Click += MapTypeToggleButtonOnClick;

                _mapView = FindViewById<MapView>(Resource.Id.mapView);
                _mapView.OnCreate(savedInstanceState);
                _mapView.GetMapAsync(this);
                _scanCallback = new MyScanCallback(device =>
                {
                    _bluetoothAdapter.BluetoothLeScanner.StopScan(_scanCallback);
                    SaveDeviceAddress(device.Address);
                    ConnectToDevice(device.Address);
                });

                List<string> permissionsToRequest = new List<string>();

                if (CheckSelfPermission(Manifest.Permission.AccessFineLocation) !=
                    Android.Content.PM.Permission.Granted)
                {
                    permissionsToRequest.Add(Manifest.Permission.AccessFineLocation);
                }

                if (CheckSelfPermission(Manifest.Permission.Bluetooth) != Android.Content.PM.Permission.Granted)
                {
                    permissionsToRequest.Add(Manifest.Permission.Bluetooth);
                }

                if (CheckSelfPermission(Manifest.Permission.BluetoothAdmin) != Android.Content.PM.Permission.Granted)
                {
                    permissionsToRequest.Add(Manifest.Permission.BluetoothAdmin);
                }

                if (CheckSelfPermission(Manifest.Permission.BluetoothConnect) != Android.Content.PM.Permission.Granted)
                {
                    permissionsToRequest.Add(Manifest.Permission.BluetoothConnect);
                }

                if (CheckSelfPermission(Manifest.Permission.BluetoothScan) != Android.Content.PM.Permission.Granted)
                {
                    permissionsToRequest.Add(Manifest.Permission.BluetoothScan);
                }

                if (permissionsToRequest.Count > 0)
                {
                    RequestPermissions(permissionsToRequest.ToArray(), REQUEST_BLUETOOTH_PERMISSIONS);
                }
                else
                {
                    string savedDeviceAddress = LoadDeviceAddress();
                    if (!string.IsNullOrEmpty(savedDeviceAddress))
                    {
                        ConnectToDevice(savedDeviceAddress);
                    }
                    else
                    {
                        if (_bluetoothAdapter.IsEnabled)
                        {
                            var scanSettings = new ScanSettings.Builder()
                                .SetScanMode(Android.Bluetooth.LE.ScanMode.LowLatency)
                                .Build();

                            _bluetoothAdapter.BluetoothLeScanner.StartScan(new List<ScanFilter>(), scanSettings,
                                _scanCallback);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                ; // handle exception
            }
        }

        public void OnLocationChanged(Android.Locations.Location location)
        {
            // Update the destination marker with new coordinates
            AddDestinationMarker(location.Latitude, location.Longitude);
        }
        public void OnProviderDisabled(string provider) { }

        public void OnProviderEnabled(string provider) { }

        public void OnStatusChanged(string provider, [GeneratedEnum] Availability status, Bundle extras) { }

        private void SetUpLocationListener()
        {
            LocationManager locationManager = (LocationManager)GetSystemService(LocationService);
            string provider = LocationManager.GpsProvider;

            if (locationManager.IsProviderEnabled(provider))
            {
                locationManager.RequestLocationUpdates(provider, 2000, 1, this);
            }
            else
            {
                Toast.MakeText(this, "GPS provider is not enabled", ToastLength.Short).Show();
            }
        }


        private void ConnectToDevice(string deviceAddress)
        {
            _bluetoothDevice = _bluetoothAdapter.GetRemoteDevice(deviceAddress);
            _bluetoothGatt = _bluetoothDevice.ConnectGatt(this, false, new MyGattCallback(this));
        }

        public void OnServicesDiscovered()
        {
            BluetoothGattService coordinateService = _bluetoothGatt.GetService(COORDINATE_SERVICE_UUID);
            if (coordinateService != null)
            {
                _coordinateCharacteristic = coordinateService.GetCharacteristic(COORDINATE_CHARACTERISTIC_UUID);
                if (_coordinateCharacteristic != null)
                {
                    _bluetoothGatt.SetCharacteristicNotification(_coordinateCharacteristic, true);
                    BluetoothGattDescriptor descriptor =
                        _coordinateCharacteristic.GetDescriptor(
                            Java.Util.UUID.FromString("00002902-0000-1000-8000-00805f9b34fb"));
                    descriptor.SetValue(BluetoothGattDescriptor.EnableNotificationValue.ToArray());
                    _bluetoothGatt.WriteDescriptor(descriptor);
                }
            }
        }
        public void UpdateConnectionStatusText()
        {
            RunOnUiThread(() =>
            {
                string connectionStatus = isConnected ? "Connected" : "Disconnected";
                _bleDataTextView.Text += $"\nBluetooth Status: {connectionStatus}";
            });
        }


        public void OnCharacteristicChanged(BluetoothGattCharacteristic characteristic)
        {
            if (characteristic.Uuid.Equals(COORDINATE_CHARACTERISTIC_UUID))
            {
                string receivedData = characteristic.GetStringValue(0);
                if (!string.IsNullOrEmpty(receivedData))
                {
                    string[] coordinates = receivedData.Split(',');
                    if (coordinates.Length == 2)
                    {
                        if (double.TryParse(coordinates[0], out double latitude) && double.TryParse(coordinates[1], out double longitude))
                        {
                            UpdateMap(latitude, longitude);

                            // Update the TextView with the data and timestamp
                            RunOnUiThread(() =>
                            {
                                _bleDataTextView.Text = $"Data: {receivedData}\nTimestamp: {DateTime.Now.ToString("HH:mm:ss.fff")}";
                                UpdateConnectionStatusText();
                            });

                        }
                    }
                }
            }
        }



        private async Task ConnectToDeviceAsync(string deviceAddress)
        {
            _bluetoothDevice = _bluetoothAdapter.GetRemoteDevice(deviceAddress);
            _bluetoothGatt = _bluetoothDevice.ConnectGatt(this, false, new MyGattCallback(this));
        }

        public void OnMapReady(GoogleMap googleMap)
        {
            _googleMap = googleMap;
            _googleMap.UiSettings.ZoomControlsEnabled = true;
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation) == Permission.Granted)
            {
                _googleMap.MyLocationEnabled = true;
            }
            else
            {
                ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.AccessFineLocation }, 1);
            }
            _toggleCenter = FindViewById<ImageButton>(Resource.Id.toggleCenter);
            _toggleCenter.Click += (sender, e) =>
            {
                _autoCenter = !_autoCenter;
            };

            // Set the map type to satellite
            SetUpLocationListener();
            _googleMap.MapType = GoogleMap.MapTypeNormal;
            MoveCameraToCoordinates(_initialLatitude, _initialLongitude);


        }

        private void AddDestinationMarker(double latitude, double longitude)
        {
            LatLng destination = new LatLng(latitude, longitude);
            MarkerOptions markerOptions = new MarkerOptions();
            markerOptions.SetPosition(destination);
            markerOptions.SetTitle("Destination");
            markerOptions.SetIcon(BitmapDescriptorFactory.DefaultMarker(BitmapDescriptorFactory.HueBlue)); // Set the marker color to blue
            _googleMap.AddMarker(markerOptions);
        }


        private void MoveCameraToCoordinates(double latitude, double longitude)
        {
            try
            {
                //if (_googleMap == null) return;

                LatLng position = new LatLng(latitude, longitude);
                CameraUpdate cameraUpdate = CameraUpdateFactory.NewLatLngZoom(position, 15);
                _googleMap.MoveCamera(cameraUpdate);
                AddMarkerAtCoordinates(latitude, longitude); // Add this line
            }
            catch (Exception ex)
            {
                ;
            }
        }
        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                return true;
            }
            else if (id == Resource.Id.action_bluetooth)
            {
                ShowDeviceListDialog();
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        private void FabOnClick(object sender, EventArgs eventArgs)
        {
            View view = (View)sender;
            Snackbar.Make(view, "Replace with your own action", Snackbar.LengthLong)
                .SetAction("Action", (View.IOnClickListener)null).Show();
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions,
            Android.Content.PM.Permission[] grantResults)
        {
            if (requestCode == REQUEST_BLUETOOTH_PERMISSIONS)
            {
                if (grantResults.Length >= 6 &&
                    grantResults[0] == Android.Content.PM.Permission.Granted &&
                    grantResults[1] == Android.Content.PM.Permission.Granted &&
                    grantResults[2] == Android.Content.PM.Permission.Granted &&
                    grantResults[3] == Android.Content.PM.Permission.Granted &&
                    grantResults[4] == Android.Content.PM.Permission.Granted &&
                    grantResults[5] == Android.Content.PM.Permission.Granted)
                {
                    string savedDeviceAddress = LoadDeviceAddress();
                    if (!string.IsNullOrEmpty(savedDeviceAddress))
                    {
                        ConnectToDevice(savedDeviceAddress);
                    }
                    else
                    {
                        if (_bluetoothAdapter.IsEnabled)
                        {
                            var scanSettings = new ScanSettings.Builder()
                                .SetScanMode(Android.Bluetooth.LE.ScanMode.LowLatency)
                                .Build();

                            _bluetoothAdapter.BluetoothLeScanner.StartScan(new List<ScanFilter>(), scanSettings,
                                _scanCallback);
                        }

                    }
                }
                else
                {
                    Snackbar.Make(FindViewById(Android.Resource.Id.Content), "Permissions are required to use this app",
                            Snackbar.LengthIndefinite)
                        .SetAction("OK", v => { })
                        .Show();
                }
            }

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        }

        protected override void OnResume()
        {
            base.OnResume();
            _mapView.OnResume();

        }

        protected override void OnPause()
        {
            _mapView.OnPause();
            base.OnPause();
        }

        protected override void OnDestroy()
        {
            // Unregister the BroadcastReceiver
            UnregisterReceiver(_deviceReceiver);

            _mapView.OnDestroy();
            base.OnDestroy();
        }

        protected override void OnSaveInstanceState(Bundle outState)
        {
            base.OnSaveInstanceState(outState);
            _mapView.OnSaveInstanceState(outState);
        }

        public override void OnLowMemory()
        {
            base.OnLowMemory();
            _mapView.OnLowMemory();
        }

        private void AddMarkerAtCoordinates(double latitude, double longitude)
        {
            if (_googleMap == null) return;

            LatLng position = new LatLng(latitude, longitude);
            MarkerOptions markerOptions = new MarkerOptions()
                .SetPosition(position)
                .SetTitle("Rocket Position");

            _googleMap.AddMarker(markerOptions);
        }

        private void MapTypeToggleButtonOnClick(object sender, EventArgs eventArgs)
        {
            if (_googleMap == null) return;

            if (_googleMap.MapType == GoogleMap.MapTypeNormal)
            {
                _googleMap.MapType = GoogleMap.MapTypeSatellite;
            }
            else
            {
                _googleMap.MapType = GoogleMap.MapTypeNormal;
            }
        }

        private void ShowDeviceListDialog()
        {
            try
            {
                HashSet<BluetoothDevice> deviceSet = new HashSet<BluetoothDevice>(_bluetoothAdapter.BondedDevices);

                AlertDialog.Builder builder = new AlertDialog.Builder(this);
                builder.SetTitle("Choose a Bluetooth device");

                LayoutInflater inflater = LayoutInflater.From(this);
                View dialogView = inflater.Inflate(Resource.Layout.device_list, null);

                ListView listView = dialogView.FindViewById<ListView>(Resource.Id.device_list);
                ArrayAdapter<string> adapter =
                    new ArrayAdapter<string>(this, Resource.Layout.device_list_item, Resource.Id.device_name);

                foreach (var device in deviceSet)
                {
                    adapter.Add(device.Name + "\n" + device.Address);
                }

                listView.Adapter = adapter;
                listView.ItemClick += async (sender, e) =>
                {
                    BluetoothDevice selectedDevice = deviceSet.ElementAt(e.Position);
                    SaveDeviceAddress(selectedDevice.Address); // Save the device address
                    await ConnectToDeviceAsync(selectedDevice.Address);
                    _deviceListDialog.Dismiss();
                };

                builder.SetView(dialogView);
                _deviceListDialog = builder.Create();
                _deviceListDialog.Show();

                _deviceReceiver.OnDeviceFound += (deviceAddress) =>
                {
                    BluetoothDevice device = _bluetoothAdapter.GetRemoteDevice(deviceAddress);
                    if (!deviceSet.Contains(device))
                    {
                        deviceSet.Add(device);
                        RunOnUiThread(() =>
                        {
                            adapter.Add(device.Name + "\n" + device.Address);
                            adapter.NotifyDataSetChanged();
                        });
                    }
                };

                // Start discovering devices
                if (_bluetoothAdapter.IsEnabled)
                {
                    var scanSettings = new ScanSettings.Builder()
                        .SetScanMode(Android.Bluetooth.LE.ScanMode.LowLatency)
                        .Build();

                    _bluetoothAdapter.BluetoothLeScanner.StartScan(new List<ScanFilter>(), scanSettings, _scanCallback);
                }

            }
            catch (Exception ex)
            {
                Toast.MakeText(this, "Error: " + ex.Message, ToastLength.Long).Show();
            }
        }

        private void SaveDeviceAddress(string deviceAddress)
        {
            ISharedPreferences prefs = GetSharedPreferences("RocketGPSTrackerPreferences", FileCreationMode.Private);
            ISharedPreferencesEditor editor = prefs.Edit();
            editor.PutString("DeviceAddress", deviceAddress);
            editor.Apply();
        }

        private string LoadDeviceAddress()
        {
            ISharedPreferences prefs = GetSharedPreferences("RocketGPSTrackerPreferences", FileCreationMode.Private);
            return prefs.GetString("DeviceAddress", null);
        }


        public void UpdateMap(double latitude, double longitude)
        {
            RunOnUiThread(() =>
            {
                LatLng newPosition = new LatLng(latitude, longitude);
                _googleMap.Clear();
                _googleMap.AddMarker(new MarkerOptions().SetPosition(newPosition).SetTitle("Current Position"));
                if (_autoCenter)
                {
                    _googleMap.MoveCamera(CameraUpdateFactory.NewLatLngZoom(newPosition, 15));
                }
                AddDestinationMarker(latitude, longitude);
            });
        }

    }

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

                        string deviceInfo = device.Name + "\n" + device.Address;
                        (context as MainActivity)?.RunOnUiThread(() =>
                        {
                            ArrayAdapter<string> adapter = ((ArrayAdapter<string>)((MainActivity)context)
                                .FindViewById<ListView>(Resource.Id.device_list).Adapter);
                            if (!AdapterContainsItem(adapter, deviceInfo))
                            {
                                adapter.Add(deviceInfo);
                                adapter.NotifyDataSetChanged();
                            }
                        });
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

    public class MyGattCallback : BluetoothGattCallback
    {
        private readonly MainActivity _activity;
        private bool isConnected = false;


        public MyGattCallback(MainActivity activity)
        {
            _activity = activity;
        }

        public override void OnConnectionStateChange(BluetoothGatt gatt, GattStatus status, ProfileState newState)
        {
            base.OnConnectionStateChange(gatt, status, newState);
            if (newState == ProfileState.Connected)
            {
                _activity.isConnected = true;
                gatt.DiscoverServices();
            }
            else if (newState == ProfileState.Disconnected)
            {
                _activity.isConnected = false;
            }
            _activity.UpdateConnectionStatusText();
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