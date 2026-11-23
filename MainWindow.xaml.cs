using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Windows.Devices.Enumeration;

namespace BluetoothAudioBridge;

public partial class MainWindow : Window
{
    readonly BridgeService _bridge = new();
    readonly LevelMeter _meter = new();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    double _level;

    public MainWindow()
    {
        InitializeComponent();
        _bridge.Status += s => Dispatcher.Invoke(() => SetStatus(s));
        _timer.Tick += (_, _) =>
        {
            // sqrt = readable at low volume, decay = smooth fall
            _level = Math.Max(Math.Sqrt(_meter.Read()), _level * 0.9);
            Level.Value = _level;
        };
        _timer.Start();
        Loaded += async (_, _) => await RefreshAsync();
        Closed += (_, _) => { _bridge.Dispose(); _meter.Dispose(); };
    }

    void SetStatus(string text)
    {
        StatusText.Text = text;
        Toggle.IsChecked = _bridge.IsWanted;
    }

    async Task RefreshAsync()
    {
        try
        {
            var list = await BridgeService.ListPairedAsync();
            Devices.ItemsSource = list;
            if (list.Count > 0) Devices.SelectedIndex = 0;
            EmptyState.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            Devices.Visibility = list.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception ex) { SetStatus("Bluetooth error: " + ex.Message); }
    }

    async void Refresh_Click(object s, RoutedEventArgs e) => await RefreshAsync();

    async void Toggle_Click(object s, RoutedEventArgs e)
    {
        if (_bridge.IsWanted) { _bridge.Disconnect(); return; }
        if (Devices.SelectedItem is DeviceInformation d)
        {
            Toggle.IsChecked = true;
            await _bridge.ConnectAsync(d.Id);
        }
        else { Toggle.IsChecked = false; SetStatus("Pick a phone first"); }
    }

    static void Open(string uri) => Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    void Bluetooth_Click(object s, RoutedEventArgs e) => Open("ms-settings:bluetooth");
    void Mixer_Click(object s, RoutedEventArgs e) => Open("ms-settings:apps-volume");
}
