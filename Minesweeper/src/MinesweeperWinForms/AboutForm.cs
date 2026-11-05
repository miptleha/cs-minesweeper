using System.Diagnostics;

namespace MinesweeperWinForms;

public class AboutForm : Form
{
    public AboutForm()
    {
        Text = "О программе";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(350, 200);
        ShowInTaskbar = false;

        var iconBox = new PictureBox
        {
            Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath)?.ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(64, 64),
            Location = new Point(20, 20)
        };

        var titleLabel = new Label { Text = "Сапёр", Font = new Font("Segoe UI", 16, FontStyle.Bold), Location = new Point(100, 20), AutoSize = true };
        var descLabel = new Label { Text = "Классическая игра «Сапёр».\nАвтор: miptleha.", Location = new Point(100, 55), AutoSize = true };

        var linkLabel = new LinkLabel { Text = "Скачать последнюю версию с GitHub", Location = new Point(20, 110), AutoSize = true };
        linkLabel.LinkClicked += (s, e) =>
        {
            Process.Start(new ProcessStartInfo { FileName = "https://github.com/miptleha/cs-minesweeper", UseShellExecute = true });
        };

        var okButton = new Button { Text = "OK", Location = new Point(250, 150), Size = new Size(80, 30), DialogResult = DialogResult.OK };

        Controls.AddRange(new Control[] { iconBox, titleLabel, descLabel, linkLabel, okButton });
        AcceptButton = okButton;
    }

    // Закрытие по Esc
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
