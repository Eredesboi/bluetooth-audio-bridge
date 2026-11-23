using Windows.Devices.Enumeration;
using Windows.Media.Audio;

namespace BluetoothAudioBridge;

/// <summary>
/// A2DP Sink via the WinRT AudioPlaybackConnection API (Windows 10 2004+).
/// Works with any phone/device acting as an A2DP source (iPhone, Android, ...).
/// Windows decodes the stream and plays it on the endpoint chosen in the Volume mixer;
/// raw PCM is never exposed to the app.
/// </summary>
public sealed class BridgeService : IDisposable
{
    public event Action<string>? Status;

    AudioPlaybackConnection? _conn;
    string? _wantedId;      // non-null = user wants to stay connected
    bool _retrying;

    public bool IsWanted => _wantedId != null;
    public bool IsConnected => _conn != null;

    // Paired devices that can act as an A2DP source. Pairing is done in Windows Settings.
    public static async Task<List<DeviceInformation>> ListPairedAsync() =>
        (await DeviceInformation.FindAllAsync(AudioPlaybackConnection.GetDeviceSelector())).ToList();

    public async Task ConnectAsync(string deviceId)
    {
        _wantedId = deviceId;
        Status?.Invoke("Connecting…");
        try { await OpenAsync(); }
        catch (Exception ex) { Status?.Invoke(ex.Message + " Retrying…"); _ = RetryLoop(); }
    }

    public void Disconnect()
    {
        _wantedId = null;
        CloseConn();
        Status?.Invoke("Disconnected");
    }

    async Task OpenAsync()
    {
        CloseConn();
        var conn = AudioPlaybackConnection.TryCreateFromId(_wantedId!)
            ?? throw new InvalidOperationException("Device not found or not paired.");
        conn.StateChanged += OnStateChanged;
        await conn.StartAsync();
        var res = await conn.OpenAsync();
        if (res.Status != AudioPlaybackConnectionOpenResultStatus.Success)
        {
            conn.Dispose();
            throw new InvalidOperationException(res.Status == AudioPlaybackConnectionOpenResultStatus.RequestTimedOut
                ? "The phone did not answer (Bluetooth on? in range?)."
                : "Connection refused by the device.");
        }
        _conn = conn;
        Status?.Invoke("Connected — now pick this PC as audio output on your phone");
    }

    void OnStateChanged(AudioPlaybackConnection c, object _)
    {
        if (c.State == AudioPlaybackConnectionState.Closed && _wantedId != null && ReferenceEquals(c, _conn))
        {
            Status?.Invoke("Connection lost — reconnecting…");
            _ = RetryLoop();
        }
    }

    async Task RetryLoop()
    {
        if (_retrying) return;
        _retrying = true;
        try
        {
            while (_wantedId != null)
            {
                await Task.Delay(3000);
                if (_wantedId == null) break;
                try { await OpenAsync(); return; }
                catch (Exception ex) { Status?.Invoke(ex.Message + " Retrying…"); }
            }
        }
        finally { _retrying = false; }
    }

    void CloseConn()
    {
        var c = _conn; _conn = null;
        if (c != null) { c.StateChanged -= OnStateChanged; c.Dispose(); }
    }

    public void Dispose() => Disconnect();
}
