namespace Scheduler;

internal sealed class ScheduleTimeline : FlowLayoutPanel
{
    private readonly TimelineCanvas canvas = new();
    internal IEnumerable<TaskRow> TaskRows => canvas.Controls.OfType<TaskRow>();
    internal int HourHeight => canvas.HourHeight;
    internal int TimeY(int minute) => canvas.TimeY(minute);

    public ScheduleTimeline()
    {
        AutoScroll = true;
        FlowDirection = FlowDirection.TopDown;
        WrapContents = false;
        Padding = Padding.Empty;
        BackColor = Palette.Background;
        Controls.Add(canvas);
    }

    internal void SetTasks(IReadOnlyList<ScheduleTask> tasks, Action<Guid> toggle, Action<Guid> delete)
    {
        var scroll = -AutoScrollPosition.Y;
        canvas.SuspendLayout();
        foreach (var card in canvas.Controls.Cast<Control>().ToArray()) card.Dispose();
        foreach (var task in tasks)
        {
            var card = new TaskRow(task, toggle, delete);
            if (DeviceDpi != 96) card.Scale(new SizeF(DeviceDpi / 96f, DeviceDpi / 96f));
            canvas.Controls.Add(card);
        }

        canvas.ResumeLayout(true);
        PerformLayout();
        AutoScrollPosition = new Point(0, scroll);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        // Reserve the scrollbar before adding the tall canvas to avoid briefly
        // introducing a horizontal scrollbar during Windows' layout passes.
        if (canvas is not null)
        {
            var width = Math.Max(1, ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2);
            var height = canvas.TimeY(1440) + (int)Math.Round(18 * DeviceDpi / 96f);
            if (AutoScrollMinSize.Height != height) AutoScrollMinSize = new Size(0, height);
            if (canvas.Size != new Size(width, height)) canvas.Size = new Size(width, height);
        }

        base.OnLayout(e);
    }

    private sealed class TimelineCanvas : Panel
    {
        private int Px(int value) => (int)Math.Round(value * DeviceDpi / 96f);
        internal int HourHeight => Px(160);
        internal int TimeY(int minute) => Px(12) + (int)Math.Round(minute * HourHeight / 60d);

        public TimelineCanvas()
        {
            DoubleBuffered = true;
            BackColor = Palette.Background;
            Margin = Padding.Empty;
            AccessibleName = "Daily timeline, 00:00 to 23:59";
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            foreach (var card in Controls.OfType<TaskRow>())
            {
                var top = TimeY(card.Task.StartMinute);
                var height = Math.Max(Px(36), TimeY(card.Task.EndMinute) - top - Px(4));
                card.Bounds = new Rectangle(Px(69), top + Px(2), Math.Max(1, Width - Px(75)), height);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var hourPen = new Pen(Palette.Line, Math.Max(1, Px(1)));
            using var quarterPen = new Pen(Color.FromArgb(237, 239, 232));
            using var timeFont = new Font(Font.FontFamily, 9, FontStyle.Bold);
            using var endFont = new Font(Font.FontFamily, 8);
            for (var minute = 0; minute <= 1440; minute += 15)
            {
                var y = TimeY(minute);
                if (y < e.ClipRectangle.Top - Px(24) || y > e.ClipRectangle.Bottom + Px(24)) continue;
                var hour = minute % 60 == 0;
                e.Graphics.DrawLine(hour ? hourPen : quarterPen, Px(65), y, Width, y);
                if (hour && minute < 1440)
                    TextRenderer.DrawText(e.Graphics, ScheduleTask.FormatTime(minute), timeFont,
                        new Rectangle(0, y - Px(10), Px(60), Px(22)), Palette.Ink,
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
            }
            
            TextRenderer.DrawText(e.Graphics, "23:59", endFont,
                new Rectangle(0, TimeY(1440) - Px(10), Px(60), Px(22)), Palette.Muted,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
        }
    }
}
