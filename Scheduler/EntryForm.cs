namespace Scheduler;

internal sealed class EntryForm : Form
{
    private readonly Icon appIcon = AppIcon.Create();
    internal TextBox TitleInput { get; } = new() { Name = "TaskTitle", MaxLength = 80, Dock = DockStyle.Fill };
    internal TextBox DescriptionInput { get; } = new() { Name = "TaskDescription", MaxLength = 500, Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    internal DateTimePicker StartInput { get; } = TimeInput("StartTime");
    internal DateTimePicker EndInput { get; } = TimeInput("EndTime");
    internal PillButton SaveButton { get; } = new() { Name = "SaveEntry", Text = "Add task", Size = new Size(116, 36) };
    internal Label ErrorLabel { get; } = new() { ForeColor = Color.FromArgb(155, 55, 55), AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Action<ScheduleTask> save;
    private string selectedColour = Palette.Colours[0].Hex;
    private readonly List<PillButton> colourButtons = [];

    public EntryForm(Action<ScheduleTask> save, int startMinute = 9 * 60)
    {
        SuspendLayout();
        this.save = save;
        Text = "Add entry · Scheduler";
        Icon = appIcon;
        Font = new Font("Segoe UI", 9);
        BackColor = Palette.Background;
        ForeColor = Palette.Ink;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(430, 540);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Padding = new Padding(24);
        ShowInTaskbar = true;

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 12, Margin = Padding.Empty };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in new[] { 36, 26, 32, 28, 66, 28, 104, 28, 40, 46, 40, 0 })
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, height));

        content.Controls.Add(new Label { Text = "Make room for a task", Font = new Font(Font.FontFamily, 16, FontStyle.Bold), AutoSize = true }, 0, 0);
        content.Controls.Add(FieldLabel("Title"), 0, 1);
        content.Controls.Add(TitleInput, 0, 2);
        content.Controls.Add(FieldLabel("Time period"), 0, 3);

        var times = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Margin = Padding.Empty };
        times.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        times.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20));
        times.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        times.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        times.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        times.Controls.Add(new Label { Text = "From", ForeColor = Palette.Muted, AutoSize = true }, 0, 0);
        times.Controls.Add(new Label { Text = "Until", ForeColor = Palette.Muted, AutoSize = true }, 2, 0);
        times.Controls.Add(StartInput, 0, 1);
        times.Controls.Add(EndInput, 2, 1);
        StartInput.Value = DateTime.Today.AddMinutes(Math.Clamp(startMinute, 0, 1438));
        EndInput.Value = DateTime.Today.AddMinutes(Math.Min(startMinute + 60, 1439));
        content.Controls.Add(times, 0, 4);
        content.Controls.Add(FieldLabel("Description  ·  optional"), 0, 5);
        content.Controls.Add(DescriptionInput, 0, 6);
        content.Controls.Add(FieldLabel("Colour"), 0, 7);

        var colours = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, WrapContents = false };
        foreach (var (name, hex) in Palette.Colours)
        {
            var swatch = new PillButton
            {
                Text = hex == selectedColour ? "✓" : "", AccessibleName = name, Name = name,
                Fill = ColorTranslator.FromHtml(hex), TextColour = Palette.Accent,
                Size = new Size(44, 32), Margin = new Padding(0, 0, 10, 0)
            };
            swatch.Click += (_, _) =>
            {
                selectedColour = hex;
                foreach (var button in colourButtons) button.Text = ReferenceEquals(button, swatch) ? "✓" : "";
            };
            colourButtons.Add(swatch);
            colours.Controls.Add(swatch);
        }

        content.Controls.Add(colours, 0, 8);
        content.Controls.Add(ErrorLabel, 0, 9);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = Padding.Empty };
        var cancel = new PillButton { Text = "Cancel", Fill = Color.FromArgb(235, 237, 230), TextColour = Palette.Ink, Size = new Size(88, 36), DialogResult = DialogResult.Cancel };
        cancel.Click += (_, _) => Close();
        actions.Controls.Add(SaveButton);
        actions.Controls.Add(cancel);
        content.Controls.Add(actions, 0, 10);
        Controls.Add(content);
        AcceptButton = SaveButton;
        CancelButton = cancel;
        SaveButton.Click += (_, _) => SaveEntry();
        TitleInput.TextChanged += (_, _) => ErrorLabel.Text = "";
        DescriptionInput.TextChanged += (_, _) => ErrorLabel.Text = "";
        StartInput.ValueChanged += (_, _) => ErrorLabel.Text = "";
        EndInput.ValueChanged += (_, _) => ErrorLabel.Text = "";
        Shown += (_, _) => TitleInput.Focus();
        ResumeLayout(true);
    }

    private void SaveEntry()
    {
        var task = new ScheduleTask
        {
            Title = TitleInput.Text.Trim(), Description = DescriptionInput.Text.Trim(),
            StartMinute = StartInput.Value.Hour * 60 + StartInput.Value.Minute,
            EndMinute = EndInput.Value.Hour * 60 + EndInput.Value.Minute,
            Colour = selectedColour
        };

        if (task.ValidationError() is { } error)
        {
            ErrorLabel.Text = error;
            return;
        }
        
        try
        {
            save(task);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ErrorLabel.Text = "Couldn't save. Check the task file is writable and try again.";
        }
        catch (ArgumentException exception)
        {
            ErrorLabel.Text = exception.Message;
        }
    }

    private static DateTimePicker TimeInput(string name) => new()
    {
        Name = name, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true,
        Dock = DockStyle.Top, AccessibleName = name == "StartTime" ? "Start time" : "End time"
    };

    private static Label FieldLabel(string text) => new() { Text = text, AutoSize = true, Padding = new Padding(0, 5, 0, 0) };

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) appIcon.Dispose();
    }
}
