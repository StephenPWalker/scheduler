# Scheduler tests

All test code and assertions live in this project. It uses a small C# console
runner with no third-party packages. The production application contains no
test-only code paths or assertions.

Run from the repository root on Windows with the .NET 10 SDK:

```powershell
dotnet run --project Scheduler.Tests -c Release
```

The default run opens and closes real test windows, without modal error dialogs.
It uses its own temporary
task files and never opens or changes your saved schedule. Each test run removes
its temporary directory. Independent groups continue after a failure, print a
summary and return a nonzero exit code if any group fails.

## Additional modes

Run storage, validation, scheduling and resource tests without opening windows:

```powershell
dotnet run --project Scheduler.Tests -c Release -- --model-only
```

Run the full suite with Windows' 96-DPI coordinate mode, useful on a machine
whose normal display scale is higher:

```powershell
dotnet run --project Scheduler.Tests -c Release -- --dpi-unaware
```

The normal run uses the monitor's actual DPI. These two modes exercise 100%
and the native display scale; they do not simulate every monitor configuration.

An optional directory argument captures the existing workflow's preview images.
Omit it for a normal run that leaves no preview files:

```powershell
dotnet run --project Scheduler.Tests -c Release -- artifacts/test-previews
```

Close an application instance launched from the same build directory before
rebuilding it. To keep a running build open, choose a separate build output:

```powershell
dotnet build Scheduler.slnx -c Release -m:1 -p:BaseOutputPath=bin/TestsReview/
dotnet Scheduler.Tests/bin/TestsReview/Release/net10.0-windows/Scheduler.Tests.dll
```

## Automated coverage

| Area | Behaviour covered |
| --- | --- |
| Validation | IDs; null, blank and Unicode titles; exact text limits; optional description; palette and malformed colours; every start minute against nine durations; every clock label; legacy short entries. |
| Schedule | Chronological ordering; seven conflict patterns; adjacency; completed periods; duplicate IDs; rejected mutations preserving files; read-only task lists; unknown IDs; freed periods; legacy ordering. |
| Storage | New and nested paths; empty schedules; repeated replacement; Unicode and escaped text; malformed and wrongly typed JSON; invalid fields; duplicate IDs; UTF-8 BOM; unknown properties; locks; read-only destinations; failed parent creation; recovery; temporary-file cleanup. |
| Main window | Empty grid; first/last task; start position; taskbar styles; native title-bar dragging; minimize, restore and close; foreground behaviour; one editor at a time; fixed Add Entry button; counters; persisted status and deletion. |
| Editor | Defaults; field limits; 24-hour controls; text trimming; all six colour selections; unique IDs; optional notes; minimum duration; reversed and overnight periods; cancellation; Enter/Escape; overlap errors; IO/access errors; field retention and retry. |
| Timeline | Fixed hours; empty-day scrolling; hour-label painting; quarter/half/full-hour cards; cross-hour periods; 23:59 boundary; one column; adjacent gaps; scroll retention; stable card positions; resizing; disposal and clearing. |
| Buttons | Hover, pressed, released and disabled states; keyboard press indication; click routing; opaque corners; all icon variants; accessible push-button role and task-specific names. |
| Icons | Embedded ICO header and frame bounds; ten image resolutions; PNG decoding and transparency; independent icon lifetimes; copied SVG/ICO assets; SVG title; matching packaged/embedded bytes. |

The exhaustive duration sweep evaluates 12,960 start/duration combinations;
the separate formatting sweep covers all 1,440 minutes. These are grouped
assertions, rather than thousands of duplicate console lines.

## Manual Windows integration checks

The suite does not claim 100% code or operating-system coverage. Check these
on a normal Windows desktop when preparing a release:

- Launch the actual executable and launch a second copy: it should keep one
  instance and show the already-running message.
- With a disposable Windows account/profile, try a malformed or inaccessible
  startup task file: the app should show its startup error and preserve it.
- With a disposable task file made read-only, try a status change or delete:
  the main window should display its error dialog and keep the task unchanged.
  Storage rollback and retry are automated separately; modal dialogs remain manual.
- Check Explorer/taskbar/Alt+Tab icons and physically drag the title bar.
- Check keyboard navigation and screen-reader announcements with the user's
  actual assistive tools.
- Move between monitors with different scaling, and change display resolution
  or taskbar placement. The automated runs exercise fixed DPI configurations.

These checks need Windows shell or account state and deliberately do not modify
the user's profile or display settings from the test runner.
