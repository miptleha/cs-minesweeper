using MinesweeperCore.Abstractions;
using MinesweeperCore.Domain;
using MinesweeperCore.Engine;
using MinesweeperCore.Events;
using MinesweeperCore.Infrastructure;

namespace MinesweeperWinForms;

public class MainForm : Form
{
    private const int CellSize = 24;
    private const int TopPanelHeight = 48;

    private readonly GameEngine _engine;
    private readonly IRecordStore _recordStore;
    private readonly Panel _topPanel;
    private readonly Panel _boardPanel;
    private readonly PictureBox _mineCounter;
    private readonly PictureBox _timerDisplay;
    private readonly Button _smileyButton;
    private readonly System.Windows.Forms.Timer _timer;

    private CellPoint? _triggerCell;
    private static readonly Dictionary<string, Image> _imageCache = new();
    private static readonly Dictionary<string, Image> _smileyCache = new();

    public MainForm()
    {
        // 1. Создаем движок и хранилище В САМОМ НАЧАЛЕ
        _recordStore = new JsonRecordStore();
        _engine = new GameEngine(new SystemRandomProvider(), _recordStore);

        Text = "Сапёр";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

        _topPanel = new Panel { Dock = DockStyle.Top, Height = TopPanelHeight, BackColor = SystemColors.Control };
        _mineCounter = new PictureBox { Size = new Size(3 * 24, 32), BackColor = Color.Black };
        _timerDisplay = new PictureBox { Size = new Size(3 * 24, 32), BackColor = Color.Black };

        _smileyButton = new Button
        {
            Size = new Size(32, 32), 
            FlatStyle = FlatStyle.Standard,
            Cursor = Cursors.Hand,
            TabStop = false,
            ImageAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(0)
        };
        
        // Теперь здесь _engine гарантированно НЕ null, warning исчезнет
        _smileyButton.Click += (s, e) => StartGame(_engine.Settings);

        _topPanel.Controls.Add(_mineCounter);
        _topPanel.Controls.Add(_smileyButton);
        _topPanel.Controls.Add(_timerDisplay);

        _topPanel.Resize += (s, e) =>
        {
            _smileyButton.Left = (_topPanel.Width - _smileyButton.Width) / 2;
            _smileyButton.Top = (_topPanel.Height - _smileyButton.Height) / 2;
            _timerDisplay.Left = _topPanel.Width - _timerDisplay.Width - 10;
            _timerDisplay.Top = (_topPanel.Height - _timerDisplay.Height) / 2;
            _mineCounter.Left = 10;
            _mineCounter.Top = (_topPanel.Height - _mineCounter.Height) / 2;
        };

        _boardPanel = new BufferedPanel { Dock = DockStyle.Fill };
        _boardPanel.Paint += BoardPanel_Paint;
        _boardPanel.MouseDown += BoardPanel_MouseDown;
        _boardPanel.MouseUp += BoardPanel_MouseUp;
        _boardPanel.MouseEnter += BoardPanel_MouseEnter;
        _boardPanel.MouseLeave += BoardPanel_MouseLeave;

        var menu = new MenuStrip();
        var gameMenu = new ToolStripMenuItem("Игра");
        gameMenu.DropDownItems.Add("Новая игра", null, (s, e) => StartGame(_engine.Settings));
        gameMenu.DropDownItems.Add(new ToolStripSeparator());
        gameMenu.DropDownItems.Add("Новичок", null, (s, e) => StartGame(DifficultyPresets.Beginner));
        gameMenu.DropDownItems.Add("Любитель", null, (s, e) => StartGame(DifficultyPresets.Intermediate));
        gameMenu.DropDownItems.Add("Профессионал", null, (s, e) => StartGame(DifficultyPresets.Expert));
        gameMenu.DropDownItems.Add(new ToolStripSeparator());
        gameMenu.DropDownItems.Add("Выход", null, (s, e) => Close());

        var helpMenu = new ToolStripMenuItem("Справка");
        // И здесь _recordStore тоже уже не null, warning CS8604 исчезнет
        helpMenu.DropDownItems.Add("Лучшие результаты", null, (s, e) => new RecordsForm(_recordStore).ShowDialog(this));
        helpMenu.DropDownItems.Add(new ToolStripSeparator());
        helpMenu.DropDownItems.Add("О программе", null, (s, e) => new AboutForm().ShowDialog(this));

        menu.Items.Add(gameMenu);
        menu.Items.Add(helpMenu);
        MainMenuStrip = menu;

        Controls.Add(_boardPanel);
        Controls.Add(_topPanel);
        Controls.Add(menu);

        _engine.CellChanged += (s, e) => { _boardPanel.Invalidate(); UpdateMineCounter(); };
        _engine.CellsRevealed += (s, e) => _boardPanel.Invalidate();
        _engine.GameStateChanged += OnGameStateChanged;
        _engine.BoardGenerated += (s, e) => UpdateMineCounter();

        _timer = new System.Windows.Forms.Timer { Interval = 100 };
        _timer.Tick += (s, e) => UpdateTimer();
        _timer.Start();

        StartGame(DifficultyPresets.Beginner);
    }

    private void StartGame(GameSettings settings)
    {
        _triggerCell = null;
        _engine.StartNewGame(settings);

        int boardW = _engine.Width * CellSize;
        int boardH = _engine.Height * CellSize;
        int menuH = MainMenuStrip?.Height ?? 24;

        ClientSize = new Size(boardW, boardH + TopPanelHeight + menuH);

        SetSmiley("smiley.png");
        UpdateMineCounter();
        UpdateTimer();
        _boardPanel.Invalidate(true);
    }

    private void OnGameStateChanged(object? sender, GameStateChangedEventArgs e)
    {
        if (e.NewState == GameState.Won)
        {
            SetSmiley("smiley_win.png");

            if (_engine.Difficulty != DifficultyLevel.Custom)
            {
                // 1. Спрашиваем имя (ядро уже сохранило запись, но с пустым/старым именем)
                using var prompt = new NamePromptForm();
                if (prompt.ShowDialog(this) == DialogResult.OK)
                {
                    _engine.PlayerName = prompt.PlayerName;

                    // 2. Перезаписываем имя в файле
                    if (_recordStore is JsonRecordStore jsonStore)
                    {
                        jsonStore.RenameRecord(_engine.Difficulty, _engine.Elapsed.TotalSeconds, prompt.PlayerName);
                    }
                }

                // 3. Открываем таблицу рекордов, чтобы игрок увидел свой результат
                new RecordsForm(_recordStore).ShowDialog(this);
            }
        }
        else if (e.NewState == GameState.Lost)
        {
            SetSmiley("smiley_lose.png");
            _triggerCell = e.TriggerCell?.Position;
        }

        UpdateMineCounter();
        UpdateTimer();
        _boardPanel.Invalidate();
    }

    private void SetSmiley(string name) => _smileyButton.Image = GetSmiley(name);

    private static Image GetSmiley(string name)
    {
        if (_smileyCache.TryGetValue(name, out var cached)) return cached;
        var original = LoadImage(name);
        var bmp = new Bitmap(28, 28);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(original, 0, 0, 28, 28);
        _smileyCache[name] = bmp;
        return bmp;
    }

    private void BoardPanel_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) SetSmiley("smiley_oh.png");
        else if (e.Button == MouseButtons.Right)
        {
            int x = e.X / CellSize, y = e.Y / CellSize;
            if (x >= 0 && x < _engine.Width && y >= 0 && y < _engine.Height)
            {
                _engine.CycleMark(new CellPoint(x, y));
                UpdateMineCounter();
            }
        }
    }

    private void BoardPanel_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (_engine.State == GameState.InProgress || _engine.State == GameState.NotStarted)
                SetSmiley("smiley.png");

            int x = e.X / CellSize, y = e.Y / CellSize;
            if (x >= 0 && x < _engine.Width && y >= 0 && y < _engine.Height)
                _engine.OpenCell(new CellPoint(x, y));
        }
    }

    private void BoardPanel_MouseEnter(object? sender, EventArgs e)
    {
        if (Control.MouseButtons == MouseButtons.Left && (_engine.State == GameState.InProgress || _engine.State == GameState.NotStarted))
            SetSmiley("smiley_oh.png");
    }

    private void BoardPanel_MouseLeave(object? sender, EventArgs e)
    {
        if (_engine.State == GameState.InProgress || _engine.State == GameState.NotStarted)
            SetSmiley("smiley.png");
    }

    private void BoardPanel_Paint(object? sender, PaintEventArgs e)
    {
        for (int x = 0; x < _engine.Width; x++)
            for (int y = 0; y < _engine.Height; y++)
            {
                var info = _engine.GetCell(new CellPoint(x, y));
                DrawCell(e.Graphics, new Rectangle(x * CellSize, y * CellSize, CellSize, CellSize), info);
            }
    }

    private void DrawCell(Graphics g, Rectangle rect, CellInfo info)
    {
        switch (info.State)
        {
            case CellState.Closed: g.DrawImage(LoadImage("cell_closed.png"), rect); break;
            case CellState.Flagged:
                g.DrawImage(LoadImage("cell_closed.png"), rect);
                g.DrawImage(LoadImage("cell_flag.png"), rect); break;
            case CellState.Question:
                g.DrawImage(LoadImage("cell_closed.png"), rect);
                g.DrawImage(LoadImage("cell_question.png"), rect); break;
            case CellState.WrongFlag: g.DrawImage(LoadImage("cell_wrong_flag.png"), rect); break;
            case CellState.Opened:
                if (info.Position == _triggerCell) g.DrawImage(LoadImage("cell_exploded.png"), rect);
                else
                {
                    g.DrawImage(LoadImage("cell_empty.png"), rect);
                    if (info.Content == CellContent.Number && info.AdjacentMines >= 1 && info.AdjacentMines <= 8)
                        g.DrawImage(LoadImage($"digit_{info.AdjacentMines}.png"), rect);
                    else if (info.Content == CellContent.Mine) g.DrawImage(LoadImage("bomb.png"), rect);
                }
                break;
        }
    }

    private void UpdateMineCounter()
    {
        int remaining = _engine.RemainingMines;
        if (_mineCounter.Image is Bitmap oldBmp) oldBmp.Dispose();

        if (remaining >= 0) _mineCounter.Image = RenderLedCounter(remaining, 3);
        else
        {
            int abs = Math.Abs(remaining);
            var bmp = new Bitmap(3 * 24, 32);
            using var g = Graphics.FromImage(bmp);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.DrawImage(LoadImage("minus.png"), new Rectangle(0, 0, 24, 32));
            var s = abs.ToString().PadLeft(2, '0');
            g.DrawImage(LoadImage($"timer_{s[0]}.png"), new Rectangle(24, 0, 24, 32));
            g.DrawImage(LoadImage($"timer_{s[1]}.png"), new Rectangle(48, 0, 24, 32));
            _mineCounter.Image = bmp;
        }
    }

    private void UpdateTimer()
    {
        var seconds = Math.Min((int)_engine.Elapsed.TotalSeconds, 999);
        if (_timerDisplay.Image is Bitmap oldBmp) oldBmp.Dispose();
        _timerDisplay.Image = RenderLedCounter(seconds, 3);
    }

    private Image RenderLedCounter(int value, int digits)
    {
        var bmp = new Bitmap(digits * 24, 32);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        var s = value.ToString().PadLeft(digits, '0');
        for (int i = 0; i < digits; i++)
            g.DrawImage(LoadImage($"timer_{s[i]}.png"), new Rectangle(i * 24, 0, 24, 32));
        return bmp;
    }

    private static Image LoadImage(string name)
    {
        if (_imageCache.TryGetValue(name, out var cached)) return cached;
        var asm = typeof(MainForm).Assembly;
        using var stream = asm.GetManifestResourceStream($"Resources.{name}");
        if (stream == null) return new Bitmap(24, 24);
        using var temp = new Bitmap(stream);
        var img = new Bitmap(temp);
        _imageCache[name] = img;
        return img;
    }

    private sealed class BufferedPanel : Panel
    {
        public BufferedPanel() => DoubleBuffered = true;
    }
}
