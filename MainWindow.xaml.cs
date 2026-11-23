using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Windows.Devices.Enumeration;

namespace BluetoothAudioBridge;

public partial class MainWindow : Window
{
    readonly BridgeService _bridge = new();
    readonly LevelMeter _meter = new();
    readonly Settings _settings = Settings.Load();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    readonly Tray _tray;
    double _level;
    bool _autoDone;

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

    static void Open(string uri) => Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    void Bluetooth_Click(object s, RoutedEventArgs e) => Open("ms-settings:bluetooth");
    void Sound_Click(object s, RoutedEventArgs e) => Open("ms-settings:sound");
}
