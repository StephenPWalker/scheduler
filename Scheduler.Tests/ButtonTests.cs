using System.Reflection;
using Scheduler;

namespace Scheduler.Tests;

internal static partial class Program
{
    private static void CheckButtonStates()
    {
        using var host = new Form { ShowInTaskbar = false, Text = "Button tests" };
        var button = new PillButton { Size = new Size(140, 40), Location = new Point(20, 20), Text = "", Fill = Color.FromArgb(80, 130, 100), BackColor = Palette.Background };
        host.Controls.Add(button);
        host.Show();
        Application.DoEvents();
        Raise(button, "OnMouseLeave", EventArgs.Empty);
        var normal = CentreColour(button);
        Raise(button, "OnMouseEnter", EventArgs.Empty);
        var hover = CentreColour(button);
        Check(hover.ToArgb() != normal.ToArgb(), "Hover state visibly differs from normal");
        Raise(button, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 20, 20, 0));
        var mousePressed = CentreColour(button);
        Check(mousePressed.ToArgb() != hover.ToArgb(), "Mouse press visibly differs from hover");
        Raise(button, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 20, 20, 0));
        Check(CentreColour(button).ToArgb() == hover.ToArgb(), "Mouse release restores the hover state");
        Raise(button, "OnMouseLeave", EventArgs.Empty);
        Check(CentreColour(button).ToArgb() == normal.ToArgb(), "Mouse leave restores the normal state");
        Raise(button, "OnKeyDown", new KeyEventArgs(Keys.Space));
        Check(CentreColour(button).ToArgb() != normal.ToArgb(), "Space shows a pressed state without requiring a mouse hover");
        Raise(button, "OnKeyUp", new KeyEventArgs(Keys.Space));
        Check(CentreColour(button).ToArgb() == normal.ToArgb(), "Releasing Space clears the pressed state");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        button.Enabled = false;
        button.PerformClick();
        Check(clicks == 0 && CentreColour(button).ToArgb() != normal.ToArgb(), "Disabled buttons are visibly disabled and cannot be clicked");
        button.Enabled = true;
        button.PerformClick();
        Check(clicks == 1, "Re-enabling a button restores its click action");
        foreach (var symbol in Enum.GetValues<ButtonSymbol>())
        {
            button.Symbol = symbol;
            button.Text = symbol is ButtonSymbol.Plus or ButtonSymbol.Check ? "Action" : "";
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Palette.Line;
            using var bitmap = Snapshot(button);
            Check(bitmap.GetPixel(0, 0).ToArgb() == button.BackColor.ToArgb(), $"{symbol} button paints every corner with its opaque surface colour");
        }
        Check(button.AccessibilityObject.Role == AccessibleRole.PushButton, "Custom painting retains the standard accessible push-button role");
        host.Close();
    }

    private static Color CentreColour(PillButton button)
    {
        using var bitmap = Snapshot(button);
        return bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
    }

    private static void Raise(PillButton button, string method, EventArgs args) => typeof(PillButton)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, [args]);
}
