using Windows.Devices.Enumeration;
using Windows.Media.Audio;

namespace BluetoothAudioBridge;

/// <summary>
/// A2DP Sink via the WinRT AudioPlaybackConnection API (Windows 10 2004+).
/// Works with any phone/device acting as an A2DP source.
/// Windows decodes the stream and plays it on the endpoint chosen in the Volume mixer;
/// raw PCM is never exposed to the app.
///
/// While "wanted", one loop keeps the connection alive: it (re)creates the connection when it is
/// closed and tries to open it every few seconds, so a phone that comes back in range
/// (or connects by itself) is picked up without any click.
/// </summary>
public sealed class BridgeService : IDisposable
{
    public event Action<string>? Status;
    public event Action? Opened;   // connected (possibly again): the stream may need re-routing

    const int RetryMs = 3000;

    AudioPlaybackConnection? _conn;
    string? _wantedId;      // non-null = user wants to stay connected
    int _generation;        // bumped on every connect/disconnect so stale loops stop
    string _last = "";

    public bool IsWanted => _wantedId != null;
    public bool IsConnected => _conn?.State == AudioPlaybackConnectionState.Opened;

    // Paired devices that can act as an A2DP source. Pairing is done in Windows Settings.
    public static async Task<List<DeviceInformation>> ListPairedAsync() =>
        (await DeviceInformation.FindAllAsync(AudioPlaybackConnection.GetDeviceSelector())).ToList();

    public void Connect(string deviceId)
    {
        CloseConn();
        _wantedId = deviceId;
        _ = Loop(++_generation);
    }

    public void Disconnect()
    {
        _wantedId = null;
        _generation++;
        CloseConn();
        Report("Disconnected");
    }

    async Task Loop(int gen)
    {
        Report("Connecting…");
        while (gen == _generation && _wantedId != null)
        {
            try
            {
                if (_conn == null)
                {
                    var c = AudioPlaybackConnection.TryCreateFromId(_wantedId)
                        ?? throw new InvalidOperationException("Device not found or not paired.");
                    c.StateChanged += OnStateChanged;
                    await c.StartAsync();
                    _conn = c;
                }
                if (_conn.State != AudioPlaybackConnectionState.Opened)
                {
                    var res = await _conn.OpenAsync();
                    if (gen != _generation) return;
                    Report(res.Status == AudioPlaybackConnectionOpenResultStatus.Success
                        ? "Connected — pick this PC as audio output on your phone"
                        : "Waiting for your phone (Bluetooth on? in range?)…");
                }
            }
            catch (Exception ex)
            {
                CloseConn();
                Report(ex.Message + " Retrying…");
            }
            await Task.Delay(RetryMs);
        }
    }

    void OnStateChanged(AudioPlaybackConnection c, object _)
    {
        if (c.State == AudioPlaybackConnectionState.Closed && ReferenceEquals(c, _conn) && _wantedId != null)
        {
            CloseConn(); // the loop recreates it
            Report("Connection lost — reconnecting…");
        }
        else if (c.State == AudioPlaybackConnectionState.Opened)
            Report("Connected — pick this PC as audio output on your phone");
    }

    void Report(string s)
    {
        if (s.StartsWith("Connected")) Opened?.Invoke();
        if (s == _last) return;
        _last = s;
        Status?.Invoke(s);
    }

    void CloseConn()
    {
        var c = _conn; _conn = null;
        if (c != null) { c.StateChanged -= OnStateChanged; c.Dispose(); }
    }

    public void Dispose() { _wantedId = null; _generation++; CloseConn(); }
}
