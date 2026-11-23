using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using Windows.Devices.Enumeration;

namespace BluetoothAudioBridge;

public sealed record OutputDevice(string? Id, string Name);

public partial class MainWindow : Window
{
    readonly BridgeService _bridge = new();
    readonly LevelMeter _meter = new();
    readonly Settings _settings = Settings.Load();
    readonly Tray _tray;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    readonly DispatcherTimer _routeTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    double _level;
    bool _autoDone, _loadingOuts, _routeNeeded;

    public MainWindow()
    {
        InitializeComponent();
        _tray = new Tray(this);
        if (Environment.GetCommandLineArgs().Contains("--minimized")) Loaded += (_, _) => Hide(); // start in the tray
        Startup.IsChecked = Settings.StartsWithWindows;
        _bridge.Status += s => Dispatcher.Invoke(() => SetStatus(s));
        _timer.Tick += (_, _) =>
        {
            // sqrt = readable at low volume, decay = smooth fall
            _level = Math.Max(Math.Sqrt(_meter.Read()), _level * 0.9);
            Level.Value = _level;
        };
        _timer.Start();

        // Windows only accepts per-app routing once this app has an audio session (stream playing),
        // so the route is (re)applied every 2 s until it succeeds, and again after each (re)connection.
        _routeNeeded = _settings.OutputId != null;
        _bridge.Opened += () => Dispatcher.Invoke(() => _routeNeeded |= _settings.OutputId != null);
        _routeTimer.Tick += (_, _) => TryRoute();
        _routeTimer.Start();
        LoadOutputs();

        Loaded += async (_, _) => await RefreshAsync();
        Closed += (_, _) => { _bridge.Dispose(); _meter.Dispose(); _tray.Dispose(); };
    }

    void SetStatus(string text)
    {
        StatusText.Text = text;
        _tray.SetText(text);
        Toggle.IsChecked = _bridge.IsWanted;
    }

    async Task RefreshAsync()
    {
        try
        {
            var list = await BridgeService.ListPairedAsync();
            Devices.ItemsSource = list;
            var saved = list.FindIndex(d => d.Id == _settings.DeviceId);
            if (list.Count > 0) Devices.SelectedIndex = saved >= 0 ? saved : 0;
            EmptyState.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            Devices.Visibility = list.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            // Reconnect to the last phone at startup if it was connected when the app last ran.
            if (!_autoDone && saved >= 0 && _settings.AutoConnect)
            {
                _autoDone = true;
                _bridge.Connect(list[saved].Id);
                Toggle.IsChecked = true;
            }
        }
        catch (Exception ex) { SetStatus("Bluetooth error: " + ex.Message); }
    }

    async void Refresh_Click(object s, RoutedEventArgs e) => await RefreshAsync();

    void Toggle_Click(object s, RoutedEventArgs e)
    {
        if (_bridge.IsWanted)
        {
            _bridge.Disconnect();
            _settings.AutoConnect = false;
        }
        else if (Devices.SelectedItem is DeviceInformation d)
        {
            _bridge.Connect(d.Id);
            _settings.DeviceId = d.Id;
            _settings.AutoConnect = true;
        }
        else { Toggle.IsChecked = false; SetStatus("Pick a phone first"); return; }
        _settings.Save();
    }

    void Startup_Click(object s, RoutedEventArgs e) => Settings.StartsWithWindows = Startup.IsChecked == true;

    // Output selector: active playback devices only (no microphones, no disabled devices).
    void LoadOutputs()
    {
        _loadingOuts = true;
        var keep = (Outputs.SelectedItem as OutputDevice)?.Id ?? _settings.OutputId;
        using var en = new MMDeviceEnumerator();
        var list = new List<OutputDevice> { new(null, "System default") };
        list.AddRange(en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(d => new OutputDevice(d.ID, d.FriendlyName)));
        Outputs.ItemsSource = list;
        var sel = list.FirstOrDefault(o => o.Id == keep) ?? list[0];
        Outputs.SelectedItem = sel;
        OutName.Text = sel.Name;
        _loadingOuts = false;
    }

    void TryRoute()
    {
        if (_routeNeeded && AudioRouting.SetOutput(_settings.OutputId)) _routeNeeded = false;
    }

    void OutBtn_Click(object s, RoutedEventArgs e)
    {
        LoadOutputs(); // devices may have been enabled or unplugged since last time
        OutPopup.IsOpen = true;
    }

    void Outputs_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (_loadingOuts || Outputs.SelectedItem is not OutputDevice o) return;
        OutName.Text = o.Name;
        _settings.OutputId = o.Id;
        _settings.Save();
        OutPopup.IsOpen = false;
        _routeNeeded = true;
        TryRoute();
    }

    static void Open(string uri) => Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    void Bluetooth_Click(object s, RoutedEventArgs e) => Open("ms-settings:bluetooth");
    void Mixer_Click(object s, RoutedEventArgs e) => Open("ms-settings:apps-volume");
}
