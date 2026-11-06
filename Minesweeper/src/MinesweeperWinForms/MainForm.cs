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

    private readonly TableLayoutPanel _topPanel;
    private readonly Panel _boardPanel;
    private readonly PictureBox _mineCounter;
    private readonly PictureBox _timerDisplay;
    private readonly Button _smileyButton;
    private readonly System.Windows.Forms.Timer _timer;

    private CellPoint? _triggerCell;
    private static readonly Dictionary<string, Image> _imageCache = new();
    private static readonly Dictionary<string, Image> _smileyCache = new();

    // Поля для обработки мыши и аккордов
    private MouseButtons _downButtons = MouseButtons.None;
    private CellPoint? _chordCandidate = null;
    private bool _suppressNextMouseUp = false;

    // Поля для ручного определения двойного клика
    private CellPoint? _lastClickPoint = null;
    private DateTime _lastClickTime = DateTime.MinValue;

    private float ScaleFactor => DeviceDpi / 96f;
    private int GetCellSize() => (int)(24 * ScaleFactor);

    public MainForm()
    {
        _recordStore = new JsonRecordStore();
        _engine = new GameEngine(new SystemRandomProvider(), _recordStore);

        Text = "Сапёр";

        // Отключаем авто-масштабирование WinForms, всё делаем вручную
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

        _topPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = SystemColors.Control,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        _topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        _topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));

        _mineCounter = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = Color.Black,
            Anchor = AnchorStyles.Left
        };

        _timerDisplay = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = Color.Black,
            Anchor = AnchorStyles.Right
        };

        _smileyButton = new Button
        {
            FlatStyle = FlatStyle.Standard,
            Cursor = Cursors.Hand,
            TabStop = false,
            ImageAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(0),
            Anchor = AnchorStyles.None
        };
        _smileyButton.Click += (s, e) => StartGame(_engine.Settings);

        _topPanel.Controls.Add(_mineCounter, 0, 0);
        _topPanel.Controls.Add(_smileyButton, 1, 0);
        _topPanel.Controls.Add(_timerDisplay, 2, 0);

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
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ApplyScale(); // Применяем масштаб шапки перед стартом
        StartGame(DifficultyPresets.Beginner);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ApplyScale(); // Если перетащили на другой монитор
        StartGame(_engine.Settings);
    }

    private void ApplyScale()
    {
        float scale = ScaleFactor;

        _topPanel.Height = (int)(48 * scale);

        _mineCounter.Size = new Size((int)(72 * scale), (int)(32 * scale));
        _mineCounter.Margin = new Padding((int)(10 * scale), (int)(8 * scale), 0, 0);

        _timerDisplay.Size = new Size((int)(72 * scale), (int)(32 * scale));
        _timerDisplay.Margin = new Padding(0, (int)(8 * scale), (int)(10 * scale), 0);

        _smileyButton.Size = new Size((int)(32 * scale), (int)(32 * scale));
        _smileyButton.Margin = new Padding(0, (int)(8 * scale), 0, 0);
    }

    private void StartGame(GameSettings settings)
    {
        _triggerCell = null;
        _downButtons = MouseButtons.None;
        _chordCandidate = null;
        _suppressNextMouseUp = false;
        _lastClickPoint = null;

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
                        jsonStore.RenameRecord(_engine.Difficulty, _engine.Elapsed.TotalSeconds, prompt.PlayerName);
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
        // Средняя кнопка — аккорд сразу (3-finger tap на тачпаде).
        if (e.Button == MouseButtons.Middle)
        {
            var midPoint = ToCellPoint(e.X, e.Y);
            if (midPoint.HasValue)
            {
                _engine.Chord(midPoint.Value);
                UpdateMineCounter();
            }
            return;
        }

        if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
            _downButtons |= e.Button;

        // Обе кнопки зажаты — потенциальный аккорд ЛКМ+ПКМ. 
        // Запоминаем клетку, где произошло второе нажатие.
        if ((_downButtons & MouseButtons.Left) != 0 && (_downButtons & MouseButtons.Right) != 0)
        {
            _chordCandidate = ToCellPoint(e.X, e.Y);
        }

        // Смайлик «о» — только для ЛКМ и только если это не начало аккорда.
        if (e.Button == MouseButtons.Left
            && _chordCandidate is null
            && (_engine.State == GameState.InProgress || _engine.State == GameState.NotStarted))
        {
            SetSmiley("smiley_oh.png");
        }
    }

    private void BoardPanel_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
            _downButtons &= ~e.Button;

        // Второй MouseUp после сработавшего аккорда — гасим.
        // НО только если это не начало нового аккорда: если пользователь снова
        // нажал ЛКМ при зажатой ПКМ (_chordCandidate задан), это новый аккорд,
        // и его нужно обработать, а не подавлять.
        if (_suppressNextMouseUp && _chordCandidate is null)
        {
            _suppressNextMouseUp = false;
            ResetSmileyForCurrentState();
            return;
        }

        // Аккорд по ЛКМ+ПКМ: срабатывает при отпускании первой из двух зажатых.
        if (_chordCandidate is CellPoint chordCell)
        {
            _chordCandidate = null;
            _suppressNextMouseUp = true;
            _engine.Chord(chordCell);
            UpdateMineCounter();
            ResetSmileyForCurrentState();
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            ResetSmileyForCurrentState();

            var point = ToCellPoint(e.X, e.Y);
            if (!point.HasValue)
                return;

            if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
            {
                // Ctrl+ЛКМ = аналог ПКМ (CycleMark). Флаги/вопросы.
                _engine.CycleMark(point.Value);
            }
            else
            {
                // КАСТОМНОЕ ОПРЕДЕЛЕНИЕ ДВОЙНОГО КЛИКА (Устойчиво к сдвигу тачпада)
                DateTime now = DateTime.Now;
                bool isDoubleClick = _lastClickPoint.HasValue
                                     && _lastClickPoint.Value == point.Value
                                     && (now - _lastClickTime).TotalMilliseconds <= SystemInformation.DoubleClickTime;

                if (isDoubleClick)
                {
                    // Это двойной клик по той же клетке! Делаем аккорд.
                    _engine.Chord(point.Value);
                    _lastClickPoint = null; // Сбрасываем, чтобы тройной клик не сработал как еще один аккорд
                }
                else
                {
                    // Обычный одиночный клик — открываем клетку.
                    _engine.OpenCell(point.Value);
                    _lastClickPoint = point.Value;
                    _lastClickTime = now;
                }
            }

            UpdateMineCounter();
        }
        else if (e.Button == MouseButtons.Right)
        {
            // ПКМ: метка (как было, на MouseUp — нужно для детекции аккорда).
            var point = ToCellPoint(e.X, e.Y);
            if (!point.HasValue)
                return;

            _engine.CycleMark(point.Value);
            UpdateMineCounter();
        }
    }

    private void BoardPanel_MouseEnter(object? sender, EventArgs e)
    {
        // Защита от «застрявших» кнопок (если отпустили за пределами панели).
        if (Control.MouseButtons == MouseButtons.None)
            _downButtons = MouseButtons.None;

        if (Control.MouseButtons == MouseButtons.Left
            && (_engine.State == GameState.InProgress || _engine.State == GameState.NotStarted))
            SetSmiley("smiley_oh.png");
    }

    private void BoardPanel_MouseLeave(object? sender, EventArgs e)
    {
        if (_engine.State == GameState.InProgress || _engine.State == GameState.NotStarted)
            SetSmiley("smiley.png");
    }

    // --- Вспомогательные методы для мыши ---

    private CellPoint? ToCellPoint(int x, int y)
    {
        int cellSize = GetCellSize();
        int cx = x / cellSize;
        int cy = y / cellSize;
        if (cx < 0 || cx >= _engine.Width || cy < 0 || cy >= _engine.Height)
            return null;
        return new CellPoint(cx, cy);
    }

    private void ResetSmileyForCurrentState()
    {
        SetSmiley(_engine.State switch
        {
            GameState.Won => "smiley_win.png",
            GameState.Lost => "smiley_lose.png",
            _ => "smiley.png"
        });
    }

    // --------------------------------------

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

        int w = _mineCounter.Width / 3;
        int h = _mineCounter.Height;

        if (remaining >= 0)
        {
            _mineCounter.Image = RenderLedCounter(remaining, 3, w, h);
        }
        else
        {
            int abs = Math.Abs(remaining);
            var bmp = new Bitmap(_mineCounter.Width, _mineCounter.Height);
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

        int w = _timerDisplay.Width / 3;
        int h = _timerDisplay.Height;

        _timerDisplay.Image = RenderLedCounter(seconds, 3, w, h);
    }

    private Image RenderLedCounter(int value, int digits, int digitW, int digitH)
    {
        var bmp = new Bitmap(digits * digitW, digitH);
        using var g = Graphics.FromImage(bmp);

        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        var s = value.ToString().PadLeft(digits, '0');
        for (int i = 0; i < digits; i++)
            g.DrawImage(LoadImage($"timer_{s[i]}.png"), new Rectangle(i * digitW, 0, digitW, digitH));

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

    // Панель с двойной буферизацией и отключенным фокусом
    // (отключенный фокус убирает красную рамку)
    private sealed class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.Selectable, false);
        }
    }
}
