using System.Globalization;
using System.Text.Json;
using Scheduler;

namespace Scheduler.Tests;

internal static partial class Program
{
    private static void CheckValidationBoundaries()
    {
        var valid = Task();
        foreach (var title in new string?[] { null, "", " ", "\t\r\n", "\u2003" })
            Check((valid with { Title = title! }).ValidationError() is not null, "Null, empty and Unicode-whitespace titles are rejected");
        Check((valid with { Id = Guid.Empty }).ValidationError() is not null, "An empty ID is rejected");
        Check(Task().Id != Task().Id, "New tasks receive independent IDs");
        Check((valid with { Title = new string('t', 80), Description = new string('d', 500) }).ValidationError() is null, "Exact title and description limits are accepted");
        Check((valid with { Description = null! }).ValidationError() is not null, "Null description is rejected");
        Check((valid with { Description = "" }).ValidationError() is null, "Description is optional");
        Check((valid with { Description = "Line one\r\n日本語 café ✓" }).ValidationError() is null, "Multiline Unicode descriptions are accepted");

        foreach (var colour in new string?[] { null, "", "#", "#12345", "#1234567", "#12345G", " 123456", "#12345\n", "#１２３４５６" })
            Check((valid with { Colour = colour! }).ValidationError() is not null, "Malformed or null colour is rejected");
        foreach (var colour in Palette.Colours)
            Check((valid with { Colour = colour.Hex }).ValidationError() is null, $"Palette colour {colour.Name} is valid");

        var mismatches = new List<string>();
        foreach (var start in Enumerable.Range(0, 1440))
        {
            foreach (var duration in new[] { -1, 0, 1, 14, 15, 30, 60, 180, 1440 })
            {
                var end = start + duration;
                var accepted = (valid with { StartMinute = start, EndMinute = end }).ValidationError() is null;
                var expected = duration >= 15 && end <= 1439;
                if (accepted != expected) mismatches.Add($"{start}+{duration}");
            }
        }
        Check(mismatches.Count == 0, "All 1,440 start minutes match minimum-duration and day-boundary rules across nine durations");
        foreach (var (start, end) in new[] { (-1, 15), (1440, 1455), (0, -1), (1439, 1439), (1425, 1439) })
            Check((valid with { StartMinute = start, EndMinute = end }).ValidationError() is not null, $"Invalid boundary {start}–{end} is rejected");
        Check((valid with { StartMinute = 1424, EndMinute = 1439 }).ValidationError() is null, "The final possible 15-minute task is accepted");
        Check((valid with { StartMinute = 0, EndMinute = 1 }).ValidationError(minimumDuration: 1) is null, "Legacy one-minute entries can still be read");
        Check(Enumerable.Range(0, 1440).All(minute => ScheduleTask.FormatTime(minute) == new TimeOnly(minute / 60, minute % 60).ToString("HH:mm", CultureInfo.InvariantCulture)),
            "All 1,440 clock labels have correct 24-hour formatting");
    }

    private static void CheckScheduleBoundaries(string directory)
    {
        var path = Path.Combine(directory, "tasks.json");
        var schedule = new TaskSchedule(new TaskStore(path));
        var occupied = Task("Occupied", 600, 660);
        schedule.Add(occupied);
        var original = File.ReadAllBytes(path);
        foreach (var (start, end) in new[] { (600, 660), (615, 645), (585, 675), (585, 615), (645, 675), (600, 615), (645, 660) })
        {
            Expect<ArgumentException>(() => schedule.Add(Task("Conflict", start, end)), $"Overlap pattern {start}–{end} is rejected");
            Check(schedule.Tasks.SequenceEqual([occupied]) && File.ReadAllBytes(path).SequenceEqual(original), "Rejected overlap changes neither memory nor disk");
        }
        Expect<ArgumentException>(() => schedule.Add(occupied with { StartMinute = 800, EndMinute = 830 }), "Duplicate IDs are rejected even at a free time");
        Expect<ArgumentException>(() => schedule.Add(Task("Too short", 800, 814)), "Service rejects a short task before saving");
        Check(File.ReadAllBytes(path).SequenceEqual(original), "Rejected duplicate and invalid task do not rewrite the file");
        schedule.Toggle(occupied.Id);
        Expect<ArgumentException>(() => schedule.Add(Task("Done still occupies time", 620, 640)), "Completed tasks still reserve their period");
        schedule.Add(Task("Before", 570, 600));
        schedule.Add(Task("After", 660, 690));
        Check(schedule.Tasks.Select(task => task.StartMinute).SequenceEqual([570, 600, 660]), "Adjacent tasks on either side are accepted and sorted");
        var snapshot = schedule.Tasks.ToArray();
        schedule.Toggle(Guid.NewGuid());
        schedule.Delete(Guid.NewGuid());
        Check(schedule.Tasks.SequenceEqual(snapshot) && new TaskStore(path).Load().SequenceEqual(snapshot), "Unknown IDs leave the schedule unchanged");
        Expect<NotSupportedException>(() => ((IList<ScheduleTask>)schedule.Tasks).Clear(), "Callers cannot mutate the task collection without saving");
        schedule.Delete(occupied.Id);
        schedule.Add(Task("Reused gap", 600, 660));
        Check(schedule.Tasks.Count == 3, "Deleting a task frees its period for another task");

        var quarters = new TaskSchedule(new TaskStore(Path.Combine(directory, "quarters.json")));
        for (var index = 0; index < 4; index++) quarters.Add(Task($"Quarter {index}", 600 + index * 15, 615 + index * 15));
        Check(quarters.Tasks.Count == 4, "Four adjacent quarter-hour tasks occupy one hour");
        quarters.Add(Task("Across hour", 705, 735));
        quarters.Add(Task("After crossing", 735, 750));
        Check(quarters.Tasks.Count == 6, "An hour-crossing task can be followed immediately by another task");

        // Older files may contain short or overlapping entries; opening the file
        // must preserve user data even though new entries obey the current rules.
        var legacyPath = Path.Combine(directory, "legacy.json");
        var legacy = new[] { Task("z", 60, 90), Task("Beta", 60, 75), Task("alpha", 60, 75), Task("Short", 0, 1) };
        File.WriteAllText(legacyPath, JsonSerializer.Serialize(legacy));
        var loaded = new TaskSchedule(new TaskStore(legacyPath));
        Check(loaded.Tasks.Select(task => task.Title).SequenceEqual(["Short", "alpha", "Beta", "z"]), "Legacy entries load without loss and sort by start, end and title");
        loaded.Toggle(legacy[^1].Id);
        Check(new TaskStore(legacyPath).Load().Count == 4, "Editing completion preserves legacy entries");
    }
}
