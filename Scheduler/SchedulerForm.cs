namespace Scheduler;

internal sealed class SchedulerForm : Form
{
    private readonly TaskSchedule schedule;
    private readonly Icon appIcon = AppIcon.Create();
    private readonly ScheduleTimeline timeline = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly Label count = new() { AutoSize = true, ForeColor = Palette.Muted, Anchor = AnchorStyles.Left };
    private EntryForm? entryForm;
    internal IEnumerable<TaskRow> TaskRows => timeline.TaskRows;
    internal ScheduleTimeline Timeline => timeline;

    public SchedulerForm(TaskSchedule schedule)
    {
        SuspendLayout();
        this.schedule = schedule;
        Text = "Scheduler";
        Icon = appIcon;
        Font = new Font("Segoe UI", 9);
        BackColor = Palette.Background;
        ForeColor = Palette.Ink;
        DoubleBuffered = true;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = true;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(552, 510);
        Padding = new Padding(22, 18, 22, 16);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

        var header = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Size = new Size(508, 68) };
        header.Controls.Add(new Label { Text = "DAILY PLANNER", ForeColor = Palette.Accent, Font = new Font(Font.FontFamily, 8, FontStyle.Bold), AutoSize = true, Location = new Point(0, 0) });
        header.Controls.Add(new Label { Text = "My schedule", Font = new Font(Font.FontFamily, 22, FontStyle.Bold), AutoSize = true, Location = new Point(-3, 18) });
        layout.Controls.Add(header, 0, 0);

        var columns = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        columns.Controls.Add(new Label { Text = "TIME", Font = new Font(Font.FontFamily, 7.5f, FontStyle.Bold), ForeColor = Palette.Muted, AutoSize = true, Location = new Point(0, 5) });
        columns.Controls.Add(new Label { Text = "YOUR TASKS", Font = new Font(Font.FontFamily, 7.5f, FontStyle.Bold), ForeColor = Palette.Muted, AutoSize = true, Location = new Point(84, 5) });
        layout.Controls.Add(columns, 0, 1);
        layout.Controls.Add(timeline, 0, 2);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = new Padding(0, 12, 0, 0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var add = new PillButton { Name = "AddEntry", Text = "Add Entry", Symbol = ButtonSymbol.Plus, Size = new Size(130, 36), Margin = Padding.Empty, AccessibleName = "Add entry" };
        add.Click += (_, _) => OpenEntry();
        footer.Controls.Add(add, 0, 0);
        footer.Controls.Add(count, 1, 0);
        layout.Controls.Add(footer, 0, 3);
        Controls.Add(layout);

        Shown += (_, _) =>
        {
            PositionAtTopRight();
            RenderTasks();
        };

        ResumeLayout(true);
    }

    private void PositionAtTopRight()
    {
        var work = (Screen.PrimaryScreen ?? Screen.FromControl(this)).WorkingArea;
        var inset = (int)Math.Round(18 * DeviceDpi / 96f);
        Size = new Size(Math.Min(Width, work.Width - inset * 2), Math.Min(Height, work.Height - inset * 2));
        Location = new Point(work.Right - Width - inset, work.Top + inset);
    }

    private void OpenEntry()
    {
        if (entryForm is { IsDisposed: false })
        {
            entryForm.Activate();
            return;
        }

        entryForm = new EntryForm(task =>
        {
            schedule.Add(task);
            RenderTasks();
        });

        entryForm.Show(this);
        entryForm.Activate();
    }

    private void RenderTasks()
    {
        timeline.SetTasks(schedule.Tasks,
            id => ChangeTasks(() => schedule.Toggle(id)), id => ChangeTasks(() => schedule.Delete(id)));
        var done = schedule.Tasks.Count(task => task.IsDone);
        count.Text = schedule.Tasks.Count == 0 ? "Your day, at your pace." : $"{done} of {schedule.Tasks.Count} complete";
    }

    private void ChangeTasks(Action change)
    {
        try
        {
            change();
            RenderTasks();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show("Couldn't save the change. Check the task file is writable and try again.",
                "Task unchanged", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            entryForm?.Dispose();
        }
        
        base.Dispose(disposing);
        if (disposing) appIcon.Dispose();
    }
}
