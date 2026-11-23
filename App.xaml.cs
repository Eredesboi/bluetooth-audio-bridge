using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace BluetoothAudioBridge;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ApplyTheme();
        // Follow the Windows light/dark setting and accent color live.
        SystemEvents.UserPreferenceChanged += (_, _) => Dispatcher.Invoke(ApplyTheme);
    }

    void ApplyTheme()
    {
        var light = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 0) is int v && v == 1;
        if (Environment.GetEnvironmentVariable("BAB_THEME") is string t) light = t == "light"; // test override
        Resources.MergedDictionaries[0] = new ResourceDictionary { Source = new Uri(light ? "Light.xaml" : "Dark.xaml", UriKind.Relative) };
        Resources["Accent"] = new SolidColorBrush(SystemParameters.WindowGlassColor);
    }
}
