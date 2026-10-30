# KINGDOM HEARTS Launcher
One simple and clean launcher for the Steam releases of KINGDOM HEARTS 1.5+2.5, 2.8 and III, plus Melody of Memory and the 358/2 Days native PC port.

> [!CAUTION]
> This is only compatible with the Steam releases. Due to the nature of how Epic Games version works, this launcher cannot be made compatible with it.

## Features
- Every game in one menu, in a single executable
- Controller support (XInput and DirectInput pads): navigate the game list and the config with the left stick / D-pad
- "Add to Steam" integration, with optional artwork
- Not having Disney and Square Enix copyright screens on launch
- Launching the 358/2 Days native PC port instead of the collection's movie
- Launching Melon Mix instead of game's movie implementations of 358/2 Days and Re:coded
- Launching GBA Chain of Memories instead of Re:Chain of Memories
- Best game orders available, such as alphabetical and reverse chronological (i want to see a marathon with alphabetical 2 reverse)
- UI-less launching via launch options (see below)

## Install
**Requirements:** Windows 10 (1803 or newer) or Windows 11, and the Steam version of the games. Nothing else to install: the launcher is a single exe and uses the .NET Framework that ships with Windows.

The launcher is designed to sit in the same `steamapps\common` folder as the games, in its own `KINGDOM HEARTS Launcher` folder:

| Steam library | Where the launcher folder goes |
|---|---|
| Default install | `C:\Program Files (x86)\Steam\steamapps\common\KINGDOM HEARTS Launcher\` |
| Another library | `D:\SteamLibrary\steamapps\common\KINGDOM HEARTS Launcher\` (use the library that holds your games) |

```
steamapps\common\
  KINGDOM HEARTS ...                      <- your game folders, untouched
  KINGDOM HEARTS Launcher\                <- put the launcher folder here
    KINGDOM HEARTS Launcher.exe
    launcher_config.json                  <- created on first run
    INSTALL.txt
    LICENSE.txt
```

1. Download the release zip (`KINGDOM-HEARTS-Launcher-v<version>.zip`) and **extract it**. Don't run the exe from inside the zip.
2. Move the extracted `KINGDOM HEARTS Launcher` folder into your `steamapps\common` folder, as shown above. (Or skip this: if you run the launcher from anywhere else, it offers on first run to move itself there. See **Moving the launcher** below.) (To find it: in Steam, right-click a KINGDOM HEARTS game > Manage > Browse local files, then go up one folder.) Steam ignores folders it doesn't manage, so this doesn't interfere with updates or verification.
3. Run `KINGDOM HEARTS Launcher.exe`. If Windows SmartScreen says "Windows protected your PC", click **More info** > **Run anyway** (the exe isn't code-signed).
4. Because the launcher is next to the games, it finds the games folder by itself. Open **Launcher Config** to check which collections it found, and to change the folder if you put the launcher somewhere else.
5. Set the **Melody of Memory** exe, and optionally the **358/2 Days native port** (see below).
6. Pick a game and press **Launch**.

**Settings** are saved in `launcher_config.json` next to the exe. If that folder isn't writable, they're saved to `%LOCALAPPDATA%\KINGDOM HEARTS Launcher\launcher_config.json` instead, so the launcher also works from write-protected locations. If both exist, the one next to the exe wins.

Games that can't be found are greyed out. The launcher writes `steam_appid.txt` into the game folders when launching, so the games can start outside of Steam (if a game folder is write-protected, it skips this and launches anyway).

On first run you're offered to add the launcher to Steam (see **Add to Steam** below); the Steam entry points to the exe's current location, so do step 2 before that.

**Updating:** replace the exe and keep `launcher_config.json`. **Uninstalling:** delete the folder (and the Steam entry, if you added one).

### Moving the launcher
If the launcher isn't in a `steamapps\common` folder, it offers on first run to move itself into one. The prompt has the folder pre-filled with the Steam library that holds your games (change it with **Browse** or by typing). On **Move**, the launcher copies itself and its settings there, starts again from the new place, and removes its old files (only the ones it copied; the old folder is deleted only if that leaves it empty). It's asked once; to do it later, use **Move this launcher to a Steam library folder...** in **Launcher Config**. The new copy then offers to add itself to Steam, which also updates an existing Steam entry that pointed at the old location.

### Making a release zip
Run this from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\Package-Release.ps1
```

It rebuilds the launcher in Release mode and creates `dist\KINGDOM-HEARTS-Launcher-v<version>.zip` containing the exe, `INSTALL.txt` (the instructions above, from `Distribution\INSTALL.txt`) and the license. The version comes from `Properties\AssemblyInfo.cs`, so bump `AssemblyVersion` and `AssemblyFileVersion` there before packaging. Keep `Distribution\INSTALL.txt` in sync when these instructions change. Share the zip (for example as a GitHub release asset).

## Controller
- **Left stick / D-pad up and down** move the selection.
- **A** (Cross on PlayStation pads) launches the selected game.
- **X** (Square) opens the config.
- In the config: stick / D-pad moves between options, **A** toggles or presses, **B** (Circle) closes a dropdown or cancels the dialog. Picking paths (the Browse buttons and path boxes) still needs a mouse and keyboard.
- DirectInput pads are read through the Windows joystick API, using buttons 1-3 (X/A/B on Xbox-layout pads, Square/Cross/Circle on PlayStation pads). Input is ignored while a message box is open. It can be turned off in the config.

## Melody of Memory
Set the game's exe in **Launcher Config** (Browse, or paste the path). There's no automatic detection, so any copy of the game works wherever it is. The launcher doesn't write a `steam_appid.txt` for it.

## Add to Steam
On first run the launcher offers to add itself to your Steam library as a non-Steam game, with its artwork, so it can be started from Steam, Big Picture and your controller. It's a single prompt (**A** on a controller to accept, **B** for "not now") and it's only asked once. If Steam is running, the launcher closes it cleanly and starts it again afterwards, because Steam would otherwise overwrite the change when it exits. The prompt isn't shown if Steam isn't installed or the launcher is already in your library.

To do it later, or again after moving the launcher folder, use **Add this launcher to Steam** in **Launcher Config**. It adds the launcher to the Steam account used most recently (the old `shortcuts.vdf` is saved as `shortcuts.vdf.khbackup`), and pressing it again updates the same entry instead of adding another.

Artwork is looked up by file name (`.png` or `.jpg`, except `logo.json`):

| File | Used as | Size |
|---|---|---|
| `capsule` | portrait capsule | 600x900 |
| `wide` | wide capsule | 460x215 |
| `hero` | hero | 3840x1240 |
| `logo` | logo over the hero | |
| `icon` | library icon | 512x512 |
| `logo.json` | logo position on the hero (optional; Steam writes it when you adjust the logo) | |

Put the files in `KINGDOM HEARTS Launcher\Assets\Steam\` before building (they're embedded, so the launcher stays one exe), or in a `Steam Assets` folder next to the exe (which takes priority).

## 358/2 Days native PC port
In **Launcher Config**, set the install folder or exe of the port (default `C:\Program Files\Kingdom Hearts 3582 Days`) and tick **Use the native PC port for 358/2 Days**. The launcher picks the most likely exe in that folder; if it guesses wrong, browse to the exe directly. The native port takes priority over Melon Mix and the movie.

## Launch options
Add these to a shortcut or Steam's launch options to launch a game without a UI. You still need the launcher to configure how the games are launched.

* -skipLauncher - launches the last game you launched via the launcher
* -kh1 - launches KH1
* -recom - launches Re: Chain of Memories
* -days - launches 358/2 Days
* -kh2 - launches KH2
* -bbs - launches Birth by Sleep
* -recoded - launches Re:coded
* -ddd - launches Dream Drop Distance
* -bbs02 - launches Birth by Sleep 0.2
* -backcover - launches X Back Cover
* -kh3 - launches KINGDOM HEARTS III
* -mom - launches Melody of Memory

## Steam Input and controllers
If you use the launcher through Steam, keep **Steam Input the same** for the launcher and for the games. Steam handles your controller differently depending on this setting, and a mismatch can leave the controller without input once a game starts (the lightbar of a PlayStation controller may turn plain red):

| Launcher | Game | Result |
|---|---|---|
| on | on | works |
| off | off | works |
| on | off | doesn't work |

To change it, right-click the launcher (or the game) in your Steam library > **Properties** > **Controller** > **Override for ...** and pick the same choice for both. With Steam Input off, the launcher reads PlayStation and Xbox controllers directly, and the games get the raw controller. With it on, Steam presents your controller to everything as an Xbox controller.

## Advanced
If a game isn't detected because of an unusual install layout, add its exe to `ExeOverrides` in `launcher_config.json`, keyed by game id (`kh1`, `recom`, `days`, `kh2`, `bbs`, `recoded`, `ddd`, `bbs02`, `backcover`, `kh3`; Melody of Memory uses its own path setting).

## Building
Open `Kingdom-Hearts-Launcher.sln` in Visual Studio, or use `Package-Release.ps1` (see **Install**). The build tools need the **.NET Framework 4.7.2 targeting pack** (in the Visual Studio Installer under *Individual components*, or the [Developer Pack](https://aka.ms/msbuild/developerpacks)). If it's missing you'll see error `MSB3644`; `Package-Release.ps1` works around that automatically by building against the .NET Framework that comes with Windows, but opening the solution in Visual Studio or running MSBuild directly will not. There are no NuGet dependencies, so the build output is a single exe.

## Credits
- Based on the original Kingdom Hearts launchers by [SandeMC](https://github.com/SandeMC/Kingdom-Hearts-Launchers) (MIT license; the original copyright notice is kept in `LICENSE.txt`). This version merges them into one program and adds controller support, KINGDOM HEARTS III, Melody of Memory, the 358/2 Days native port and Steam integration.
- 358/2 Days and Re:coded can be launched through Melon Mix, a separate project that this launcher only starts.
- KINGDOM HEARTS is a trademark of Disney Enterprises, Inc. and Square Enix Co., Ltd. This is an unofficial fan-made tool, not affiliated with or endorsed by either company, and it contains no game files. The artwork in `Assets\Steam\` belongs to its respective owners.

## Demo
### Official Launcher
https://github.com/SandeMC/Kingdom-Hearts-Launchers/assets/70141395/444215c5-6e5a-4552-b71e-418acfa878dd
### This launcher
Game copyright screens can be further slimmed down with [mods](https://www.pcgamingwiki.com/wiki/Kingdom_Hearts_HD_1.5_%2B_2.5_ReMIX#Skip_intro_videos). This launcher can also skip the copyright screen for KH1 without any other mods.

https://github.com/SandeMC/Kingdom-Hearts-Launchers/assets/70141395/8b67c65c-9381-4b63-af88-289ecb001d27
