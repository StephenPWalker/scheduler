using System.Text;
using System.Text.Json;
using Scheduler;

namespace Scheduler.Tests;

internal static partial class Program
{
    private static void CheckStorageCases(string directory)
    {
        var path = Path.Combine(directory, "nested", "deeper", "tasks.json");
        var store = new TaskStore(path);
        Check(store.Load().Count == 0 && !Directory.Exists(Path.GetDirectoryName(path)), "Reading a missing schedule creates no directories or files");
        var task = Task("Unicode 日本語 ✓", 0, 15) with { Description = "Line 1\nLine 2\r\n\"quoted\" \\ text", Colour = "#aBC123", IsDone = true };
        store.Save([task]);
        Check(store.Load().Single() == task, "Nested directories, Unicode, quotes, slashes and line endings round-trip");
        store.Save([]);
        using (var emptyJson = JsonDocument.Parse(File.ReadAllText(path)))
            Check(store.Load().Count == 0 && emptyJson.RootElement.GetArrayLength() == 0, "Saving no tasks replaces an existing file with an empty JSON array");
        store.Save([task]);
        Check(store.Load().Single() == task, "Saving after an empty schedule restores normal persistence");

        var badPath = Path.Combine(directory, "bad.json");
        foreach (var invalidJson in new[] { "", " ", "{}", "42", "true", "[", "[{},]", "[\"task\"]", "[{\"Id\":\"invalid-guid\"}]", "[{\"StartMinute\":\"09:00\"}]", "[{\"IsDone\":1}]" })
        {
            File.WriteAllText(badPath, invalidJson);
            Expect<JsonException>(() => new TaskStore(badPath).Load(), "Malformed or incorrectly typed JSON is rejected");
            Check(File.ReadAllText(badPath) == invalidJson, "Rejected JSON is not repaired or overwritten");
        }
        foreach (var invalid in new[]
        {
            task with { Id = Guid.Empty }, task with { Title = null! }, task with { Description = null! }, task with { Colour = null! },
            task with { Title = " " }, task with { Title = new string('a', 81) }, task with { Description = new string('d', 501) },
            task with { StartMinute = -1 }, task with { EndMinute = 1440 }, task with { EndMinute = 0 }
        })
        {
            File.WriteAllText(badPath, JsonSerializer.Serialize(new[] { invalid }));
            Expect<InvalidDataException>(() => new TaskStore(badPath).Load(), "Invalid stored field is rejected");
        }
        File.WriteAllText(badPath, " \r\n[ ]\t");
        Check(new TaskStore(badPath).Load().Count == 0, "An empty array with JSON whitespace is accepted");
        var withFutureField = JsonSerializer.Serialize(new[] { task }).Replace("\"IsDone\":true", "\"IsDone\":true,\"FutureProperty\":42");
        File.WriteAllText(badPath, withFutureField, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        Check(new TaskStore(badPath).Load().Single() == task, "UTF-8 BOM and unknown future properties are accepted");
        Check(!Directory.EnumerateFiles(directory, "*.tmp", SearchOption.AllDirectories).Any(), "Successful saves leave no temporary files anywhere in the test tree");
    }

    private static void CheckStorageRecovery(string directory)
    {
        var blockedParent = Path.Combine(directory, "parent-is-file");
        File.WriteAllText(blockedParent, "untouched");
        var blockedSchedule = new TaskSchedule(new TaskStore(Path.Combine(blockedParent, "tasks.json")));
        ExpectStorageFailure(() => blockedSchedule.Add(Task()), "A file in place of the parent directory prevents saving");
        Check(blockedSchedule.Tasks.Count == 0 && File.ReadAllText(blockedParent) == "untouched", "Failed directory creation changes no state or existing data");

        var path = Path.Combine(directory, "recover.json");
        var schedule = new TaskSchedule(new TaskStore(path));
        var original = Task();
        schedule.Add(original);
        var attributes = File.GetAttributes(path);
        try
        {
            File.SetAttributes(path, attributes | FileAttributes.ReadOnly);
            ExpectStorageFailure(() => schedule.Toggle(original.Id), "A read-only destination rejects replacement");
            Check(!schedule.Tasks[0].IsDone && new TaskStore(path).Load()[0] == original, "Read-only save failure leaves memory and file intact");
        }
        finally { File.SetAttributes(path, attributes); }
        schedule.Toggle(original.Id);
        Check(new TaskStore(path).Load()[0].IsDone, "A failed save can be retried after the file becomes writable");
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Expect<IOException>(() => new TaskStore(path).Load(), "A locked task file produces a read error rather than an empty schedule");
        Check(new TaskStore(path).Load().Count == 1, "A failed read leaves the original schedule available for retry");
        Check(!Directory.EnumerateFiles(directory, "*.tmp", SearchOption.AllDirectories).Any(), "All failure paths clean temporary save files");
    }
}
