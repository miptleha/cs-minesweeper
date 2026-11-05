namespace MinesweeperWinForms;

public class NamePromptForm : Form
{
    public string PlayerName { get; private set; } = "Игрок";

    public NamePromptForm()
    {
        Text = "Новый рекорд!";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(250, 120);

        var label = new Label { Text = "Введите ваше имя:", Location = new Point(10, 10), AutoSize = true };
        var textBox = new TextBox { Location = new Point(10, 30), Width = 210, Text = "Игрок" };
        var btnOk = new Button { Text = "OK", Location = new Point(140, 70), Size = new Size(80, 30), DialogResult = DialogResult.OK };

        btnOk.Click += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
                PlayerName = textBox.Text.Trim();
        };

        Controls.AddRange(new Control[] { label, textBox, btnOk });
        AcceptButton = btnOk;
    }
}