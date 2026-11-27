using System.Windows.Forms;
using MinesweeperCore.Abstractions;
using MinesweeperBlazorHybrid.Windows;

namespace MinesweeperBlazorHybrid.Services;

/// <summary>
/// Сервис для показа нативных WinForms-диалогов из Blazor-компонентов.
/// Переиспользует формы из MinesweeperWinForms.
/// </summary>
public class DialogService
{
    private readonly IRecordStore _recordStore;

    public DialogService(IRecordStore recordStore)
    {
        _recordStore = recordStore;
    }

    /// <summary>
    /// Показывает окно «Лучшие результаты».
    /// </summary>
    public void ShowRecords()
    {
        var owner = Form.ActiveForm;
        using var form = new RecordsForm(_recordStore);
        if (owner != null) form.ShowDialog(owner);
        else form.ShowDialog();
    }

    /// <summary>
    /// Показывает окно «О программе».
    /// </summary>
    public void ShowAbout()
    {
        var owner = Form.ActiveForm;
        using var form = new AboutForm();
        if (owner != null) form.ShowDialog(owner);
        else form.ShowDialog();
    }

    /// <summary>
    /// Показывает окно «Управление».
    /// </summary>
    public void ShowControls()
    {
        const string text =
            "ЛКМ — открыть клетку; повторный ЛКМ по открытой цифре — аккорд (открыть соседей)." + "\n" +
            "ПКМ — метка: флаг → вопрос → пусто." + "\n" +
            "Ctrl+ЛКМ или долгое нажатие (~0,5 с) — то же, что ПКМ.";
        MessageBox.Show(Form.ActiveForm, text, "Управление", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>
    /// Показывает окно ввода имени. Возвращает имя или null при отмене.
    /// </summary>
    public string? ShowNamePrompt()
    {
        var owner = Form.ActiveForm;
        using var form = new NamePromptForm();

        var result = owner != null ? form.ShowDialog(owner) : form.ShowDialog();

        return result == DialogResult.OK ? form.PlayerName : null;
    }
}
