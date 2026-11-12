<p align="center"><img src="assets/logo.png" width="128" alt="RigCord logo"></p>

<h1 align="center">RigCord</h1>
<p align="center">Your rig, live on Discord.</p>

<p align="center">
  <a href="https://github.com/Nikoladab/RigCord/releases/latest/download/RigCord.exe"><b>⬇ Download RigCord.exe</b></a>
  &nbsp;•&nbsp;
  <a href="https://github.com/Nikoladab/RigCord/releases/latest">All releases</a>
</p>

## How to download
You don't need to know anything about GitHub, and **don't** use the green **Code** button (that's the source code, not the app).

1. Click **[⬇ Download RigCord.exe](https://github.com/Nikoladab/RigCord/releases/latest/download/RigCord.exe)**. It always gets the newest version.
2. If your browser asks, choose **Keep** to save the file.
3. Double-click `RigCord.exe`. Nothing gets installed.
4. If Windows shows **"Windows protected your PC"**, click **More info → Run anyway**. Windows shows this for every app that isn't signed with a paid certificate.
5. Follow the [Setup](#setup) steps below. It takes about 1 minute.

> Tip: put `RigCord.exe` in its own folder (for example `Documents\RigCord`) before running it, and turn on **Start with Windows** in the app so you never have to open it again.

RigCord shows your live PC stats and the game you're playing on your Discord profile:

```
Playing RigCord
In Roblox
CPU 31% • GPU 94% 76°C • RAM 15.2/32 GB
1:25:02
```

## Features
- **Live stats**: CPU, GPU, RAM, and VRAM usage. GPU % is measured the same way Task Manager does it.
- **Game detection**: shows the game you're playing, with a timer that starts when the game started.
  - The game's official icon appears as a small badge on your picture, and hovering it shows the game name. Names and icons come from Discord's list of known games.
- **Custom text**: write your own lines with placeholders like `{game}`, `{cpu}`, `{gpu}`, `{temp}`, `{ram}`, `{vram}`, `{cpuname}`, and `{gpuname}`.
- **Update notifications**: RigCord checks GitHub Releases and tells you when a new version is out.
- **GPU temperature** on NVIDIA cards, plus optional CPU / GPU names.
- **Clickable profile picture** that links to this repo, plus an optional button of your own (shown in the app's preview).
- **Settings window** with a live profile preview and usage rings.
- **Light on resources**: about 6 MB RAM and about 0% CPU while it sits in the tray.
- **One small .exe**: nothing to install, and portable mode lets you share a ready-made folder.

## Setup
1. Create an app at https://discord.com/developers/applications. Its name is what appears after "Playing".
2. Run `RigCord.exe`, paste the **Application ID**, and press **Save**.
3. In Discord, turn on Settings → Activity Privacy → **Share your detected activities**.

## Advanced
- **✎ Advanced** (next to Display): write your own profile lines. They override the Display toggles.
- **Advanced settings** (bottom link) lets you change:
  - the update interval
  - CPU / GPU name overrides
  - one button of your own
  - which apps should never count as a game

Everything is saved in `settings.ini`. If that file is next to the exe, RigCord runs in portable mode.

## Build
Run `build.bat`. It uses the C# compiler that ships with Windows (.NET Framework 4.x), so you don't need to install anything.
`src/make-icon.ps1` regenerates `assets/logo.png` and the exe icon from `assets/logo-original.jpg`.

Requirements: Windows 10/11 and the Discord desktop app.

## License
[MIT](LICENSE) © Nikoladab
