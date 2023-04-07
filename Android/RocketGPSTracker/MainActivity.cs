using Android;
using Android.App;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.Content.PM;
using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.Graphics;
using Android.Locations;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Google.Android.Material.Snackbar;
using Java.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Exception = System.Exception;
using Path = System.IO.Path;
using Toolbar = AndroidX.AppCompat.Widget.Toolbar;

namespace RocketGPSTracker
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true, ScreenOrientation = ScreenOrientation.Portrait)]

    public class MainActivity : AppCompatActivity, IOnMapReadyCallback, ILocationListener
    {

        private BluetoothAdapter _bluetoothAdapter;
        private BluetoothDevice _bluetoothDevice;
        private BluetoothGatt _bluetoothGatt;
        private BluetoothGattCharacteristic _coordinateCharacteristic;
        private BluetoothManager bluetoothManager;

        private MapView _mapView;
        private GoogleMap _googleMap;
        private Marker _destinationMarker;
        private Marker _bleMarker;

        private TextView _bleDataTextView;

        private ImageButton _toggleCenter;

        private const int RequestPermissionsRequestCode = 1000;

        private List<Tuple<DateTime, double, double>> _coordinateLog = new List<Tuple<DateTime, double, double>>();
        private PolylineOptions _polylineOptions;

        private double _initialLatitude;
        private double _initialLongitude;
        private double _bleLatitude;
        private double _bleLongitude;

        public bool IsConnected = false;
        private bool _autoCenter = true;

        private LocationManager _locationManager;
        private string _locationProvider;
        
        private ScanCallback _scanCallback;

        private static readonly UUID CoordinateServiceUuid = UUID.FromString("4fafc201-1fb5-459e-8fcc-c5c9c331914b");
        private static readonly UUID CoordinateCharacteristicUuid = UUID.FromString("beb5483e-36e1-4688-b7f5-ea07361b26a8");


        protected override async void OnCreate(Bundle savedInstanceState)
        {
            try
            {
                // Base setup
                base.OnCreate(savedInstanceState);
                Xamarin.Essentials.Platform.Init(this, savedInstanceState);
                SetContentView(Resource.Layout.activity_main);

                // Toolbar
                Toolbar toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
                SetSupportActionBar(toolbar);

                // Location
                InitializeLocationManager();

                // Bluetooth
                bluetoothManager = (BluetoothManager)GetSystemService(Context.BluetoothService);
                _bluetoothAdapter = bluetoothManager.Adapter;

                // BLE data display
                _bleDataTextView = FindViewById<TextView>(Resource.Id.bleDataTextView);

                // Map setup
                _initialLatitude = 39.771823; //40.1630475,-76.3007722
                _initialLongitude = -74.897318; //39.771823, -74.897318

                ImageButton mapTypeToggleButton = FindViewById<ImageButton>(Resource.Id.mapTypeToggleButton);
                mapTypeToggleButton.Click += MapTypeToggleButtonOnClick;

                _mapView = FindViewById<MapView>(Resource.Id.mapView);
                _mapView.OnCreate(savedInstanceState);
                _mapView.GetMapAsync(this);

                // Connect button
                var connectButton = FindViewById<Button>(Resource.Id.connectButton);
                connectButton.Click += ConnectButtonOnClick;

                // Request permissions
                RequestPermissionsIfNeeded();
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Error {ex.Message}", ToastLength.Long).Show();
            }
        }


        private void ConnectButtonOnClick(object sender, EventArgs e)
        {
            // Connect to the Bluetooth device here
            string savedDeviceAddress = LoadDeviceAddress();
            if (!string.IsNullOrEmpty(savedDeviceAddress))
            {
                ConnectToDevice(savedDeviceAddress);
            }
        }

        private void RequestPermissionsIfNeeded()
        {
            List<string> permissionsToRequest = new List<string>();


            if (CheckSelfPermission(Manifest.Permission.AccessFineLocation) != Permission.Granted)
            {
                permissionsToRequest.Add(Manifest.Permission.AccessFineLocation);
            }

            if (CheckSelfPermission(Manifest.Permission.AccessCoarseLocation) != Permission.Granted)
            {
                permissionsToRequest.Add(Manifest.Permission.AccessCoarseLocation);
            }

            if (CheckSelfPermission(Manifest.Permission.Bluetooth) != Permission.Granted)
            {
                permissionsToRequest.Add(Manifest.Permission.Bluetooth);
            }

            if (CheckSelfPermission(Manifest.Permission.BluetoothAdmin) != Permission.Granted)
            {
                permissionsToRequest.Add(Manifest.Permission.BluetoothAdmin);
            }

            if (CheckSelfPermission(Manifest.Permission.BluetoothConnect) != Permission.Granted)
            {
                permissionsToRequest.Add(Manifest.Permission.BluetoothConnect);
            }

            if (CheckSelfPermission(Manifest.Permission.BluetoothScan) != Permission.Granted)
            {
                permissionsToRequest.Add(Manifest.Permission.BluetoothScan);
            }

            if (permissionsToRequest.Count > 0)
            {
                ActivityCompat.RequestPermissions(this, permissionsToRequest.ToArray(), RequestPermissionsRequestCode);
            }
            else
            {
                if (_bluetoothAdapter.IsEnabled)
                {
                    _scanCallback = new MyScanCallback(device =>
                    {
                        _bluetoothAdapter.BluetoothLeScanner.StopScan(_scanCallback);
                        SaveDeviceAddress(device.Address);
                    });
                    var scanSettings = new ScanSettings.Builder().SetScanMode(Android.Bluetooth.LE.ScanMode.LowLatency).Build();
                    _bluetoothAdapter.BluetoothLeScanner.StartScan(new List<ScanFilter>(), scanSettings, _scanCallback);
                }
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions,
            Permission[] grantResults)
        {
            if (requestCode == RequestPermissionsRequestCode)
            {
                if (AllPermissionsGranted())
                {
                    ; // I guess maybe a singleton for the scanner on first run
                }
                else
                {
                    Snackbar.Make(FindViewById(Android.Resource.Id.Content),
                            "Certain permissions are required to use this app", Snackbar.LengthIndefinite)
                        .SetAction("OK", v => { RequestPermissionsIfNeeded(); })
                        .Show();
                }
            }

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        private bool AllPermissionsGranted()
        {
            return CheckSelfPermission(Manifest.Permission.ReadExternalStorage) == Permission.Granted &&
                   CheckSelfPermission(Manifest.Permission.WriteExternalStorage) == Permission.Granted &&
                   CheckSelfPermission(Manifest.Permission.AccessFineLocation) == Permission.Granted &&
                   CheckSelfPermission(Manifest.Permission.Bluetooth) == Permission.Granted &&
                   CheckSelfPermission(Manifest.Permission.BluetoothAdmin) == Permission.Granted &&
                   CheckSelfPermission(Manifest.Permission.BluetoothConnect) == Permission.Granted &&
                   CheckSelfPermission(Manifest.Permission.BluetoothScan) == Permission.Granted;
        }





        public void OnProviderDisabled(string provider)
        {
        }

        public void OnProviderEnabled(string provider)
        {
        }

        public void OnStatusChanged(string provider, [GeneratedEnum] Availability status, Bundle extras)
        {
        }

        protected override void OnStart()
        {
            base.OnStart();
        }



        private void SetUpLocationListener()
        {
            try
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
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Error {ex.Message}", ToastLength.Long).Show();
            }
        }


        private void ConnectToDevice(string deviceAddress)
        {
            try
            {
                _bluetoothDevice = _bluetoothAdapter.GetRemoteDevice(deviceAddress);
                _bluetoothGatt = _bluetoothDevice.ConnectGatt(this, false, new MyGattCallback(this), BluetoothTransports.Le);
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Error {ex.Message}", ToastLength.Long).Show();
            }
        }
        
        public void OnServicesDiscovered()
        {
            BluetoothGattService coordinateService = _bluetoothGatt.GetService(CoordinateServiceUuid);
            if (coordinateService != null)
            {
                _coordinateCharacteristic = coordinateService.GetCharacteristic(CoordinateCharacteristicUuid);
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

        


        public void OnMapReady(GoogleMap googleMap)
        {
            try
            {
                _googleMap = googleMap;
                _googleMap.UiSettings.ZoomControlsEnabled = true;
                _polylineOptions = new PolylineOptions().InvokeWidth(10).InvokeColor(Color.Red);

                if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation) ==
                    Permission.Granted)
                {
                    _googleMap.MyLocationEnabled = true;
                }
                else
                {
                    ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.AccessFineLocation }, 1);
                }

                _toggleCenter = FindViewById<ImageButton>(Resource.Id.toggleCenter);
                _toggleCenter.Click += (sender, e) => { _autoCenter = !_autoCenter; };

                SetUpLocationListener();
                _googleMap.MapType = GoogleMap.MapTypeNormal;
                MoveCameraToCoordinates(_initialLatitude, _initialLongitude);
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Error {ex.Message}", ToastLength.Long).Show();
            }

        }

        private void UpdateMap(double phoneLatitude, double phoneLongitude, double bleLatitude, double bleLongitude)
        {
            RunOnUiThread(() =>
            {
                LatLng phonePosition = new LatLng(phoneLatitude, phoneLongitude);
                if (_destinationMarker == null)
                {
                    _destinationMarker = _googleMap.AddMarker(new MarkerOptions().SetPosition(phonePosition)
                        .SetTitle("Phone GPS")
                        .SetIcon(BitmapDescriptorFactory.DefaultMarker(BitmapDescriptorFactory.HueBlue)));
                }
                else
                {
                    _destinationMarker.Position = phonePosition;
                }

                MoveCameraToCoordinates(bleLatitude, bleLongitude);
            });
        }




        private void MoveCameraToCoordinates(double latitude, double longitude)
        {
            try
            {
                LatLng position = new LatLng(latitude, longitude);

                if (_bleMarker == null)
                {
                    if (_bleMarker == null)
                    {
                        _bleMarker = _googleMap.AddMarker(new MarkerOptions()
                            .SetPosition(position)
                            .SetTitle("BLE GPS")
                            .SetSnippet($"Latest Received Coords: {_bleLatitude}, {_bleLongitude}") // Add this line
                            .SetIcon(BitmapDescriptorFactory.DefaultMarker(BitmapDescriptorFactory.HueRed)));
                    }
                }
                else
                {
                    _bleMarker.Position = position;
                }

                CameraUpdate cameraUpdate = CameraUpdateFactory.NewLatLngZoom(position, 15);

                _polylineOptions.Add(position);
                // Draw the polyline on the map
                _googleMap.AddPolyline(_polylineOptions);
                if (_autoCenter)
                    _googleMap.MoveCamera(cameraUpdate);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public void OnCharacteristicChanged(BluetoothGattCharacteristic characteristic)
        {
            if (characteristic.Uuid.Equals(CoordinateCharacteristicUuid))
            {
                string receivedData = characteristic.GetStringValue(0);
                if (!string.IsNullOrEmpty(receivedData))
                {
                    string[] coordinates = receivedData.Split(',');
                    if (coordinates.Length == 2)
                    {
                        if (double.TryParse(coordinates[0], out _bleLatitude) && double.TryParse(coordinates[1], out _bleLongitude))
                        {
                            RunOnUiThread(() =>
                            {
                                _bleDataTextView.SetText("", TextView.BufferType.Normal); // Clear the text before appending new data
                                _bleDataTextView.Append($"Data: {receivedData}\nTimestamp: {DateTime.Now.ToString("HH:mm:ss.fff")}");
                                UpdateConnectionStatusText();
                                UpdateBleMarkerSnippet(); // Add this line
                            });
                        }
                    }
                }
            }
        }

        public void UpdateConnectionStatusText()
        {
            RunOnUiThread(() =>
            {
                string connectionStatus = IsConnected ? "Connected" : "Disconnected";
                _bleDataTextView.Append($"\nBluetooth Status: {connectionStatus}");
            });
        }

        public void OnLocationChanged(Android.Locations.Location location)
        {
            UpdateMap(location.Latitude, location.Longitude, _bleLatitude, _bleLongitude);
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }


        protected override void OnResume()
        {
            base.OnResume();
            _mapView.OnResume();
            _locationManager.RequestLocationUpdates(_locationProvider, 0, 0, this);

        }

        protected override void OnPause()
        {
            _mapView.OnPause();
            base.OnPause();
            _locationManager.RemoveUpdates(this);
        }

        protected override void OnDestroy()
        {
            // Unregister the BroadcastReceiver

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




        private async Task SaveCoordinatesToFileAsync(double latitude, double longitude)
        {
            try
            {
                string fileName = "coordinates_log.txt";
                var folderPath =
                    Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDocuments);

                string fullPath = Path.Combine(folderPath.ToString(), fileName);

                await using (StreamWriter writer = File.AppendText(fullPath))
                {
                    string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                    await writer.WriteLineAsync($"{timestamp} - {latitude},{longitude}");
                }
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Error {ex.Message}", ToastLength.Long).Show();
            }
        }


        private async Task DedupeAndSaveCoordinates(double latitude, double longitude)
        {
            DateTime now = DateTime.UtcNow;
            TimeSpan dedupeInterval = TimeSpan.FromMinutes(5);

            if (_coordinateLog.Any())
            {
                var latestEntry = _coordinateLog.Last();

                if (Math.Abs(latitude - latestEntry.Item2) < 0.000001 &&
                    Math.Abs(longitude - latestEntry.Item3) < 0.000001)
                {
                    if ((now - latestEntry.Item1) >= dedupeInterval)
                    {
                        await SaveCoordinatesToFileAsync(latitude, longitude);
                        _coordinateLog.Add(Tuple.Create(now, latitude, longitude));
                    }
                }
                else
                {
                    await SaveCoordinatesToFileAsync(latitude, longitude);
                    _coordinateLog.Add(Tuple.Create(now, latitude, longitude));
                }
            }
            else
            {
                await SaveCoordinatesToFileAsync(latitude, longitude);
                _coordinateLog.Add(Tuple.Create(now, latitude, longitude));
            }
        }

        private void InitializeLocationManager()
        {
            _locationManager = (LocationManager)GetSystemService(LocationService);

            Criteria criteriaForLocationProvider = new Criteria
            {
                Accuracy = Accuracy.Fine,
                PowerRequirement = Power.NoRequirement
            };

            IList<string> acceptableLocationProviders =
                _locationManager.GetProviders(criteriaForLocationProvider, true);
            if (acceptableLocationProviders.Any())
            {
                _locationProvider = acceptableLocationProviders.First();
            }
            else
            {
                _locationProvider = string.Empty;
            }
        }

        private void UpdateBleMarkerSnippet()
        {
            if (_bleMarker != null)
            {
                _bleMarker.Snippet = $"Latest Received Coords: {_bleLatitude}, {_bleLongitude}";
            }
        }

    }
}