namespace MinesweeperWinForms;

public class NamePromptForm : Form
{
    public string PlayerName { get; private set; } = "Игрок";

    public NamePromptForm()
    {
        Text = "Новый рекорд!";

        AutoScaleMode = AutoScaleMode.None;
        float scale = DeviceDpi / 96f;

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size((int)(280 * scale), (int)(140 * scale));
        ShowInTaskbar = false;

        var label = new Label { Text = "Введите ваше имя:", Location = new Point((int)(20 * scale), (int)(20 * scale)), AutoSize = true };
        var textBox = new TextBox { Location = new Point((int)(20 * scale), (int)(45 * scale)), Width = (int)(240 * scale), Text = "Игрок" };
        var btnOk = new Button { Text = "OK", Location = new Point((int)(180 * scale), (int)(90 * scale)), Size = new Size((int)(80 * scale), (int)(30 * scale)), DialogResult = DialogResult.OK };

        btnOk.Click += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
                PlayerName = textBox.Text.Trim();
        };

        Controls.AddRange(new Control[] { label, textBox, btnOk });
        AcceptButton = btnOk;

        // Убрали умножение базового шрифта на scale
        Font = new Font("Segoe UI", 9F);
    }
}
