using Hawkynt.NativeForms;
using Hawkynt.NativeForms.Drawing;

namespace MinesweeperNativeForms
{
    /// <summary>Окно болванки на Hawkynt.NativeForms.</summary>
    internal class MainForm : Form
    {
        public MainForm()
        {
            Text = "Сапёр";
            ClientSize = new System.Drawing.Size(800, 600);

            var (width, height, argb) = ImageDecoder.DecodePng(Assets.GetPng("bomb"));
            SetIcon(width, height, argb);

            // TODO: игровое поле — следующая фаза
        }
    }
}
