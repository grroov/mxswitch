# mxswitch (Tandem Easy-Switch Fork)

Switch your **Logitech Signature Plus M750** mouse and **Logitech MX Keys S** keyboard in tandem between paired computers with a single press of the button directly below the M750 scroll wheel.

## Quick Reference Summary

- **macOS (Channel 1):**
  - **App bound in Logi Options+:** `/Applications/EasySwitch-Tandem.app`
  - **Config:** `~/.config/mxswitch/config` (`channel_a=1`, `channel_b=2`, `device=all`)
  - **One-step installer:** `./macos/install.sh`
  - **Inspect Bolt receiver slots:** `~/.local/bin/mxswitch --list`
  - **Inspect active channels:** `~/.local/bin/mxswitch --info --all`
- **Windows 11 (Channel 2):**
  - **App bound in Logi Options+:** `EasySwitch-Tandem.exe` (installed to `%LOCALAPPDATA%\Programs\EasySwitch\EasySwitch-Tandem.exe` and `C:\Program Files\EasySwitch\EasySwitch-Tandem.exe` when run as Admin, compiled via `windows\install.ps1` from `windows\EasySwitchTandem.cs`)
  - **Silent script fallbacks (if corporate EDR blocks custom `.exe`):** `%LOCALAPPDATA%\Programs\EasySwitch\EasySwitch-Tandem.cmd` / `EasySwitch-Tandem.vbs`
  - **Config & Cache:** `%LOCALAPPDATA%\mxswitch\config` and `%LOCALAPPDATA%\mxswitch\tandem_cache.txt`
  - **One-step installer:** `powershell.exe -ExecutionPolicy Bypass -File .\windows\install.ps1`
  - **Inspect Bolt receiver slots:** `powershell -ExecutionPolicy Bypass -File .\windows\mxswitch.ps1 -List`
  - **Inspect active channels:** `powershell -ExecutionPolicy Bypass -File .\windows\mxswitch.ps1 -Info -All`

---

Four independent implementations, one per platform. The native ones need **no
third-party dependencies**; the Python port needs `hidapi` except on Windows:

| Platform | File | Needs |
|---|---|---|
| macOS | `macos/mxswitch.c`, `macos/install.sh` | `clang` (Command Line Tools) |
| Windows | `windows/EasySwitchTandem.cs`, `windows/install.ps1`, `windows/mxswitch.ps1` | PowerShell 5.1+, built-in `.NET Framework csc.exe` |
| Linux | `linux/mxswitch.sh` | bash, coreutils, a udev rule |
| any | `python/mxswitch.py` | Python 3; `hidapi` except on Windows |

---

## Quick Start: One-Button Tandem Switching (`Signature Plus M750` + `MX Keys S`)

This fork includes Logi Bolt receiver register (`0xB5`) slot inspection, instant wake-up retry handling for sleeping Bolt peripherals, and native background app wrappers (`EasySwitch-Tandem.app` on macOS and `EasySwitch-Tandem.exe` on Windows 11) so you can bind the **Signature Plus M750's top button** (directly below the scroll wheel) in **Logi Options+** to switch both the mouse and keyboard in tandem between **Channel 1** and **Channel 2**.

### 1. macOS Setup (Channel 1 Host)

```bash
git clone https://github.com/grroov/mxswitch.git
cd mxswitch
chmod +x macos/install.sh
./macos/install.sh
```

What `macos/install.sh` does:
1. Compiles and ad-hoc signs `~/.local/bin/mxswitch`.
2. Creates `~/.config/mxswitch/config` (`channel_a=1`, `channel_b=2`, `device=all`).
3. Builds and signs `/Applications/EasySwitch-Tandem.app` (a headless native Mach-O bundle with `LSUIElement` / `LSBackgroundOnly` that never flashes a Dock icon or Terminal window).

**Configure macOS Permissions & Logi Options+:**
1. Open **System Settings → Privacy & Security → Input Monitoring**, click **`+`**, and enable **`/Applications/EasySwitch-Tandem.app`**.
2. Open **Logi Options+**, select **Signature Plus M750**, click the **Top button** (below the scroll wheel), choose **Open application**, and select **`/Applications/EasySwitch-Tandem.app`**.
3. You can now quit (`Cmd + Q`) the Logi Options+ window; `logioptionsplus_agent` runs the button action in the background.

Useful macOS CLI diagnostics:
```bash
~/.local/bin/mxswitch --list          # Lists all 6 Logi Bolt receiver slots & paired devices
~/.local/bin/mxswitch --info --all    # Shows active Easy-Switch channels & ChangeHost feature index
```

---

### 2. Windows 11 Setup (Channel 2 Host — Corporate Laptop Friendly)

No Visual Studio, no Python, no external SDKs, and no permanent Administrator daemon required. The installer compiles `windows\EasySwitchTandem.cs` into a standalone Windows GUI/background executable (`EasySwitch-Tandem.exe`) using the built-in Windows `.NET Framework` compiler (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`) that ships with every Windows 11 machine.

Open **Windows Terminal** (PowerShell) and run:

```powershell
git clone https://github.com/grroov/mxswitch.git
cd mxswitch
powershell -ExecutionPolicy Bypass -File .\windows\install.ps1
```

What `windows\install.ps1` does:
1. Writes `%LOCALAPPDATA%\mxswitch\config` (`channel_a=1`, `channel_b=2`, `device=all`).
2. Compiles `windows\EasySwitchTandem.cs` with `/target:winexe` into:
   ```text
   C:\Users\<YourUsername>\AppData\Local\Programs\EasySwitch\EasySwitch-Tandem.exe
   ```
   *(Because it is compiled with `/target:winexe`, clicking your mouse button runs the switch in <30 ms and **never** flashes a Command Prompt or PowerShell window.)*
3. Copies `mxswitch.ps1` into the same folder for CLI diagnostics.

**Configure Logi Options+ on Windows 11:**
1. Open **Logi Options+** on Windows 11 and select **Signature Plus M750**.
2. Click the **Top button** (directly below the scroll wheel).
3. Choose **Open application** and paste the path printed by `install.ps1`:
   ```text
   C:\Users\<YourUsername>\AppData\Local\Programs\EasySwitch\EasySwitch-Tandem.exe
   ```
4. Close the Logi Options+ window—pressing the top button below the scroll wheel will now immediately toggle both your **M750** and **MX Keys S** back to Channel 1 (or between Channel 1 and Channel 2 from either computer)!

Useful Windows CLI diagnostics:
```powershell
powershell -ExecutionPolicy Bypass -File .\windows\mxswitch.ps1 -List
powershell -ExecutionPolicy Bypass -File .\windows\mxswitch.ps1 -Info -All
```

---

Pick one. They are not layered on each other — each is a standalone file
implementing the same protocol against its platform's native HID API. When both
a mouse and a keyboard are present, the mouse is preferred by default; pass
`--all` to move every ChangeHost device on the machine together.

## Why this exists

Easy-Switch devices pair with up to three hosts but only expose channel
switching through a recessed button underneath the device. Logitech's own
software-side answer is Flow, which needs both machines on a network path that
can see each other — frequently blocked by corporate VPN or DLP policy. Options+
can bind Easy-Switch to a button or the Actions Ring, but that requires running
Options+ on every machine, and the Actions Ring is missing from some builds.

Underneath all of it is a single documented HID++ command. `mxswitch` sends that
command and nothing else.

## How it works

Logitech peripherals speak **HID++**, a request/response protocol tunnelled over
a vendor-defined HID collection alongside the normal mouse and keyboard reports.
Feature `0x1814` is `ChangeHost`; its function 1, `setCurrentHost`, takes a
zero-based host index and moves the device to that channel.

A request is 7 bytes (short) or 20 bytes (long):

```
byte 0    report id      0x10 short, 0x11 long
byte 1    device index   0xFF direct (Bluetooth/USB), 1..6 behind a receiver
byte 2    feature index  position of 0x1814 in this device's feature table
byte 3    (function << 4) | software id
byte 4+   parameters
```

Two details matter more than they look:

**The feature index is not the feature ID.** `0x1814` identifies the feature;
where it sits in a given device's table is firmware-specific. Hardcoding an
index works until a firmware update quietly moves it, after which you are
sending `setCurrentHost` to whatever feature now occupies that slot. Every
implementation here queries the root feature at runtime to resolve it.

**A successful `setCurrentHost` never replies.** By the time the device would
answer, it has already dropped off this host. Silence is success.

## The asymmetry worth understanding

A channel switch is always initiated from the machine the device is *currently*
connected to. An idle computer cannot summon the mouse; the active host tells the
device to leave. So `mxswitch` must be installed on **every** machine you want to
switch away from, each configured to send the device to the *other* one.

The practical consequence: if you switch to a channel whose host is asleep, off,
or has Bluetooth disabled, the device parks on a dead channel and the button
underneath is your only way back. Keep the target awake, or pair it via a
receiver, which stays live whenever the machine is powered.

## Command line

```sh
mxswitch 2          # switch to channel 2
mxswitch --info     # device, transport, feature index, current channel
mxswitch --all 2    # switch every ChangeHost device, not just the preferred one
mxswitch --name Keys 2  # switch matching keyboards to channel 2
mxswitch --toggle   # switch to the other channel in the configured pair
```

`--name` (repeatable) keeps only devices whose product name contains one of the
given substrings; with `--all` it moves exactly the matched devices, e.g. a
keyboard and mouse while leaving a third device alone. The config `name=` list
is combined with it. Devices behind a receiver are matched by their HID++ names;
unnamed receiver slots cannot match a name filter.

PowerShell spells the long options `-Info`, `-All`, `-Toggle`. `--toggle` reads
the current channel and moves to the other one of a pair you configure, so the
target is not hardcoded in your hotkey. The pair lives in
`${XDG_CONFIG_HOME:-$HOME/.config}/mxswitch/config` on macOS and Linux,
`%LOCALAPPDATA%\mxswitch\config` on Windows:

```ini
channel_a=1
channel_b=2
```

`--toggle --all` reads the preferred matching device and moves all matches to
the same target channel.

See `config.example` for device/name filters. Invalid config values are errors;
`--config PATH` must exist. `--status` adds battery/firmware, `--dry-run` previews
commands, and `--verbose` shows diagnostics. PowerShell uses `-Name`, `-Config`,
`-Status`, `-DryRun` and `-Verbose`.

Exit codes: `0` success, `1` no compatible device or operation failed,
`2` bad usage/configuration, `3` partial failure across multiple devices.

“Command sent” confirms the request was written, not connection to the target host.

## Install

Run from the tree, or install into a user prefix. Nothing needs a system-wide
`sudo make install` except the one-time Linux udev rule.

### macOS

```sh
make                 # builds and ad-hoc signs ./mxswitch
./mxswitch --info
```

To put it on your PATH without sudo:

```sh
make install PREFIX="$HOME/.local"
# → ~/.local/bin/mxswitch   (ensure ~/.local/bin is on PATH)
# Opens Input Monitoring settings if this binary is not already allowed.
```

Grant **Input Monitoring** in System Settings → Privacy & Security, both to the
binary and to whatever launches it. `make install` runs `mxswitch --setup`,
which skips the Settings pane when TCC already allows this binary.

The `codesign -s -` step in the Makefile is not cosmetic. TCC identifies
unsigned binaries by path *and* code hash, so an unsigned build silently loses
its Input Monitoring grant on every rebuild.

### Windows

```powershell
.\windows\mxswitch.ps1 -Info
.\windows\mxswitch.ps1 -Channel 2
```

No admin rights needed: the vendor-defined collection is not the exclusively
claimed mouse collection, so a normal user can open it.

If the execution policy objects:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\windows\mxswitch.ps1 -Channel 2
```

### Linux

One-time: allow your user to open Logitech hidraw devices (this is the only
step that needs root):

```sh
sudo cp linux/42-logitech-hidpp.rules /etc/udev/rules.d/
sudo udevadm control --reload-rules && sudo udevadm trigger
```

Then run from the tree, or install into a user prefix:

```sh
./linux/mxswitch.sh --info
# or:
install -m 755 linux/mxswitch.sh ~/.local/bin/mxswitch
mxswitch --info
```

### Python (any platform)

```sh
pip install hidapi        # not needed on Windows
python python/mxswitch.py --info
```

On Windows the Python version drives `hid.dll` and `setupapi.dll` through
`ctypes` and needs no packages at all. Elsewhere it falls back to `hidapi`.

## Binding a shortcut

**Windows** — `windows/mxswitch.ahk` (AutoHotkey v2). Set `SCRIPT` at the top and
drop a shortcut to it in `shell:startup`. The main hotkey runs `-Toggle`, so
your channel pair lives in the config file rather than in the script. It also
binds a variant that switches *and* locks the workstation, which is the one worth
using day to day. AHK hotkeys do not fire on the lock screen, so trigger the
return switch from the other machine rather than expecting to wake Windows and
press the key.

**macOS** — BetterTouchTool: keyboard shortcut trigger → *Execute Terminal
Command / Shell Script (async, non-blocking)* → e.g.
`$HOME/.local/bin/mxswitch --toggle` or the absolute path to `./mxswitch` in this
repo. Raycast script commands and Automator Quick Actions work the same way.

**Linux** — GNOME Settings → Keyboard → Custom Shortcuts, or the KDE equivalent,
pointing at e.g. `$HOME/.local/bin/mxswitch --toggle`.

Prefer `--toggle` to a hardcoded channel: pairing a third host later does not
silently change which channel a bare `2` means.

## Prior art

This is not the first tool to send `ChangeHost`, and if one of these fits you
better, use it:

- [marcelhoffs/input-switcher](https://github.com/marcelhoffs/input-switcher) —
  Windows/Linux scripts around a vendored `hidapitester`; switches a keyboard and
  mouse together. `mxswitch --all` can also move both; its default is to move
  the preferred device, usually the mouse.
- [meliksah/lcs](https://github.com/meliksah/lcs) — GUI channel switcher.
- [omar16100/logi_mx_auto_switch](https://github.com/omar16100/logi_mx_auto_switch) —
  makes a mouse follow its keyboard automatically on macOS.
- [boyvanamstel/SwitchMX](https://github.com/boyvanamstel/SwitchMX) — macOS, MX Master 3S.
- [Solaar](https://github.com/pwr-Solaar/Solaar) and
  [Mouser](https://github.com/TomBadash/Mouser) — full Options+ replacements that
  cover `ChangeHost` among much else.

What is different here: no vendored binaries anywhere, one self-contained file
per platform, and runtime feature-index discovery rather than hardcoded offsets.

## License

MIT. See `LICENSE`.
