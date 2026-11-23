# Changelog

All notable changes to this project are documented here.
Format based on [Keep a Changelog](https://keepachangelog.com/), versions follow [Semantic Versioning](https://semver.org/).

## [1.0.1] - 2026-11-23

### Changed
- Moved from .NET 6 (out of support) to .NET 8.
- README: screenshots, accurate description (the app cannot redirect the sound to another output).

## [1.0.0] - 2026-11-23

First public release.

### Added
- Bluetooth A2DP sink using the Windows `AudioPlaybackConnection` API: any paired phone plays its audio on the PC.
- Automatic reconnection, and automatic connection to the last phone at startup.
- Notification-area icon; closing the window keeps the app running.
- Optional "Start with Windows" (starts hidden).
- Live audio level bar.
- Windows 11 look, light and dark themes, follows the Windows accent color.
- Single-instance protection.
