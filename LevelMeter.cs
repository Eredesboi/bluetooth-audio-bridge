using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace BluetoothAudioBridge;

/// <summary>
/// Peak level (0..1) of the phone's stream. Windows plays it from the audio service (svchost),
/// not from this app, so that is the session we read. Read-only: nothing is captured or changed.
/// </summary>
public sealed class LevelMeter : IDisposable
{
    readonly MMDeviceEnumerator _enum = new();
    readonly Dictionary<uint, bool> _isService = new(); // pid -> is a svchost process

    bool IsService(uint pid)
    {
        if (pid == 0) return false;
        if (_isService.TryGetValue(pid, out var v)) return v;
        try { v = Process.GetProcessById((int)pid).ProcessName == "svchost"; } catch { v = false; }
        return _isService[pid] = v;
    }

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
                    if (IsService(s.GetProcessID)) peak = Math.Max(peak, s.AudioMeterInformation.MasterPeakValue);
                }
            }
        }
        catch { /* device came or went; next tick retries */ }
        return peak;
    }

    public void Dispose() => _enum.Dispose();
}
