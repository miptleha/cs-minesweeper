using System.Drawing.Drawing2D;
using MinesweeperCore.Abstractions;
using MinesweeperCore.Domain;
using MinesweeperCore.Engine;
using MinesweeperCore.Events;
using MinesweeperCore.Infrastructure;

namespace MinesweeperWinForms;

public class MainForm : Form
{
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

    // Коэффициент масштабирования для High-DPI (1.0 = 100%, 1.5 = 150%)
    private float ScaleFactor => DeviceDpi / 96f;

    // Динамический размер клетки (на 100% DPI будет 24)
    private int GetCellSize() => (int)(24 * ScaleFactor);

    public MainForm()
    {
        // 1. Создаем движок и хранилище ДО инициализации UI (чтобы не было warning-ов)
        _recordStore = new JsonRecordStore();
        _engine = new GameEngine(new SystemRandomProvider(), _recordStore);

        Text = "Сапёр";

        // 2. Включаем DPI-масштабирование
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);

        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

        // 3. Создаем панели
        _topPanel = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = SystemColors.Control };

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
        _smileyButton.Click += (s, e) => StartGame(_engine.Settings);

        _topPanel.Controls.Add(_mineCounter);
        _topPanel.Controls.Add(_smileyButton);
        _topPanel.Controls.Add(_timerDisplay);

        // Центрирование элементов на верхней панели
        _topPanel.Resize += (s, e) =>
        {
            _smileyButton.Left = (_topPanel.Width - _smileyButton.Width) / 2;
            _smileyButton.Top = (_topPanel.Height - _smileyButton.Height) / 2;

            _timerDisplay.Left = _topPanel.Width - _timerDisplay.Width - (int)(10 * ScaleFactor);
            _timerDisplay.Top = (_topPanel.Height - _timerDisplay.Height) / 2;

            _mineCounter.Left = (int)(10 * ScaleFactor);
            _mineCounter.Top = (_topPanel.Height - _mineCounter.Height) / 2;
        };

        _boardPanel = new BufferedPanel { Dock = DockStyle.Fill };
        _boardPanel.Paint += BoardPanel_Paint;
        _boardPanel.MouseDown += BoardPanel_MouseDown;
        _boardPanel.MouseUp += BoardPanel_MouseUp;
        _boardPanel.MouseEnter += BoardPanel_MouseEnter;
        _boardPanel.MouseLeave += BoardPanel_MouseLeave;

        // 4. Меню
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
        helpMenu.DropDownItems.Add("Лучшие результаты", null, (s, e) => new RecordsForm(_recordStore).ShowDialog(this));
        helpMenu.DropDownItems.Add(new ToolStripSeparator());
        helpMenu.DropDownItems.Add("О программе", null, (s, e) => new AboutForm().ShowDialog(this));

        menu.Items.Add(gameMenu);
        menu.Items.Add(helpMenu);
        MainMenuStrip = menu;

        Controls.Add(_boardPanel);
        Controls.Add(_topPanel);
        Controls.Add(menu);

        // 5. Подписка на события движка
        _engine.CellChanged += (s, e) => { _boardPanel.Invalidate(); UpdateMineCounter(); };
        _engine.CellsRevealed += (s, e) => _boardPanel.Invalidate();
        _engine.GameStateChanged += OnGameStateChanged;
        _engine.BoardGenerated += (s, e) => UpdateMineCounter();

        _timer = new System.Windows.Forms.Timer { Interval = 100 };
        _timer.Tick += (s, e) => UpdateTimer();
        _timer.Start();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // Запускаем игру после загрузки формы, когда DeviceDpi точно известен
        StartGame(DifficultyPresets.Beginner);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        // При переносе окна на монитор с другим DPI пересчитываем размеры
        StartGame(_engine.Settings);
    }

    private void StartGame(GameSettings settings)
    {
        _triggerCell = null;
        _engine.StartNewGame(settings);

        int cellSize = GetCellSize();
        int boardW = _engine.Width * cellSize;
        int boardH = _engine.Height * cellSize;
        int menuH = MainMenuStrip?.Height ?? 24;

        ClientSize = new Size(boardW, boardH + _topPanel.Height + menuH);

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
                using var prompt = new NamePromptForm();
                if (prompt.ShowDialog(this) == DialogResult.OK)
                {
                    _engine.PlayerName = prompt.PlayerName;
                    if (_recordStore is JsonRecordStore jsonStore)
                    {
                        jsonStore.RenameRecord(_engine.Difficulty, _engine.Elapsed.TotalSeconds, prompt.PlayerName);
                    }
                }
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

    private Image GetSmiley(string name)
    {
        if (_smileyCache.TryGetValue(name, out var cached)) return cached;
        var original = LoadImage(name);

        int size = (int)(28 * ScaleFactor);
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(original, 0, 0, size, size);

        _smileyCache[name] = bmp;
        return bmp;
    }

    private void BoardPanel_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            SetSmiley("smiley_oh.png");
        }
        else if (e.Button == MouseButtons.Right)
        {
            int cellSize = GetCellSize();
            int x = e.X / cellSize, y = e.Y / cellSize;
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

            int cellSize = GetCellSize();
            int x = e.X / cellSize, y = e.Y / cellSize;
            if (x >= 0 && x < _engine.Width && y >= 0 && y < _engine.Height)
            {
                _engine.OpenCell(new CellPoint(x, y));
            }
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
        int cellSize = GetCellSize();
        for (int x = 0; x < _engine.Width; x++)
            for (int y = 0; y < _engine.Height; y++)
            {
                var info = _engine.GetCell(new CellPoint(x, y));
                DrawCell(e.Graphics, new Rectangle(x * cellSize, y * cellSize, cellSize, cellSize), info);
            }
    }

    private void DrawCell(Graphics g, Rectangle rect, CellInfo info)
    {
        switch (info.State)
        {
            case CellState.Closed:
                g.DrawImage(LoadImage("cell_closed.png"), rect);
                break;
            case CellState.Flagged:
                g.DrawImage(LoadImage("cell_closed.png"), rect);
                g.DrawImage(LoadImage("cell_flag.png"), rect);
                break;
            case CellState.Question:
                g.DrawImage(LoadImage("cell_closed.png"), rect);
                g.DrawImage(LoadImage("cell_question.png"), rect);
                break;
            case CellState.WrongFlag:
                g.DrawImage(LoadImage("cell_wrong_flag.png"), rect);
                break;
            case CellState.Opened:
                if (info.Position == _triggerCell)
                {
                    g.DrawImage(LoadImage("cell_exploded.png"), rect);
                }
                else
                {
                    g.DrawImage(LoadImage("cell_empty.png"), rect);
                    if (info.Content == CellContent.Number && info.AdjacentMines >= 1 && info.AdjacentMines <= 8)
                        g.DrawImage(LoadImage($"digit_{info.AdjacentMines}.png"), rect);
                    else if (info.Content == CellContent.Mine)
                        g.DrawImage(LoadImage("bomb.png"), rect);
                }
                break;
        }
    }

    private void UpdateMineCounter()
    {
        int remaining = _engine.RemainingMines;
        if (_mineCounter.Image is Bitmap oldBmp) oldBmp.Dispose();

        if (remaining >= 0)
        {
            _mineCounter.Image = RenderLedCounter(remaining, 3);
        }
        else
        {
            int abs = Math.Abs(remaining);
            int w = (int)(24 * ScaleFactor);
            int h = (int)(32 * ScaleFactor);
            var bmp = new Bitmap(3 * w, h);
            using var g = Graphics.FromImage(bmp);

            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            g.DrawImage(LoadImage("minus.png"), new Rectangle(0, 0, w, h));
            var s = abs.ToString().PadLeft(2, '0');
            g.DrawImage(LoadImage($"timer_{s[0]}.png"), new Rectangle(w, 0, w, h));
            g.DrawImage(LoadImage($"timer_{s[1]}.png"), new Rectangle(2 * w, 0, w, h));

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
        int w = (int)(24 * ScaleFactor);
        int h = (int)(32 * ScaleFactor);
        var bmp = new Bitmap(digits * w, h);
        using var g = Graphics.FromImage(bmp);

        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        var s = value.ToString().PadLeft(digits, '0');
        for (int i = 0; i < digits; i++)
            g.DrawImage(LoadImage($"timer_{s[i]}.png"), new Rectangle(i * w, 0, w, h));

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
