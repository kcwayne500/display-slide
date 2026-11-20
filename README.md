# Display Slide

A small Windows 11 tray utility for moving a laptop screen left or right beneath external monitors in an extended desktop.

## Run

Download `Display-Slide.exe` from the [latest release](https://github.com/kcwayne500/display-slide/releases/latest) and open it. Display Slide stays in the system tray. Launch it again after signing in to enable the shortcut for that session.

1. Press **Ctrl+Alt+L** to open the popup.
2. Drag the slider or use **← / →** to line up the laptop in the monitor preview.
3. Choose **Test** to try the position, then **Apply** to save it in Windows. **Enter** runs the highlighted action.
4. Use **Cancel** or **Esc** to restore the position from when you opened the popup.

Display Slide works with the built-in screen enabled, at least one external monitor, and Windows set to **Extend**. Set an external monitor as the main display. If the shortcut is taken, open Display Slide from its tray icon.

## Build

From the repository root in PowerShell on Windows 11, run:

```powershell
.\Build.ps1
```

The script uses the Windows .NET Framework C# compiler and writes `dist/Display Slide.exe`. To regenerate the included icon, run `python tools/GenerateIcon.py` with Pillow installed.
