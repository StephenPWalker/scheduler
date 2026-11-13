using System.Xml.Linq;
using Scheduler;

namespace Scheduler.Tests;

internal static partial class Program
{
    private static void CheckIconAssets()
    {
        using var stream = typeof(SchedulerForm).Assembly.GetManifestResourceStream("Scheduler.AppIcon.ico");
        Check(stream is not null, "Application icon is embedded in the compiled assembly");
        using var data = new MemoryStream();
        stream!.CopyTo(data);
        var bytes = data.ToArray();
        using var reader = new BinaryReader(new MemoryStream(bytes));
        Check(reader.ReadUInt16() == 0 && reader.ReadUInt16() == 1, "Embedded resource is a Windows ICO");
        var count = reader.ReadUInt16();
        Check(count == 10, "ICO contains ten display resolutions");
        var frames = new List<(int Width, int Height, int Length, int Offset)>();
        for (var index = 0; index < count; index++)
        {
            var widthByte = reader.ReadByte();
            var heightByte = reader.ReadByte();
            reader.ReadUInt16(); // Palette size and reserved byte.
            var planes = reader.ReadUInt16();
            var bits = reader.ReadUInt16();
            var length = reader.ReadInt32();
            var offset = reader.ReadInt32();
            Check(planes == 1 && bits == 32 && length > 0 && offset >= 6 + count * 16 && offset + length <= bytes.Length, "Icon directory entry has valid colour depth and data bounds");
            frames.Add((widthByte == 0 ? 256 : widthByte, heightByte == 0 ? 256 : heightByte, length, offset));
        }
        Check(frames.Select(frame => frame.Width).SequenceEqual([16, 20, 24, 32, 40, 48, 64, 96, 128, 256]), "Icon includes small, high-DPI and Explorer preview sizes");
        foreach (var frame in frames)
        {
            using var frameStream = new MemoryStream(bytes, frame.Offset, frame.Length);
            using var image = new Bitmap(frameStream);
            Check(image.Width == frame.Width && image.Height == frame.Height && image.GetPixel(0, 0).A == 0, "PNG icon frame decodes at its declared size with transparent corners");
        }
        using var first = AppIcon.Create();
        using var second = AppIcon.Create();
        first.Dispose();
        using var visible = second.ToBitmap();
        Check(visible.Width > 0 && second.Handle != 0, "Window icons remain valid after the resource stream and another icon are disposed");

        var svgPath = Path.Combine(AppContext.BaseDirectory, "Assets", "scheduler.svg");
        var icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "scheduler.ico");
        Check(File.Exists(svgPath) && File.Exists(icoPath), "Reusable SVG and ICO assets are copied with the application");
        var svg = XDocument.Load(svgPath).Root!;
        Check(svg.Name.LocalName == "svg" && (string?)svg.Attribute("viewBox") == "0 0 64 64" && svg.Elements().Any(element => element.Name.LocalName == "title"),
            "Reusable SVG is scalable and has an accessible title");
        Check(File.ReadAllBytes(icoPath).SequenceEqual(bytes), "Packaged ICO matches the embedded application icon");
    }
}
