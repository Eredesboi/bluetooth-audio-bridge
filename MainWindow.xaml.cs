using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.Devices.Enumeration;

namespace BluetoothAudioBridge;

public partial class MainWindow : Window
{
    const double Center = 88, Radius = 82; // ring geometry (176 px square, 6 px inset)

    readonly BridgeService _bridge = new();
    readonly LevelMeter _meter = new();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    double _level;

    public MainWindow()
    {
        InitializeComponent();
        BuildTitle();
        _bridge.Status += s => Dispatcher.Invoke(() => SetStatus(s));
        _timer.Tick += (_, _) => UpdateRing();
        _timer.Start();
        Loaded += async (_, _) => await RefreshAsync();
        Closed += (_, _) => { _bridge.Dispose(); _meter.Dispose(); };
    }

    // Outlined uppercase title (text turned into a geometry so it can be stroked).
    void BuildTitle()
    {
        var ft = new FormattedText("BLUETOOTH\nAUDIO BRIDGE", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.ExtraBold, FontStretches.Normal),
            30, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip) { LineHeight = 32 };
        TitlePath.Data = ft.BuildGeometry(new Point(0, 0));
    }

    void SetStatus(string text)
    {
        StatusText.Text = text;
        var connected = _bridge.IsConnected;
        Dot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, connected ? "Orange" : "Muted");
        ToggleText.Text = _bridge.IsWanted ? "Disconnect" : "Connect";
        ToggleText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, connected ? "Orange" : "Text");
    }

    void UpdateRing()
    {
        _level = Math.Max(Math.Sqrt(_meter.Read()), _level * 0.9); // sqrt = readable at low volume, decay = smooth fall
        var sweep = Math.Min(_level, 0.999) * 360;
        if (sweep < 1) { LevelArc.Data = Geometry.Empty; return; }
        var a = (sweep - 90) * Math.PI / 180;
        var end = new Point(Center + Radius * Math.Cos(a), Center + Radius * Math.Sin(a));
        var fig = new PathFigure { StartPoint = new Point(Center, Center - Radius) };
        fig.Segments.Add(new ArcSegment(end, new Size(Radius, Radius), 0, sweep > 180, SweepDirection.Clockwise, true));
        LevelArc.Data = new PathGeometry(new[] { fig });
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
            ToggleText.Text = "Disconnect";
            await _bridge.ConnectAsync(d.Id);
        }
        else SetStatus("Pick a phone first");
    }

    static void Open(string uri) => Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    void Bluetooth_Click(object s, RoutedEventArgs e) => Open("ms-settings:bluetooth");
    void Mixer_Click(object s, RoutedEventArgs e) => Open("ms-settings:apps-volume");
}
