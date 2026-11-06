using System.Diagnostics;

namespace MinesweeperWinForms;

public class AboutForm : Form
{
    public AboutForm()
    {
        Text = "О программе";

        AutoScaleMode = AutoScaleMode.None;
        float scale = DeviceDpi / 96f;

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size((int)(350 * scale), (int)(200 * scale));
        ShowInTaskbar = false;

        var iconBox = new PictureBox
        {
            Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath)?.ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size((int)(64 * scale), (int)(64 * scale)),
            Location = new Point((int)(20 * scale), (int)(20 * scale))
        };

        // Убрали умножение шрифта на scale
        var titleLabel = new Label
        {
            Text = "Сапёр",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            Location = new Point((int)(100 * scale), (int)(20 * scale)),
            AutoSize = true
        };
        var descLabel = new Label { Text = "Классическая игра «Сапёр».", Location = new Point((int)(100 * scale), (int)(55 * scale)), AutoSize = true };
        var authorLabel = new Label { Text = "Автор: miptleha.", Location = new Point((int)(100 * scale), (int)(75 * scale)), AutoSize = true };

        var linkLabel = new LinkLabel { Text = "Скачать последнюю версию с GitHub", Location = new Point((int)(20 * scale), (int)(120 * scale)), AutoSize = true };
        linkLabel.LinkClicked += (s, e) =>
        {
            Process.Start(new ProcessStartInfo { FileName = "https://github.com/miptleha/cs-minesweeper", UseShellExecute = true });
        };

        var okButton = new Button { Text = "OK", Location = new Point((int)(250 * scale), (int)(150 * scale)), Size = new Size((int)(80 * scale), (int)(30 * scale)), DialogResult = DialogResult.OK };

        Controls.AddRange(new Control[] { iconBox, titleLabel, descLabel, authorLabel, linkLabel, okButton });
        AcceptButton = okButton;

        // Убрали умножение базового шрифта на scale
        Font = new Font("Segoe UI", 9F);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
