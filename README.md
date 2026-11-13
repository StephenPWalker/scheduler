# Scheduler

A small Windows desktop task scheduler, written entirely in C# using Windows
Forms. MIT licensed, with no third-party packages or downloaded assets.

## Run

Requires Windows 10/11 and the .NET 10 SDK to build:

```powershell
dotnet run --project Scheduler
```

## Build

From the repository root, with any running copy closed:

```powershell
dotnet build Scheduler/Scheduler.csproj -c Release
```

Run the built application:

```powershell
.\Scheduler\bin\Release\net10.0-windows\Scheduler.exe
```

To create a distributable release folder:

```powershell
dotnet publish Scheduler -c Release --self-contained false -o artifacts/Scheduler
```

Run `artifacts/Scheduler/Scheduler.exe`. The destination machine needs the
.NET 10 Windows Desktop Runtime. The app does not install a service, register
startup entries, or require administrator access.

## Use

- The application opens at the top right of the primary screen's usable desktop
  area. Drag the Windows title bar to move it. It appears in the taskbar and
  Alt+Tab, and has normal minimize and close buttons. It behaves like a normal
  application window when brought to the foreground.
- Click **Add Entry** to open a separate window. Enter
  a title, start/end times, an optional description and one of six colours.
  Times use the 24-hour clock. A period must end after it starts on the same day.
  A task must last at least 15 minutes. Tasks can cross hour boundaries (for
  example, 11:45–12:15). Overlapping time ranges are rejected.
- Click **Todo** / **Done** to switch status, or the trash button to delete a task.
  Hover over a task to read its full title and description.
- A new schedule starts with no tasks. The scrollable daily grid keeps the
  hours fixed on the left from 00:00 to 23:59, with faint 15-minute guides.
  There are no placeholder cards or sample entries. Cards occupy one column,
  positioned by their actual start and end times. Two 30-minute cards or four
  15-minute cards fit into an hour. The add button stays visible while scrolling.
- Close the application with the title-bar **×** or **Alt+F4**. Closing also
  closes any open entry window. Launch it again to reopen it.
  A second launch leaves the existing instance and its task file alone.

Tasks and completion states save immediately to
`%LOCALAPPDATA%/DesktopScheduler/tasks.json`. The same schedule remains until
you change it; nothing resets or disappears at midnight. There are no accounts,
network requests, reminders, calendar integrations, or automatic task deletion.

Saves use a temporary file and an atomic replacement. A failed save leaves the
displayed schedule unchanged. Invalid task files produce an error and are left
untouched rather than being replaced with an empty schedule.

## Application icon

The original MIT-licensed [SVG](Scheduler/Assets/scheduler.svg) and
[Windows ICO](Scheduler/Assets/scheduler.ico) use the app's forest-green and
ivory colours. The icon appears in Explorer, the taskbar, Alt+Tab and both
window title bars. The ICO includes 16, 20, 24, 32, 40, 48, 64, 96, 128 and
256px versions with transparent backgrounds. Both assets ship in the release's
`Assets` folder.

## Checks

The dependency-free C# check runner covers time validation, ordering,
persistence, failed saves, malformed files and the real Windows Forms controls:

```powershell
dotnet run --project Scheduler.Tests
```

UI checks include the title bar, initial position, minimize/restore/close,
foreground behaviour, empty schedules, the 23:59 boundary and button
background rendering at the current Windows display scale.

See [LICENSE](LICENSE) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
