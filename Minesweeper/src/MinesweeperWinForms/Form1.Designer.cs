namespace MinesweeperWinForms;

partial class Form1
{
    /// <summary>
    ///  Переменная, необходимая дизайнеру.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Освобождение используемых ресурсов.
    /// </summary>
    /// <param name="disposing">true — освобождать управляемые ресурсы; иначе false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Метод, необходимый дизайнеру. Не изменять вручную.
    ///  Содержимое метода редактируется только в дизайнере.
    /// </summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(800, 450);
        Text = "Form1";
    }

    #endregion
}
