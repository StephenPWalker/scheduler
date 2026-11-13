using System.Runtime.InteropServices;
using System.Text.Json;
using Scheduler;

namespace Scheduler.Tests;

internal static partial class Program
{
    private static int checks;

    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.SetHighDpiMode(args.Contains("--dpi-unaware") ? HighDpiMode.DpiUnaware : HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var testDirectory = Path.Combine(Path.GetTempPath(), "SchedulerChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);
        try
        {
            var failures = new List<string>();
            var groups = 0;
            void Run(string name, Action action)
            {
                groups++;
                Console.WriteLine($"\nGROUP: {name}");
                try { action(); }
                catch (Exception exception)
                {
                    failures.Add(name);
                    Console.Error.WriteLine($"FAIL: {name}\n{exception}");
                }
            }

            Run("Validation regression", CheckValidation);
            Run("Validation boundaries and every minute of the day", CheckValidationBoundaries);
            Run("Persistence regression", () => CheckPersistence(testDirectory));
            Run("Overlap, adjacency and rejected mutations", () => CheckScheduleBoundaries(Area(testDirectory, "schedule")));
            Run("Malformed file regression", () => CheckBadFiles(testDirectory));
            Run("Storage formats, replacements and compatibility", () => CheckStorageCases(Area(testDirectory, "storage")));
            Run("Locked file regression", () => CheckFailedSaves(testDirectory));
            Run("Storage failure and recovery", () => CheckStorageRecovery(Area(testDirectory, "recovery")));
            Run("Embedded and packaged icons", CheckIconAssets);
            if (!args.Contains("--model-only"))
            {
                Run("Window and full workflow regression", () => CheckWindows(testDirectory, args.FirstOrDefault(arg => !arg.StartsWith("--"))));
                Run("Editor fields, palette and cancellation", CheckEditorBehaviour);
                Run("Editor failures and retries", CheckEditorFailures);
                Run("Main window counters and scrolling", () => CheckMainWindowCounters(Area(testDirectory, "window-counters")));
                Run("Keyboard accept and cancel", CheckEditorKeyboard);
                Run("Timeline geometry, scrolling and disposal", CheckTimelineBehaviour);
                Run("Button rendering and interaction states", CheckButtonStates);
            }
            Console.WriteLine($"\n{checks} assertions passed; {groups - failures.Count}/{groups} groups passed.");
            if (failures.Count > 0) Console.Error.WriteLine("Failed groups: " + string.Join(", ", failures));
            return failures.Count == 0 ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            // Only delete the unique directory this run created under the OS temp directory.
            var fullPath = Path.GetFullPath(testDirectory);
            var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(fullPath).StartsWith("SchedulerChecks-"))
                throw new InvalidOperationException("Unexpected test cleanup path.");
            Directory.Delete(fullPath, recursive: true);
        }
    }

    private static string Area(string root, string name)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static ScheduleTask Task(string title = "Focus", int start = 9 * 60, int end = 10 * 60) => new()
    {
        Title = title, StartMinute = start, EndMinute = end,
        Description = "A little uninterrupted time", Colour = "#E3EDDF"
    };

    private static void CheckValidation()
    {
        var valid = Task();
        Check(valid.ValidationError() is null, "Normal task is valid");
        Check((valid with { Title = "  " }).ValidationError() is not null, "Blank title rejected");
        Check((valid with { Title = new string('a', 81) }).ValidationError() is not null, "Long title rejected");
        Check((valid with { Description = new string('a', 501) }).ValidationError() is not null, "Long description rejected");
        Check((valid with { EndMinute = valid.StartMinute }).ValidationError() is not null, "Zero duration rejected");
        Check((valid with { EndMinute = valid.StartMinute + 14 }).ValidationError() is not null, "Tasks shorter than 15 minutes rejected");
        Check((valid with { EndMinute = valid.StartMinute + 15 }).ValidationError() is null, "15 minute tasks accepted");
        Check(Task("Across noon", 11 * 60 + 45, 12 * 60 + 15).ValidationError() is null, "Task can span an hour boundary");
        Check((valid with { EndMinute = 60 }).ValidationError() is not null, "Reversed/overnight range rejected");
        Check((valid with { StartMinute = -1 }).ValidationError() is not null, "Negative time rejected");
        Check((valid with { EndMinute = 1440 }).ValidationError() is not null, "Out of range time rejected");
        Check((valid with { StartMinute = 0, EndMinute = 1439 }).ValidationError() is null, "Full day boundaries valid");
        
        foreach (var colour in new[] { "#000000", "#aBC123", "#FFFFFF" })
            Check((valid with { Colour = colour }).ValidationError() is null, "Hex colour accepted: " + colour);
        
        foreach (var colour in new[] { "red", "#GG0000", "#123", "0000000", "#1234567" })
            Check((valid with { Colour = colour }).ValidationError() is not null, "Invalid colour rejected: " + colour);
        
        Check(ScheduleTask.FormatTime(9 * 60 + 5) == "09:05", "Time is padded and 24 hour");
    }

    private static void CheckPersistence(string directory)
    {
        var store = new TaskStore(Path.Combine(directory, "tasks.json"));
        var schedule = new TaskSchedule(store);
        Check(schedule.Tasks.Count == 0, "First launch starts empty");

        var late = Task("Later", 15 * 60, 16 * 60) with { Colour = "#EBE3F4" };
        var early = Task("Earlier", 8 * 60, 9 * 60) with { Description = "Unicode ✓ café 日本語" };
        schedule.Add(late);
        schedule.Add(early);
        schedule.Add(Task("Adjacent", 9 * 60, 9 * 60 + 30));
        Check(schedule.Tasks[0].Id == early.Id && schedule.Tasks[^1].Id == late.Id, "Tasks sorted by time; adjacent tasks allowed");
        Expect<ArgumentException>(() => schedule.Add(Task("Overlapping", 8 * 60 + 30, 9 * 60 + 30)), "Overlapping time ranges rejected");
        Expect<ArgumentException>(() => schedule.Add(early), "Duplicate task rejected");
        
        schedule.Toggle(early.Id);
    
        var reloaded = new TaskSchedule(store);

        Check(reloaded.Tasks[0] == early with { IsDone = true }, "Text, colour, times and completion survive reload");
        reloaded.Toggle(early.Id);

        Check(!new TaskSchedule(store).Tasks[0].IsDone, "Done toggles back to Todo and persists");
        reloaded.Delete(early.Id);

        Check(new TaskSchedule(store).Tasks.All(task => task.Id != early.Id), "Deletion persists");
        Check(Directory.GetFiles(directory, "*.tmp").Length == 0, "Successful saves leave no temporary files");
    }

    private static void CheckBadFiles(string directory)
    {
        var path = Path.Combine(directory, "bad.json");
        File.WriteAllText(path, "not json");
        Expect<JsonException>(() => new TaskSchedule(new TaskStore(path)), "Broken JSON rejected");
        Check(File.ReadAllText(path) == "not json", "Broken JSON remains untouched");

        foreach (var contents in new[] { "null", "[null]", "[{}]", JsonSerializer.Serialize(new[] { Task() with { Colour = "invalid" } }) })
        {
            File.WriteAllText(path, contents);
            Expect<InvalidDataException>(() => new TaskSchedule(new TaskStore(path)), "Invalid stored task rejected");
            Check(File.ReadAllText(path) == contents, "Invalid task file remains untouched");
        }

        var duplicate = Task();
        File.WriteAllText(path, JsonSerializer.Serialize(new[] { duplicate, duplicate }));
        Expect<InvalidDataException>(() => new TaskSchedule(new TaskStore(path)), "Duplicate stored IDs rejected");
    }

    private static void CheckFailedSaves(string directory)
    {
        var path = Path.Combine(directory, "locked.json");
        var schedule = new TaskSchedule(new TaskStore(path));
        var task = Task();
        schedule.Add(task);
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            ExpectStorageFailure(() => schedule.Toggle(task.Id), "Locked file prevents save");
            Check(!schedule.Tasks[0].IsDone, "Failed toggle leaves state unchanged");
            ExpectStorageFailure(() => schedule.Delete(task.Id), "Locked file prevents delete");
            Check(schedule.Tasks.Count == 1, "Failed delete leaves task intact");
            ExpectStorageFailure(() => schedule.Add(Task("Another", 11 * 60, 12 * 60)), "Locked file prevents add");
            Check(schedule.Tasks.Count == 1, "Failed add leaves state unchanged");
        }

        Check(new TaskStore(path).Load().Single() == task, "Failed saves preserve original file");
        Check(Directory.GetFiles(directory, "*.tmp").Length == 0, "Failed saves clean temporary files");
    }

    private static void CheckWindows(string directory, string? screenshotDirectory)
    {
        var schedule = new TaskSchedule(new TaskStore(Path.Combine(directory, "ui.json")));
        using (var fresh = new SchedulerForm(schedule))
        {
            fresh.Show();
            Application.DoEvents();
            Check(schedule.Tasks.Count == 0 && !fresh.TaskRows.Any(), "Fresh schedule has no tasks or placeholder cards");
            var emptyDayHeight = fresh.Timeline.AutoScrollMinSize.Height;
            Check(fresh.Timeline.VerticalScroll.Visible && emptyDayHeight > fresh.Timeline.ClientSize.Height, "Fixed daily time grid remains scrollable when empty");
            if (screenshotDirectory is not null)
            {
                Directory.CreateDirectory(screenshotDirectory);
                Capture(fresh, Path.Combine(screenshotDirectory, "empty-schedule.png"));
            }

            AllControls(fresh).OfType<PillButton>().Single(button => button.Name == "AddEntry").PerformClick();
            Application.DoEvents();
            var firstEntry = Application.OpenForms.OfType<EntryForm>().Single();
            firstEntry.TitleInput.Text = "First task";
            firstEntry.StartInput.Value = DateTime.Today;
            firstEntry.EndInput.Value = DateTime.Today.AddMinutes(15);
            firstEntry.SaveButton.PerformClick();
            Application.DoEvents();
            Check(fresh.TaskRows.Count() == 1, "Adding the first task creates exactly one card");
            Check(schedule.Tasks.Single().StartMinute == 0, "A task can start at 00:00");
            Check(fresh.TaskRows.Single().Height < fresh.Timeline.HourHeight / 4, "15 minute card occupies one quarter of an hour, with a small gap");
            Check(fresh.Timeline.AutoScrollMinSize.Height == emptyDayHeight, "Adding a task does not change hourly grid spacing");
            
            fresh.TaskRows.Single().DeleteButton.PerformClick();
            Application.DoEvents();
            Check(!fresh.TaskRows.Any() && fresh.Timeline.AutoScrollMinSize.Height == emptyDayHeight, "Deleting the last task leaves the time grid unchanged");
            Check(new TaskStore(Path.Combine(directory, "ui.json")).Load().Count == 0, "Deleting all tasks persists an empty schedule");
            fresh.Close();
        }

        schedule.Add(Task("Morning focus", 0, 60) with { IsDone = true });
        schedule.Add(Task("Design review", 60, 120) with { Colour = "#DFEAF5", Description = "Walk through the new ideas" });
        schedule.Add(Task("Read a chapter", 14 * 60, 14 * 60 + 30) with { Colour = "#F7E3D5" });
        using var normalWindow = new Form { Text = "Scheduler check", ShowInTaskbar = false, Size = new Size(160, 100), Location = new Point(750, 300), StartPosition = FormStartPosition.Manual };
        normalWindow.Show();
        normalWindow.Activate();
        Application.DoEvents();
        using var widget = new SchedulerForm(schedule);
        widget.Show();
        Application.DoEvents();
        Check(!widget.TopMost && widget.ShowInTaskbar && widget.FormBorderStyle == FormBorderStyle.FixedSingle, "Scheduler is a normal application window with a taskbar entry");
        Check(widget.ControlBox && widget.MinimizeBox && !widget.MaximizeBox, "Native title bar provides close and minimize controls");
        
        var work = Screen.PrimaryScreen!.WorkingArea;
        Console.WriteLine($"Display: {widget.DeviceDpi} DPI, work area {work}, widget {widget.Size}");
        var inset = (int)Math.Round(18 * widget.DeviceDpi / 96f);
        Check(widget.Location == new Point(work.Right - widget.Width - inset, work.Top + inset), "Application opens at top right");
        
        var styles = GetWindowLong(widget.Handle, -20);
        Check((styles & 0x08000080) == 0, "Window can activate and appears in Alt+Tab");
       
        var captionPoint = new Point(widget.Left + (int)(100 * widget.DeviceDpi / 96f), widget.Top + (int)(15 * widget.DeviceDpi / 96f));
        var packedPoint = (nint)((captionPoint.Y << 16) | (captionPoint.X & 0xFFFF));
        Check(SendMessage(widget.Handle, 0x0084, 0, packedPoint) == 2, "Native title bar supports dragging the window");
        
        widget.Location = new Point(work.Left + inset, work.Top + inset);
        Application.DoEvents();
        Check(widget.Location == new Point(work.Left + inset, work.Top + inset), "Window can move away from its initial position");
        
        SendMessage(widget.Handle, 0x0112, 0xF020, 0); // SC_MINIMIZE
        Application.DoEvents();
        Check(widget.WindowState == FormWindowState.Minimized, "Application can be minimized");
        
        SendMessage(widget.Handle, 0x0112, 0xF120, 0); // SC_RESTORE
        Application.DoEvents();
        Check(widget.WindowState == FormWindowState.Normal, "Application can be restored");
        widget.BringToFront();
        Application.DoEvents();
        Check(IsAbove(widget.Handle, normalWindow.Handle), "Application can come to the foreground");
        Check(widget.TaskRows.Count() == 3, "Only the three actual tasks appear, with no empty slots between them");
        Check(widget.TaskRows.First().AccessibleName!.Contains("00:00"), "Schedule starts at midnight");
        
        var taskList = widget.Timeline;
        Check(!taskList.HorizontalScroll.Visible, "Task list fits its width at current display scale");
        Check(widget.TaskRows.All(row => row.ClientRectangle.Contains(row.StatusButton.Bounds)), "Status buttons fit task pills at current display scale");
        CheckButtonBackgrounds(widget);
        if (screenshotDirectory is not null)
        {
            Directory.CreateDirectory(screenshotDirectory);
            Capture(widget, Path.Combine(screenshotDirectory, "scheduler.png"));
        }

        var todo = widget.TaskRows.First(row => row.StatusButton.Text == "Todo");
        todo.StatusButton.PerformClick();
        Application.DoEvents();
        Check(schedule.Tasks.Count(task => task.IsDone) == 2, "Clicking Todo marks Done");
        
        widget.TaskRows.First(row => row.StatusButton.Text == "Done").StatusButton.PerformClick();
        Application.DoEvents();
        Check(schedule.Tasks.Count(task => task.IsDone) == 1, "Clicking Done marks Todo");
        
        widget.TaskRows.First().DeleteButton.PerformClick();
        Application.DoEvents();
        Check(schedule.Tasks.Count == 2, "Clicking the trash button deletes the task");
        
        var add = AllControls(widget).OfType<PillButton>().Single(button => button.AccessibleName == "Add entry");
        add.PerformClick();
        Application.DoEvents();
        var entry = Application.OpenForms.OfType<EntryForm>().Single();
        Check(entry.Visible && !entry.TopMost, "Add entry opens a separate normal window");
        
        CheckButtonBackgrounds(entry);
        add.PerformClick();
        Check(Application.OpenForms.OfType<EntryForm>().Count() == 1, "Add entry reuses existing entry window");
        
        entry.SaveButton.PerformClick();
        Check(entry.ErrorLabel.Text.Length > 0 && schedule.Tasks.Count == 2, "Blank title keeps editor open with validation");
        
        entry.TitleInput.Text = "Write notes";
        entry.EndInput.Value = entry.StartInput.Value.AddMinutes(-1);
        entry.SaveButton.PerformClick();
        
        Check(!entry.IsDisposed && schedule.Tasks.Count == 2, "Invalid period keeps editor open");
        entry.EndInput.Value = entry.StartInput.Value.AddMinutes(45);
        entry.DescriptionInput.Text = "Capture the useful bits";
        AllControls(entry).OfType<PillButton>().Single(button => button.Name == "Lavender").PerformClick();
        if (screenshotDirectory is not null) Capture(entry, Path.Combine(screenshotDirectory, "add-entry.png"));
        
        entry.SaveButton.PerformClick();
        Application.DoEvents();
        Check(entry.IsDisposed && schedule.Tasks.Count == 3, "Valid entry saves and closes editor");
        
        var saved = new TaskStore(Path.Combine(directory, "ui.json")).Load().Single(task => task.Title == "Write notes");
        Check(saved.Colour == "#EBE3F4" && saved.EndMinute - saved.StartMinute == 45, "Colour and selected period persist from editor");
        Check(widget.TaskRows.Count() == 3, "New task appears in widget");
        add.PerformClick();
        Application.DoEvents();
        var lateEntry = Application.OpenForms.OfType<EntryForm>().Single();
        lateEntry.TitleInput.Text = "End of day";
        lateEntry.StartInput.Value = DateTime.Today.AddHours(23);
        lateEntry.EndInput.Value = DateTime.Today.AddHours(23).AddMinutes(59);
        lateEntry.SaveButton.PerformClick();
        Application.DoEvents();
        Check(schedule.Tasks.Single(task => task.Title == "End of day").EndMinute == 1439, "A task ending at 23:59 saves correctly");
        
        var existingBounds = widget.TaskRows.Single(row => row.Task.Title == "Read a chapter").Bounds;
        add.PerformClick();
        Application.DoEvents();
        var acrossNoon = Application.OpenForms.OfType<EntryForm>().Single();
        acrossNoon.TitleInput.Text = "Across noon";
        acrossNoon.StartInput.Value = DateTime.Today.AddHours(11).AddMinutes(45);
        acrossNoon.EndInput.Value = DateTime.Today.AddHours(12).AddMinutes(15);
        acrossNoon.SaveButton.PerformClick();
        Application.DoEvents();
        var crossing = widget.TaskRows.Single(row => row.Task.Title == "Across noon");
        Check(crossing.Top < widget.Timeline.TimeY(12 * 60) && crossing.Bottom > widget.Timeline.TimeY(12 * 60), "11:45–12:15 card spans the fixed noon line");
        Check(crossing.Height < widget.Timeline.HourHeight / 2 && crossing.Height > widget.Timeline.HourHeight / 4, "30 minute card uses half an hour of height");
        Check(widget.TaskRows.Select(row => row.Left).Distinct().Count() == 1, "All tasks share one fixed task column");
        Check(widget.TaskRows.Single(row => row.Task.Title == "Read a chapter").Bounds == existingBounds, "Adding an earlier task does not move an existing task or its time");
        for (var quarter = 0; quarter < 4; quarter++)
        {
            add.PerformClick();
            Application.DoEvents();
            var quarterEntry = Application.OpenForms.OfType<EntryForm>().Single();
            quarterEntry.TitleInput.Text = $"Quarter {quarter + 1}";
            quarterEntry.StartInput.Value = DateTime.Today.AddHours(10).AddMinutes(quarter * 15);
            quarterEntry.EndInput.Value = quarterEntry.StartInput.Value.AddMinutes(15);
            quarterEntry.SaveButton.PerformClick();
            Application.DoEvents();
        }

        var quarters = widget.TaskRows.Where(row => row.Task.Title.StartsWith("Quarter ")).OrderBy(row => row.Top).ToArray();
        Check(quarters.Length == 4 && quarters.Zip(quarters.Skip(1)).All(pair => pair.First.Bottom < pair.Second.Top), "Four 15 minute cards fit into one hour without overlap");
        Check(quarters[0].Top >= widget.Timeline.TimeY(10 * 60) && quarters[^1].Bottom <= widget.Timeline.TimeY(11 * 60), "All four quarter-hour cards stay between the 10:00 and 11:00 lines");
        
        add.PerformClick();
        Application.DoEvents();
        var conflicting = Application.OpenForms.OfType<EntryForm>().Single();
        conflicting.TitleInput.Text = "Conflicting task";
        conflicting.StartInput.Value = DateTime.Today.AddHours(11).AddMinutes(50);
        conflicting.EndInput.Value = DateTime.Today.AddHours(12).AddMinutes(20);
        conflicting.SaveButton.PerformClick();
        Check(!conflicting.IsDisposed && conflicting.ErrorLabel.Text.Contains("overlaps"), "Editor explains an overlapping time range and keeps it unsaved");
        
        conflicting.Close();
        if (screenshotDirectory is not null)
        {
            widget.Timeline.AutoScrollPosition = new Point(0, widget.Timeline.TimeY(10 * 60) - 30);
            Application.DoEvents();
            Capture(widget, Path.Combine(screenshotDirectory, "timeline.png"));
        }

        add.PerformClick();
        Application.DoEvents();
        var openEntry = Application.OpenForms.OfType<EntryForm>().Single();
        SendMessage(widget.Handle, 0x0112, 0xF060, 0); // SC_CLOSE: same command as title-bar X / Alt+F4.
        Application.DoEvents();
        Check(widget.IsDisposed && openEntry.IsDisposed, "System close exits scheduler and closes its entry window");
    }

    private static void CheckButtonBackgrounds(Form form)
    {
        var buttons = AllControls(form).OfType<PillButton>().ToArray();
        foreach (var button in buttons)
        {
            using var bitmap = new Bitmap(button.Width, button.Height);
            button.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            Check(button.BackColor.A == 255 && bitmap.GetPixel(0, 0).ToArgb() == button.BackColor.ToArgb(),
                $"Button {button.Name} paints opaque corners without black artifacts");
        }
    }

    private static IEnumerable<Control> AllControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in AllControls(child)) yield return descendant;
        }
    }

    private static bool IsAbove(nint upper, nint lower)
    {
        for (var handle = GetWindow(lower, 3); handle != 0; handle = GetWindow(handle, 3))
            if (handle == upper) return true;

        return false;
    }

    private static void Capture(Form form, string path)
    {
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void Check(bool result, string description)
    {
        if (!result) throw new InvalidOperationException("FAILED: " + description);
        
        checks++;
        Console.WriteLine("PASS: " + description);
    }

    private static void Expect<T>(Action action, string description) where T : Exception
    {
        try { action(); }
        catch (T) { Check(true, description); return; }
        throw new InvalidOperationException("FAILED: " + description);
    }

    private static void ExpectStorageFailure(Action action, string description)
    {
        try { action(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Check(true, description);
            return;
        }

        throw new InvalidOperationException("FAILED: " + description);
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint handle, int index);
    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint handle, uint command);
    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint handle, int message, nint wParam, nint lParam);
}
