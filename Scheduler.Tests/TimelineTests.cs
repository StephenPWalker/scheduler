using Scheduler;

namespace Scheduler.Tests;

internal static partial class Program
{
    private static void CheckTimelineBehaviour()
    {
        using var host = new Form { Text = "Timeline tests", AutoScaleMode = AutoScaleMode.None, ShowInTaskbar = false };
        var scale = host.DeviceDpi / 96f;
        host.ClientSize = new Size((int)(600 * scale), (int)(380 * scale));
        var timeline = new ScheduleTimeline { Dock = DockStyle.Fill };
        host.Controls.Add(timeline);
        host.Show();
        Application.DoEvents();
        timeline.SetTasks([], _ => { }, _ => { });
        Check(!timeline.TaskRows.Any() && timeline.VerticalScroll.Visible && !timeline.HorizontalScroll.Visible, "An empty timeline has a full scrollable day and no cards or horizontal scrollbar");
        var dayHeight = timeline.AutoScrollMinSize.Height;
        var hourHeight = timeline.HourHeight;
        Check(Enumerable.Range(0, 24).All(hour => timeline.TimeY((hour + 1) * 60) - timeline.TimeY(hour * 60) == hourHeight), "Every hour has the same fixed height from midnight to day end");
        using (var bitmap = Snapshot(timeline))
            Check(HasTimeInk(bitmap, (int)(60 * scale)), "Hour labels are actually painted on the left even with no tasks");

        var tasks = new[]
        {
            Task("Quarter", 0, 15), Task("Half", 15, 45),
            Task("Long title " + new string('x', 45), 45, 105) with { Description = "Multiline notes\nNext line", IsDone = true },
            Task("Crossing", 705, 735), Task("Day end", 1424, 1439)
        };
        Guid toggled = default, deleted = default;
        timeline.SetTasks(tasks, id => toggled = id, id => deleted = id);
        Application.DoEvents();
        var cards = timeline.TaskRows.ToArray();
        Check(cards.Length == tasks.Length && cards.Select(card => card.Left).Distinct().Count() == 1 && cards.Select(card => card.Width).Distinct().Count() == 1,
            "All real task cards share exactly one column and width");
        Check(cards[0].Bottom < cards[1].Top && cards[1].Bottom < cards[2].Top, "Adjacent periods have a visible card gap");
        Check(cards[1].Height > cards[0].Height && cards[2].Height > cards[1].Height, "15, 30 and 60 minute cards have increasing proportional heights");
        Check(cards[3].Top < timeline.TimeY(720) && cards[3].Bottom > timeline.TimeY(720), "Hour-crossing card spans its fixed hour line");
        Check(cards[^1].Bottom < timeline.TimeY(1440) && timeline.AutoScrollMinSize.Height == dayHeight, "The last possible 15-minute card fits inside the unchanged full day");
        Check(cards.All(card => card.AccessibleName!.Contains(card.Task.Title) && card.StatusButton.AccessibleName!.Contains(card.Task.Title) && card.DeleteButton.AccessibleName!.Contains(card.Task.Title)),
            "Cards and action buttons expose task-specific accessible names");
        Check(cards[2].StatusButton.Text == "Done" && cards[2].StatusButton.Symbol == ButtonSymbol.Check, "Completed cards expose Done text and a checkmark");
        cards[0].StatusButton.PerformClick();
        cards[1].DeleteButton.PerformClick();
        Check(toggled == tasks[0].Id && deleted == tasks[1].Id, "Status and delete actions route the correct task IDs");
        foreach (var card in cards)
        {
            using var bitmap = Snapshot(card);
            Check(bitmap.GetPixel(0, 0).ToArgb() == Palette.Background.ToArgb(), "Compact and long cards paint their outer corners cleanly");
        }

        timeline.AutoScrollPosition = new Point(0, timeline.TimeY(12 * 60));
        Application.DoEvents();
        var scroll = timeline.AutoScrollPosition;
        var oldPositions = cards.ToDictionary(card => card.Task.Id, card => card.Bounds);
        timeline.SetTasks(tasks.Select(task => task with { IsDone = !task.IsDone }).ToArray(), _ => { }, _ => { });
        Application.DoEvents();
        Check(timeline.AutoScrollPosition == scroll, "Refreshing completion states preserves the scroll position");
        Check(cards.All(card => card.IsDisposed), "Replacing the task list disposes every old card");
        Check(timeline.TaskRows.All(card => card.Bounds == oldPositions[card.Task.Id]), "Status changes do not move or resize any task");
        using (var bitmap = Snapshot(timeline))
            Check(HasTimeInk(bitmap, (int)(60 * scale)), "Left-hand time labels remain visible after scrolling");
        foreach (var width in new[] { 420, 700, 552 })
        {
            host.ClientSize = new Size((int)(width * scale), host.ClientSize.Height);
            Application.DoEvents();
            Check(!timeline.HorizontalScroll.Visible && timeline.HourHeight == hourHeight, "Changing viewport width keeps hours fixed and avoids horizontal scrolling");
            Check(timeline.TaskRows.All(card => card.ClientRectangle.Contains(card.StatusButton.Bounds) && card.ClientRectangle.Contains(card.DeleteButton.Bounds)), "Task actions stay inside cards after a viewport resize");
        }
        timeline.AutoScrollPosition = new Point(0, int.MaxValue);
        Application.DoEvents();
        var visibleDay = new Rectangle(0, -timeline.AutoScrollPosition.Y, timeline.ClientSize.Width, timeline.ClientSize.Height);
        Check(visibleDay.IntersectsWith(timeline.TaskRows.Last().Bounds) && visibleDay.Bottom >= timeline.TimeY(1440), "The scroll viewport reaches the final task and the end of the day");
        var beforeClearing = timeline.TaskRows.ToArray();
        timeline.SetTasks([], _ => { }, _ => { });
        Application.DoEvents();
        Check(!timeline.TaskRows.Any() && beforeClearing.All(card => card.IsDisposed) && timeline.AutoScrollMinSize.Height == dayHeight,
            "Clearing tasks disposes cards and preserves the time grid");
        host.Close();
    }

    private static bool HasTimeInk(Bitmap bitmap, int gutterWidth)
    {
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < Math.Min(gutterWidth, bitmap.Width); x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.A > 0 && pixel.R < 100 && pixel.G < 130 && pixel.B < 100) return true;
            }
        return false;
    }

    private static Bitmap Snapshot(Control control)
    {
        var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        return bitmap;
    }
}
