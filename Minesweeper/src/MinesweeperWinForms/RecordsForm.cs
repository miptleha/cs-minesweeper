using MinesweeperCore.Abstractions;
using MinesweeperCore.Domain;

namespace MinesweeperWinForms;

public class RecordsForm : Form
{
    public RecordsForm(IRecordStore recordStore)
    {
        Text = "Лучшие результаты";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(300, 250);
        ShowInTaskbar = false;

        var tabControl = new TabControl { Dock = DockStyle.Fill };
        tabControl.TabPages.Add(CreateTab("Новичок", DifficultyLevel.Beginner, recordStore));
        tabControl.TabPages.Add(CreateTab("Любитель", DifficultyLevel.Intermediate, recordStore));
        tabControl.TabPages.Add(CreateTab("Профессионал", DifficultyLevel.Expert, recordStore));

        Controls.Add(tabControl);
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

    private TabPage CreateTab(string title, DifficultyLevel level, IRecordStore store)
    {
        var page = new TabPage(title);
        var listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable
        };

        listView.Columns.Add("Место", 50);
        listView.Columns.Add("Имя", 130);
        listView.Columns.Add("Время", 80);

        var records = store.LoadRecords(level).ToList();
        for (int i = 0; i < records.Count; i++)
        {
            var row = new ListViewItem((i + 1).ToString());
            row.SubItems.Add(string.IsNullOrWhiteSpace(records[i].PlayerName) ? "Аноним" : records[i].PlayerName);
            row.SubItems.Add($"{records[i].TimeSeconds:F1} сек");
            listView.Items.Add(row);
        }

        page.Controls.Add(listView);
        return page;
    }
}
