using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace BluetoothAudioBridge;

/// <summary>Remembers the last phone and whether to stay connected. Stored locally, never sent anywhere.</summary>
public sealed class Settings
{
    public string? DeviceId { get; set; }
    public bool AutoConnect { get; set; }
    public string? OutputId { get; set; }   // null = system default

    static readonly string PathFile = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BluetoothAudioBridge", "settings.json");

    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(PathFile)) ?? new(); }
        catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathFile)!);
        File.WriteAllText(PathFile, JsonSerializer.Serialize(this));
    }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool StartsWithWindows
    {
        get { using var k = Registry.CurrentUser.OpenSubKey(RunKey); return k?.GetValue("BluetoothAudioBridge") != null; }
        set
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, true)!;
            if (value) k.SetValue("BluetoothAudioBridge", $"\"{Environment.ProcessPath}\" --minimized");
            else k.DeleteValue("BluetoothAudioBridge", false);
        }
    }
}
