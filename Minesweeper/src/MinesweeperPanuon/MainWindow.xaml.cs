using System;
using System.Windows;
using Panuon.WPF.UI;

namespace MinesweeperPanuon
{
    public partial class MainWindow : WindowX
    {
        public MainWindow()
        {
            InitializeComponent();
            try
            {
                // Персональная иконка окна набора: имя PNG-ассета из Resources.
                // TODO: заменить "smiley" на уникальный ассет этого набора контролов.
                Icon = WpfAssets.GetPng("bomb");
            }
            catch (Exception)
            {
                // PNG недоступен — окно остаётся с иконкой exe-файла (ApplicationIcon).
            }
        }
    }
}
