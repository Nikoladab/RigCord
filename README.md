<p align="center">
  <img src="assets/logo.png" width="120" alt="RigCord logo">
</p>

<h1 align="center">RigCord</h1>

<p align="center">
  <b>Your rig, live on Discord.</b><br>
  Live CPU, GPU and RAM usage and the game you're playing, right on your Discord profile.
</p>

<p align="center">
  <a href="https://github.com/Nikoladab/RigCord/releases/latest"><img src="https://img.shields.io/github/v/release/Nikoladab/RigCord?style=flat-square&color=6366f1&label=version" alt="Latest version"></a>
  <a href="https://github.com/Nikoladab/RigCord/releases"><img src="https://img.shields.io/github/downloads/Nikoladab/RigCord/total?style=flat-square&color=22d3ee" alt="Downloads"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0ea5e9?style=flat-square" alt="Windows 10 | 11">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-a78bfa?style=flat-square" alt="MIT license"></a>
</p>

<p align="center">
  <a href="https://github.com/Nikoladab/RigCord/releases/latest/download/RigCord.exe">
    <img src="https://img.shields.io/badge/Download%20for%20Windows-RigCord.exe-5865F2?style=for-the-badge&logo=windows&logoColor=white" alt="Download RigCord for Windows">
  </a>
</p>

---

## Overview

RigCord sits quietly in your system tray and turns your Discord status into a live hardware monitor. Your friends see what you're playing and how hard your rig is working, updated every few seconds.

```
Playing RigCord
In Cyberpunk 2077
CPU 31% • GPU 94% 76°C • RAM 15.2/32 GB
1:25:02
```

## Features

| | |
|---|---|
| **Live hardware stats** | CPU, GPU, RAM and VRAM usage, measured the same way Task Manager does |
| **Game detection** | Recognises thousands of games automatically, shows the official game icon and a play timer |
| **GPU temperature** | Live temperature on NVIDIA graphics cards |
| **Custom text** | Write your own status lines with placeholders like `{game}`, `{cpu}` or `{gpuname}` |
| **Custom button** | Add a link to your YouTube, Twitch or anything else under your activity |
| **Automatic update alerts** | Get notified as soon as a new version is released |
| **Featherweight** | Around 6 MB of RAM and near-zero CPU while it runs |
| **Portable** | A single small `.exe` with no installer and no dependencies |

## Getting started

### 1. Download
Download **[RigCord.exe](https://github.com/Nikoladab/RigCord/releases/latest/download/RigCord.exe)** (latest version) and place it in a folder of your choice.

> [!NOTE]
> Windows may show *"Windows protected your PC"* the first time you run it. This is normal for independent apps without a paid code-signing certificate. Click **More info → Run anyway**.

### 2. Create your Discord application
1. Open the [Discord Developer Portal](https://discord.com/developers/applications) and click **New Application**.
2. Give it a name, which is what appears after **"Playing"** on your profile (for example `RigCord` or `My PC`).
3. Copy the **Application ID** from the *General Information* page.

### 3. Connect
1. Run `RigCord.exe`, paste the Application ID and click **Save**.
2. In Discord, go to **User Settings → Activity Privacy** and turn on **Share your detected activities with others**.

That's it. Enable **Start with Windows** in RigCord and it will run automatically from now on.

## Customisation

| Where | What you can change |
|---|---|
| **Display** | Show or hide the game, CPU/GPU names, VRAM, GPU temperature and the timer |
| **✎ Advanced** | Write your own status lines. These override the Display options. |
| **Advanced settings** | Update interval, custom hardware names, your own profile button, and apps that should never count as a game |

**Available placeholders:** `{game}` `{cpu}` `{gpu}` `{temp}` `{ram}` `{ram%}` `{vram}` `{cpuname}` `{gpuname}`

All settings are stored in `settings.ini`. If that file sits next to `RigCord.exe`, RigCord runs in portable mode.

## FAQ

<details>
<summary><b>I can't see the button on my profile.</b></summary>
<br>
Discord never shows activity buttons to the profile owner. Everyone else can see it. The preview in RigCord shows how it looks to them.
</details>

<details>
<summary><b>My status doesn't appear on Discord.</b></summary>
<br>
Make sure you're using the Discord <b>desktop app</b>, not the browser version, that <i>Share your detected activities</i> is enabled, and that the Application ID is correct. The status pill in the top-right corner of RigCord shows whether it's connected.
</details>

<details>
<summary><b>A program that isn't a game shows up as a game.</b></summary>
<br>
Open <b>Advanced settings → Game detection</b> and add it to the ignore list. While it's running, a one-click "ignore" button appears there.
</details>

<details>
<summary><b>Is it safe?</b></summary>
<br>
Yes. RigCord is fully open source, so you can read every line in <code>src/RigCord.cs</code>. It only talks to your local Discord app and to public Discord/GitHub endpoints, and it never collects any data.
</details>

## Requirements

- Windows 10 or 11
- Discord desktop app

## Building from source

No SDK required. RigCord compiles with the C# compiler that ships with Windows (.NET Framework 4.x).

```bat
build.bat
```

`src/make-icon.ps1` regenerates the app icon and logo from `assets/logo-original.jpg`.

## License

Released under the [MIT License](LICENSE).
Made by **[Nikoladab](https://github.com/Nikoladab)**.
