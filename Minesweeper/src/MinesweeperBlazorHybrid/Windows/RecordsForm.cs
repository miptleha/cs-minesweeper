using MinesweeperCore.Abstractions;
using MinesweeperCore.Domain;

namespace MinesweeperBlazorHybrid.Windows;

public class RecordsForm : Form
{
    public RecordsForm(IRecordStore recordStore)
    {
        Text = "Лучшие результаты";

        AutoScaleMode = AutoScaleMode.None;
        float scale = DeviceDpi / 96f;

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size((int)(360 * scale), (int)(260 * scale));
        ShowInTaskbar = false;

        var tabControl = new TabControl { Dock = DockStyle.Fill };
        tabControl.TabPages.Add(CreateTab("Новичок", DifficultyLevel.Beginner, recordStore, scale));
        tabControl.TabPages.Add(CreateTab("Любитель", DifficultyLevel.Intermediate, recordStore, scale));
        tabControl.TabPages.Add(CreateTab("Профессионал", DifficultyLevel.Expert, recordStore, scale));

        Controls.Add(tabControl);

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

    private TabPage CreateTab(string title, DifficultyLevel level, IRecordStore store, float scale)
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

        listView.Columns.Add("Место", (int)(60 * scale));
        listView.Columns.Add("Имя", (int)(160 * scale));
        listView.Columns.Add("Время", (int)(90 * scale));

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
