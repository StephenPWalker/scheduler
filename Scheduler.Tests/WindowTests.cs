using Scheduler;

namespace Scheduler.Tests;

internal static partial class Program
{
    private static void CheckMainWindowCounters(string directory)
    {
        var path = Path.Combine(directory, "tasks.json");
        var schedule = new TaskSchedule(new TaskStore(path));
        schedule.Add(Task("Stored task", 705, 735));
        using var window = new SchedulerForm(schedule);
        window.Show();
        Application.DoEvents();
        Check(HasLabel(window, "0 of 1 complete"), "Footer counts an incomplete stored task");
        var add = AllControls(window).OfType<PillButton>().Single(button => button.Name == "AddEntry");
        var addPosition = add.PointToScreen(Point.Empty);
        window.Timeline.AutoScrollPosition = new Point(0, window.Timeline.TimeY(11 * 60));
        Application.DoEvents();
        var scroll = window.Timeline.AutoScrollPosition;
        Check(add.PointToScreen(Point.Empty) == addPosition, "Add Entry stays fixed while the timeline scrolls");
        var originalBounds = window.TaskRows.Single().Bounds;
        window.TaskRows.Single().StatusButton.PerformClick();
        Application.DoEvents();
        Check(schedule.Tasks.Single().IsDone && HasLabel(window, "1 of 1 complete") && window.TaskRows.Single().StatusButton.Text == "Done", "Status action updates the model, footer and card together");
        Check(window.TaskRows.Single().Bounds == originalBounds && window.Timeline.AutoScrollPosition == scroll, "Status changes do not change card position or scroll");
        Check(new TaskStore(path).Load().Single().IsDone, "Main-window status changes reach the saved task file");
        window.TaskRows.Single().StatusButton.PerformClick();
        Application.DoEvents();
        Check(!schedule.Tasks.Single().IsDone && HasLabel(window, "0 of 1 complete"), "Marking a completed task as Todo updates the footer");
        window.TaskRows.Single().DeleteButton.PerformClick();
        Application.DoEvents();
        Check(schedule.Tasks.Count == 0 && !window.TaskRows.Any() && HasLabel(window, "Your day, at your pace."), "Deleting the last stored task restores the empty footer and grid");
        Check(new TaskStore(path).Load().Count == 0, "The empty schedule after deletion is persisted");
        window.Close();
    }

    private static bool HasLabel(Control root, string text) => AllControls(root).OfType<Label>().Any(label => label.Text == text);
}
