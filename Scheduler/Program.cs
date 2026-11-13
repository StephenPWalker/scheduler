namespace Scheduler;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var instance = new Mutex(true, @"Local\StephenPWalker.Scheduler", out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("Scheduler is already running. Switch to it from the taskbar.", "Scheduler");
            return;
        }

        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopScheduler", "tasks.json");
            
        try
        {
            var schedule = new TaskSchedule(new TaskStore(path));
            Application.Run(new SchedulerForm(schedule));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
        {
            MessageBox.Show($"Scheduler could not open its task file. Your existing file has been left untouched.\n\n{path}\n\n{exception.Message}",
                "Unable to open schedule", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
