using System.Reflection;

namespace PZ_Mapper_Studio;

internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About PZ Reverse Mapper";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(680, 520);
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.FromArgb(244, 246, 249);

        var extractedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (extractedIcon is not null)
        {
            Icon = (Icon)extractedIcon.Clone();
        }

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(24)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        var iconBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0, 0, 16, 8),
            Image = LoadLogo() ?? extractedIcon?.ToBitmap()
        };
        header.SetRowSpan(iconBox, 2);

        var title = new Label
        {
            Dock = DockStyle.Fill,
            Text = "PZ Reverse Mapper",
            Font = new Font("Segoe UI", 20f, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 38, 52),
            TextAlign = ContentAlignment.BottomLeft
        };

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = $"Version {version?.ToString(3) ?? "1.0.0"}",
            ForeColor = Color.FromArgb(91, 101, 118),
            TextAlign = ContentAlignment.TopLeft
        };

        header.Controls.Add(iconBox, 0, 0);
        header.Controls.Add(title, 1, 0);
        header.Controls.Add(versionLabel, 1, 1);

        var description = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 10f),
            DetectUrls = false,
            Text =
                "PZ Reverse Mapper rebuilds editable mapping assets from compiled Project Zomboid map data.\r\n\r\n" +
                "It converts legacy 300 × 300 and Build 42 256 × 256 compiled cells into TMX/PZW projects, " +
                "restores objects and room/building metadata, creates map previews, and reconstructs tilesets " +
                "directly from .pack atlas metadata.\r\n\r\n" +
                "The tile extractor keeps every physical atlas pack separate, restores each tile to its real " +
                "canvas, merges matching .floor.pack tiles only after cutting, and rebuilds tilesets with the " +
                "standard eight-column layout.\r\n\r\n" +
                "This standalone application is the modern successor to the original PZ_Mapper research project. " +
                "It contains only the new .NET 8 Studio and command-line converter.\r\n\r\n" +
                "PZ Reverse Mapper is source-available software. Official releases are licensed for personal, " +
                "non-commercial use; redistribution and commercial exploitation require written permission.\r\n\r\n" +
                "PZ Reverse Mapper is an unofficial community tool. Project Zomboid, WorldEd and TileZed are " +
                "created by The Indie Stone. No game assets are included or redistributed."
        };

        var closeButton = new Button
        {
            Text = "Close",
            Dock = DockStyle.Right,
            Width = 112,
            DialogResult = DialogResult.OK
        };

        AcceptButton = closeButton;
        CancelButton = closeButton;
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(description, 0, 1);
        layout.Controls.Add(closeButton, 0, 2);
        Controls.Add(layout);

        extractedIcon?.Dispose();
    }

    private static Image? LoadLogo()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("PZReverseMapper.png");
        if (stream is null)
        {
            return null;
        }

        using var image = Image.FromStream(stream);
        return (Image)image.Clone();
    }
}
