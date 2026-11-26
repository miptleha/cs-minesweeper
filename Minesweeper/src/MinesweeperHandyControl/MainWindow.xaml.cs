using System;
using System.Windows;

namespace MinesweeperHandyControl
{
    public partial class MainWindow : Window
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
