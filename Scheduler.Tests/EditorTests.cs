using System.Reflection;
using Scheduler;

namespace Scheduler.Tests;

internal static partial class Program
{
    private static EntryForm ShowEntry(Action<ScheduleTask> save)
    {
        var entry = new EntryForm(save);
        entry.Show();
        Application.DoEvents();
        return entry;
    }

    private static void FillEntry(EntryForm entry, string title = "A task", int start = 540, int end = 600)
    {
        entry.TitleInput.Text = title;
        entry.StartInput.Value = DateTime.Today.AddMinutes(start);
        entry.EndInput.Value = DateTime.Today.AddMinutes(end);
    }

    private static void CheckEditorBehaviour()
    {
        var saved = new List<ScheduleTask>();
        using (var editor = ShowEntry(saved.Add))
        {
            Check(editor.TitleInput.MaxLength == 80 && editor.DescriptionInput.MaxLength == 500, "Text fields expose the title and description limits");
            Check(editor.StartInput.CustomFormat == "HH:mm" && editor.EndInput.CustomFormat == "HH:mm" && editor.StartInput.ShowUpDown && editor.EndInput.ShowUpDown,
                "Time inputs use a 24-hour clock without a date calendar");
            Check(editor.StartInput.Value.Hour == 9 && editor.EndInput.Value.Hour == 10, "Default editor period is 09:00–10:00");
            Check(ReferenceEquals(editor.AcceptButton, editor.SaveButton) && editor.CancelButton is PillButton, "Save and cancel are wired as dialog actions");
            FillEntry(editor, "  Trim me  ");
            editor.DescriptionInput.Text = " \r\n  Notes ✓ 日本語  \r\n ";
            editor.SaveButton.PerformClick();
            Check(editor.IsDisposed && saved.Single().Title == "Trim me" && saved.Single().Description == "Notes ✓ 日本語", "Saving trims outer whitespace and preserves Unicode text");
            Check(!saved.Single().IsDone && saved.Single().Colour == Palette.Colours[0].Hex, "New entries start as Todo in the default colour");
        }

        foreach (var (name, hex) in Palette.Colours)
        {
            using var editor = ShowEntry(saved.Add);
            FillEntry(editor, name);
            var swatches = AllControls(editor).OfType<PillButton>().Where(button => Palette.Colours.Any(colour => colour.Name == button.Name)).ToArray();
            Check(swatches.Length == 6 && swatches.Count(button => button.Text == "✓") == 1, "Editor has six swatches and one initial selection");
            swatches.Single(button => button.Name == name).PerformClick();
            Check(swatches.Count(button => button.Text == "✓") == 1 && swatches.Single(button => button.Name == name).Text == "✓", $"Selecting {name} deselects every other swatch");
            editor.SaveButton.PerformClick();
            Check(editor.IsDisposed && saved[^1].Colour == hex, $"Selected {name} colour is saved");
        }
        Check(saved.Select(task => task.Id).Distinct().Count() == saved.Count, "Separate editor windows create distinct task IDs");

        using (var editor = ShowEntry(saved.Add))
        {
            FillEntry(editor, new string('T', 80), 1424, 1439);
            editor.DescriptionInput.Text = new string('D', 500);
            editor.SaveButton.PerformClick();
            Check(editor.IsDisposed && saved[^1].Title.Length == 80 && saved[^1].Description.Length == 500, "Exact text limits and the final possible quarter-hour save through the UI");
        }
        using (var editor = ShowEntry(saved.Add))
        {
            FillEntry(editor, "No description");
            editor.SaveButton.PerformClick();
            Check(saved[^1].Description.Length == 0, "Description can be left empty in the editor");
        }

        var count = saved.Count;
        using (var editor = ShowEntry(saved.Add))
        {
            FillEntry(editor, "Discard this");
            ((Button)editor.CancelButton!).PerformClick();
            Check(editor.IsDisposed && saved.Count == count, "Cancel closes the modeless editor without saving");
        }
        using (var editor = ShowEntry(saved.Add))
        {
            FillEntry(editor, "Also discard this");
            editor.Close();
            Check(editor.IsDisposed && saved.Count == count, "Title-bar close discards an unsaved entry");
        }
    }

    private static void CheckEditorFailures()
    {
        var saves = 0;
        using (var editor = ShowEntry(_ => saves++))
        {
            foreach (var title in new[] { "", "  ", "\t\r\n" })
            {
                FillEntry(editor, title);
                editor.SaveButton.PerformClick();
                Check(editor.ErrorLabel.Text.Contains("title") && saves == 0 && !editor.IsDisposed, "Invalid title stays in the editor without calling save");
            }
            foreach (var (start, end) in new[] { (540, 540), (600, 540), (540, 554), (1380, 0) })
            {
                FillEntry(editor, "Invalid period", start, end);
                editor.SaveButton.PerformClick();
                Check(editor.ErrorLabel.Text.Length > 0 && saves == 0 && !editor.IsDisposed, "Zero, reverse, short and overnight periods do not reach the save callback");
            }
            editor.TitleInput.Text = "Changed title";
            Check(editor.ErrorLabel.Text.Length == 0, "Changing the title clears the validation error");
            editor.SaveButton.PerformClick();
            editor.DescriptionInput.Text = "Changed description";
            Check(editor.ErrorLabel.Text.Length == 0, "Changing the description clears the validation error");
            editor.SaveButton.PerformClick();
            editor.StartInput.Value = DateTime.Today.AddHours(22);
            Check(editor.ErrorLabel.Text.Length == 0, "Changing the start time clears the validation error");
            editor.SaveButton.PerformClick();
            editor.EndInput.Value = DateTime.Today.AddHours(23);
            Check(editor.ErrorLabel.Text.Length == 0, "Changing the end time clears the validation error");
        }

        foreach (var failure in new Exception[] { new IOException("disk unavailable"), new UnauthorizedAccessException("file is locked"), new ArgumentException("This period overlaps another task.") })
        {
            var fail = true;
            var captured = new List<ScheduleTask>();
            using var editor = ShowEntry(task =>
            {
                if (fail) throw failure;
                captured.Add(task);
            });
            FillEntry(editor, "Keep my entry", 705, 735);
            editor.DescriptionInput.Text = "Keep these notes too";
            editor.SaveButton.PerformClick();
            Check(!editor.IsDisposed && captured.Count == 0 && editor.ErrorLabel.Text.Length > 0, $"Editor displays {failure.GetType().Name} instead of closing");
            Check(editor.TitleInput.Text == "Keep my entry" && editor.DescriptionInput.Text == "Keep these notes too" && editor.StartInput.Value.Minute == 45,
                "A failed save keeps all entered fields for retry");
            fail = false;
            editor.SaveButton.PerformClick();
            Check(editor.IsDisposed && captured.Single().StartMinute == 705 && captured[0].EndMinute == 735, "Retry succeeds with the original entered period");
        }
    }

    private static void CheckEditorKeyboard()
    {
        var saved = new List<ScheduleTask>();
        using (var editor = ShowEntry(saved.Add))
        {
            FillEntry(editor);
            Check(DialogKey(editor, Keys.Enter), "Enter is handled by the editor");
            Check(editor.IsDisposed && saved.Count == 1, "Enter saves a valid task");
        }
        using (var editor = ShowEntry(saved.Add))
        {
            Check(DialogKey(editor, Keys.Enter) && !editor.IsDisposed && saved.Count == 1, "Enter on an invalid entry displays validation without saving");
            Check(DialogKey(editor, Keys.Escape), "Escape is handled by the editor");
            Check(editor.IsDisposed && saved.Count == 1, "Escape closes the editor without saving");
        }
    }

    private static bool DialogKey(Form form, Keys key) => (bool)typeof(Form)
        .GetMethod("ProcessDialogKey", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [key])!;
}
