# Bluetooth Audio Bridge

Turn your Windows PC into a Bluetooth speaker for **any phone or Bluetooth audio source**.
The sound shows up in Windows as a **regular app** ("Bluetooth Audio Bridge"), so you can route it from the Windows volume mixer to any output: speakers, headset, a virtual audio device or a mixing app.

- Free and open source (MIT)
- No cloud, no telemetry, no network access at all
- No driver, no extra software on the phone, no Wi-Fi

```
Phone ──Bluetooth A2DP──▶ Windows (AudioPlaybackConnection) ──▶ Volume mixer ──▶ any output ──▶ headset
```

## Why this works (feasibility)

Windows has no *visible* A2DP Sink feature: pairing a phone does not make the PC play its audio.
However, **Windows 10 version 2004 (build 19041) and Windows 11 ship an official API**,
[`Windows.Media.Audio.AudioPlaybackConnection`](https://learn.microsoft.com/uwp/api/windows.media.audio.audioplaybackconnection),
that opens the A2DP Sink profile. Windows then decodes and plays the stream itself.

What that means:

| | |
|---|---|
| Native driver / C++ needed? | **No.** The API is usable straight from C#. |
| Raw PCM access? | **No.** Windows plays the sound; the app never sees the samples. |
| Choose the output device from code? | **No.** You pick it in the Windows volume mixer (per-app output). |
| Latency | That of the Bluetooth codec (SBC/AAC), roughly 100–200 ms. Not reducible by the app. |
| Hardware | Your Bluetooth adapter must support the A2DP Sink role (most recent adapters do). |

## Requirements

- Windows 10 version 2004 or later, or Windows 11
- Bluetooth adapter with A2DP Sink support
- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) to build (or any newer SDK that can target `net6.0-windows10.0.19041.0`)

## Build and run

```powershell
git clone https://github.com/Eredesboi/bluetooth-audio-bridge.git
cd bluetooth-audio-bridge
dotnet run -c Release
```

Standalone executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained
```

## Usage

1. **Pair your phone once** in Windows: Settings › Bluetooth & devices › Add device. (The app has a shortcut button if the list is empty.)
2. Open the app, pick the phone in the list, turn on the **Connection** switch.
3. **On the phone, choose the PC as the audio output**
   (usually the output or cast icon in the music player or the quick settings panel).
   Without this step the sound stays on the phone.
4. Play something. The audio level bar shows the live level.
5. If the link drops (out of range, Bluetooth toggled), the app retries every 3 seconds until you turn the switch off.

The app follows the Windows light/dark setting and accent color.

## Route the sound to a specific output or mixer

1. Connect your phone and start playing so that the app appears in the mixer.
2. Open Windows Settings › System › Sound › **Volume mixer** (button in the app).
3. Find *Bluetooth Audio Bridge* and set its **Output device** (speakers, headset, virtual audio device…).

The app is a normal Windows audio app, so any mixer or virtual audio device can pick it up.

## Test procedure

1. Bluetooth on both devices. Pair the phone in Windows settings.
2. Open the app → select the phone → turn on the switch. Status turns to "Connected".
3. On the phone, select the PC as audio output, then play music or a video.
4. Sound comes out of the PC and the level bar moves.
5. Walk away or toggle the phone's Bluetooth, then come back: status goes "Connection lost — reconnecting…" then "Connected" again.

## Known limitations

- Output device is chosen in the Windows volume mixer only (the API does not expose it).
- One phone at a time.
- The level bar reads the audio session of this app's process. If Windows ever plays the stream under another process, the bar can stay empty while sound still plays.
- Not tested with every phone / Bluetooth adapter. Reports and pull requests welcome.

## Architecture

| File | Role |
|---|---|
| `BridgeService.cs` | A2DP Sink connection (`AudioPlaybackConnection`), paired device list, auto-reconnect |
| `LevelMeter.cs` | Audio level of this app's sessions via WASAPI (NAudio) |
| `MainWindow.xaml(.cs)` | WPF UI (Windows 11 look): device list, connection switch, level bar |
| `App.xaml(.cs)`, `Dark.xaml`, `Light.xaml`, `Themes.xaml` | Theme (follows Windows light/dark and accent), shared styles |

Only dependency: [NAudio](https://github.com/naudio/NAudio) (MIT) for the level meter.

## License

MIT, see [LICENSE](LICENSE).
