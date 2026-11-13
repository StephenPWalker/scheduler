using System.Text.Json;

namespace Scheduler;

internal sealed record ScheduleTask
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int StartMinute { get; init; }
    public int EndMinute { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Colour { get; init; } = Palette.Colours[0].Hex;
    public bool IsDone { get; init; }

    public static string FormatTime(int minute) => $"{minute / 60:00}:{minute % 60:00}";

    public string? ValidationError(int minimumDuration = 15)
    {
        if (Id == Guid.Empty) return "A task must have an ID.";

        if (string.IsNullOrWhiteSpace(Title)) return "Give your task a title.";

        if (Title.Length > 80) return "Keep the title to 80 characters or fewer.";

        if (Description is null || Description.Length > 500) return "Keep the description to 500 characters or fewer.";
        
        if (StartMinute < 0 || StartMinute > 1439 || EndMinute < 0 || EndMinute > 1439 || EndMinute <= StartMinute)
            return "Choose an end time after the start time, within the same day.";
        if (EndMinute - StartMinute < minimumDuration)
            return $"Tasks must last at least {minimumDuration} minutes.";

        if (Colour is null || Colour.Length != 7 || Colour[0] != '#' || Colour.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") >= 0)
            return "Choose a valid task colour.";

        return null;
    }
}

internal sealed class TaskStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public List<ScheduleTask> Load()
    {
        if (!File.Exists(path)) return [];

        var tasks = JsonSerializer.Deserialize<List<ScheduleTask>>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("The task file does not contain a task list.");

        if (tasks.Any(task => task is null || task.ValidationError(minimumDuration: 1) is not null) || tasks.Select(task => task.Id).Distinct().Count() != tasks.Count)
            throw new InvalidDataException("The task file contains invalid or duplicate entries.");

        return tasks;
    }

    public void Save(IReadOnlyList<ScheduleTask> tasks)
    {
        var absolutePath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        var temporaryPath = absolutePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, tasks, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, absolutePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}

internal sealed class TaskSchedule
{
    private readonly TaskStore store;
    private List<ScheduleTask> tasks;
    public IReadOnlyList<ScheduleTask> Tasks => tasks.AsReadOnly();

    public TaskSchedule(TaskStore store)
    {
        this.store = store;
        tasks = Sort(store.Load());
    }

    public void Add(ScheduleTask task)
    {
        if (task.ValidationError() is { } error) throw new ArgumentException(error);

        if (tasks.Any(existing => existing.Id == task.Id)) throw new ArgumentException("This task already exists.");
        if (tasks.Any(existing => task.StartMinute < existing.EndMinute && task.EndMinute > existing.StartMinute))
            throw new ArgumentException("This period overlaps another task. Choose a free time.");
        
        Commit([.. tasks, task]);
    }

    public void Toggle(Guid id) => Commit(tasks.Select(task => task.Id == id ? task with { IsDone = !task.IsDone } : task));
    public void Delete(Guid id) => Commit(tasks.Where(task => task.Id != id));

    private void Commit(IEnumerable<ScheduleTask> updated)
    {
        var next = Sort(updated);
        store.Save(next);
        tasks = next;
    }

    private static List<ScheduleTask> Sort(IEnumerable<ScheduleTask> items) => items
        .OrderBy(task => task.StartMinute).ThenBy(task => task.EndMinute).ThenBy(task => task.Title, StringComparer.OrdinalIgnoreCase).ToList();
}
