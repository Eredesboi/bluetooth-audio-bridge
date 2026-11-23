using NAudio.CoreAudioApi;

namespace BluetoothAudioBridge;

/// <summary>Peak level (0..1) of this process's audio sessions on all active render devices.</summary>
public sealed class LevelMeter : IDisposable
{
    readonly MMDeviceEnumerator _enum = new();
    readonly int _pid = Environment.ProcessId;

    public float Read()
    {
        float peak = 0;
        try
        {
            foreach (var dev in _enum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                var sessions = dev.AudioSessionManager.Sessions;
                for (int i = 0; i < sessions.Count; i++)
                {
                    var s = sessions[i];
                    if (s.GetProcessID == _pid) peak = Math.Max(peak, s.AudioMeterInformation.MasterPeakValue);
                }
            }
        }
        catch { /* device came or went; next tick retries */ }
        return peak;
    }

    public void Dispose() => _enum.Dispose();
}
