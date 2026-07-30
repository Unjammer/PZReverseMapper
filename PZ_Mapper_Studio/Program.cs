namespace PZ_Mapper_Studio;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length == 2 && string.Equals(args[0], "--screenshot", StringComparison.OrdinalIgnoreCase))
        {
            SaveScreenshot(args[1]);
            return;
        }

        if (args.Length == 2 && string.Equals(args[0], "--screenshot-about", StringComparison.OrdinalIgnoreCase))
        {
            SaveAboutScreenshot(args[1]);
            return;
        }

        Application.Run(new MainForm());
    }

    private static void SaveScreenshot(string outputFile)
    {
        using var form = new MainForm
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            Size = new Size(1280, 940),
            ShowInTaskbar = false
        };
        SaveFormScreenshot(outputFile, form);
    }

    private static void SaveAboutScreenshot(string outputFile)
    {
        using var form = new AboutForm
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            ShowInTaskbar = false
        };
        SaveFormScreenshot(outputFile, form);
    }

    private static void SaveFormScreenshot(string outputFile, Form form)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputFile));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        form.Show();
        Application.DoEvents();

        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        bitmap.Save(outputFile, System.Drawing.Imaging.ImageFormat.Png);
    }
}
