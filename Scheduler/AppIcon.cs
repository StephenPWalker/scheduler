namespace Scheduler;

internal static class AppIcon
{
    public static Icon Create()
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("Scheduler.AppIcon.ico")
            ?? throw new InvalidOperationException("The embedded Scheduler icon is missing.");
            
        using var source = new Icon(stream);
        return (Icon)source.Clone();
    }
}
