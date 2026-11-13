using System.Drawing.Drawing2D;

namespace Scheduler;

internal sealed class TaskRow : UserControl
{
    private readonly ScheduleTask task;
    internal ScheduleTask Task => task;
    private readonly ToolTip tooltip = new();
    internal PillButton StatusButton { get; }
    internal PillButton DeleteButton { get; }

    public TaskRow(ScheduleTask task, Action<Guid> toggle, Action<Guid> delete)
    {
        this.task = task;
        DoubleBuffered = true;
        BackColor = Palette.Background;
        Size = new Size(496, 62);
        Margin = new Padding(0, 0, 0, 8);
        AccessibleName = $"{task.Title}, {ScheduleTask.FormatTime(task.StartMinute)} to {ScheduleTask.FormatTime(task.EndMinute)}";
        var surface = ColorTranslator.FromHtml(task.Colour);
        StatusButton = new PillButton
        {
            Name = "Status", Text = task.IsDone ? "Done" : "Todo", Size = new Size(76, 32),
            Symbol = task.IsDone ? ButtonSymbol.Check : ButtonSymbol.None,
            BackColor = surface,
            Fill = task.IsDone ? Palette.Accent : Color.FromArgb(253, 254, 250),
            TextColour = task.IsDone ? Color.White : Palette.Accent,
            AccessibleName = task.IsDone ? $"Mark {task.Title} as todo" : $"Mark {task.Title} as done"
        };
        
        StatusButton.FlatAppearance.BorderSize = task.IsDone ? 0 : 1;
        StatusButton.FlatAppearance.BorderColor = Color.FromArgb(174, 194, 174);

        DeleteButton = new PillButton
        {
            Name = "Delete", Symbol = ButtonSymbol.Delete, Size = new Size(32, 32),
            BackColor = surface, Fill = Color.FromArgb(250, 241, 239), TextColour = Color.FromArgb(147, 70, 67),
            AccessibleName = $"Delete {task.Title}"
        };

        StatusButton.Click += (_, _) => toggle(task.Id);
        DeleteButton.Click += (_, _) => delete(task.Id);
        tooltip.SetToolTip(this, $"{ScheduleTask.FormatTime(task.StartMinute)}–{ScheduleTask.FormatTime(task.EndMinute)}\n{task.Title}\n{task.Description}");
        tooltip.SetToolTip(DeleteButton, "Delete task");
        tooltip.SetToolTip(StatusButton, "Click to switch between Todo and Done");
        Controls.Add(StatusButton);
        Controls.Add(DeleteButton);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        var scale = DeviceDpi / 96f;
        int Px(int value) => (int)Math.Round(value * scale);

        var buttonTop = Math.Max(0, Math.Min(Px(12), (Height - Px(32)) / 2));
        if (StatusButton is not null) StatusButton.Location = new Point(Width - Px(125), buttonTop);
        
        if (Controls.Count > 0) Controls[^1].Location = new Point(Width - Px(42), buttonTop);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = DeviceDpi / 96f;
        int Px(int value) => (int)Math.Round(value * scale);
        var pill = new Rectangle(1, 1, Width - 2, Height - 2);
        using var shape = Palette.Rounded(pill, Math.Min(Px(18), pill.Height / 2));
        using var fill = new SolidBrush(ColorTranslator.FromHtml(task.Colour));
        g.FillPath(fill, shape);

        using var smallFont = new Font(Font.FontFamily, 8);
        using var titleFont = new Font(Font.FontFamily, 9.5f, task.IsDone ? FontStyle.Strikeout : FontStyle.Bold);
        var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        var compact = Height < Px(56);
        var textBounds = new Rectangle(Px(13), compact ? (Height - Px(22)) / 2 : Px(10), Math.Max(0, Width - Px(150)), Px(22));
        var title = compact && task.Description.Length > 0 ? $"{task.Title} — {task.Description.ReplaceLineEndings(" ")}" : task.Title;
        TextRenderer.DrawText(g, title, titleFont, textBounds, task.IsDone ? Palette.Muted : Palette.Ink, flags);
        if (!compact)
        {
            textBounds.Y = Px(34);
            textBounds.Height = Px(18);
            TextRenderer.DrawText(g, task.Description.Length == 0 ? "—" : task.Description.ReplaceLineEndings(" "), smallFont, textBounds, Palette.Muted, flags);
            if (Height >= Px(84))
            {
                textBounds.Y = Height - Px(24);
                TextRenderer.DrawText(g, $"{ScheduleTask.FormatTime(task.StartMinute)}–{ScheduleTask.FormatTime(task.EndMinute)}", smallFont, textBounds, Palette.Muted, flags);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) tooltip.Dispose();
        base.Dispose(disposing);
    }
}
