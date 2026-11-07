using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MinesweeperCore.Abstractions;
using MinesweeperCore.Domain;
using MinesweeperCore.Engine;
using MinesweeperCore.Events;
using MinesweeperCore.Infrastructure;

namespace MinesweeperMonoGame;

public enum Screen
{
    Main,
    Records,
    About,
    NamePrompt
}

public enum MenuKind
{
    None,
    Game,
    Help
}

public class MinesweeperGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private GameEngine _engine = null!;
    private IRecordStore _recordStore = null!;
    private readonly Dictionary<string, Texture2D> _tex = new();
    private SpriteFont _font = null!;

    private Screen _screen = Screen.Main;
    private MenuKind _openMenu = MenuKind.None;
    private int _menuHover = -1;
    private int _recordsTab = 0;

    // Ввод
    private MouseState _prevMouse;
    private KeyboardState _prevKeyboard;
    private bool _suppressNextMouseUp;
    private CellPoint? _chordCell;
    private CellPoint? _lastClickPoint;
    private TimeSpan _lastClickTime;
    private bool _wasInsideBoard;
    private string _smiley = "smiley";
    private CellPoint? _triggerCell;

    // NamePrompt
    private string _nameBuffer = "Игрок";

    // Для отладки счётчиков
    private int _lastMines;
    private int _lastSeconds;

    // --- Данные меню (T5) ---
    private readonly string[] _gameMenuItems = {
        "Новая игра",
        "-",
        "Новичок",
        "Любитель",
        "Профессионал",
        "-",
        "Выход"
    };
    private readonly string[] _helpMenuItems = {
        "Лучшие результаты",
        "-",
        "О программе"
    };

    // Прямоугольники для кликов
    private Rectangle _okButtonRect = Rectangle.Empty;
    private Rectangle _githubLinkRect = Rectangle.Empty;
    private Rectangle _prevLevelRect = Rectangle.Empty;
    private Rectangle _nextLevelRect = Rectangle.Empty;

    private SpriteBatch _spriteBatch = null!;
    private Texture2D _solidColor = null!;

    private const int CellSize = 24;
    private const int TopPanelHeight = 48;
    private const int MenuHeight = 24;
    private const int SmileySize = 32;
    private const int LedW = 24;
    private const int LedH = 32;
    private const int PanelTopMargin = 8;
    private const int DoubleClickMs = 500;

    private int _menuGameX = 8;
    private int _menuGameW;
    private int _menuHelpX;
    private int _menuHelpW;

    public MinesweeperGame()
    {
        _recordStore = new JsonRecordStore();
        _engine = new GameEngine(new SystemRandomProvider(), _recordStore);

        _engine.CellChanged += (s, e) => UpdateMineCounter();
        _engine.CellsRevealed += (s, e) => { };
        _engine.BoardGenerated += (s, e) => UpdateMineCounter();
        _engine.GameStateChanged += OnGameStateChanged;

        _graphics = new GraphicsDeviceManager(this);
        Window.AllowUserResizing = false;
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        Window.TextInput += OnTextInput;
        Window.Title = "Сапёр";
        ApplyWindowSize();
        StartGame(DifficultyPresets.Beginner);
        base.Initialize();
    }

    private void ApplyWindowSize()
    {
        _graphics.PreferredBackBufferWidth = _engine.Width * CellSize;
        _graphics.PreferredBackBufferHeight = _engine.Height * CellSize + TopPanelHeight + MenuHeight;
        _graphics.ApplyChanges();
    }

    private void StartGame(GameSettings settings)
    {
        _triggerCell = null;
        _chordCell = null;
        _suppressNextMouseUp = false;
        _lastClickPoint = null;
        _wasInsideBoard = false;
        _screen = Screen.Main;
        _openMenu = MenuKind.None;
        _menuHover = -1;

        _engine.StartNewGame(settings);
        ApplyWindowSize();
        SetSmiley("smiley");
        UpdateMineCounter();
        UpdateTimer();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _tex["bomb"] = Content.Load<Texture2D>("bomb");
        _tex["cell_closed"] = Content.Load<Texture2D>("cell_closed");
        _tex["cell_empty"] = Content.Load<Texture2D>("cell_empty");
        _tex["cell_flag"] = Content.Load<Texture2D>("cell_flag");
        _tex["cell_question"] = Content.Load<Texture2D>("cell_question");
        _tex["cell_wrong_flag"] = Content.Load<Texture2D>("cell_wrong_flag");
        _tex["cell_exploded"] = Content.Load<Texture2D>("cell_exploded");
        _tex["minus"] = Content.Load<Texture2D>("minus");
        for (int i = 1; i <= 8; i++) _tex[$"digit_{i}"] = Content.Load<Texture2D>($"digit_{i}");
        for (int i = 0; i <= 9; i++) _tex[$"timer_{i}"] = Content.Load<Texture2D>($"timer_{i}");
        _tex["smiley"] = Content.Load<Texture2D>("smiley");
        _tex["smiley_oh"] = Content.Load<Texture2D>("smiley_oh");
        _tex["smiley_win"] = Content.Load<Texture2D>("smiley_win");
        _tex["smiley_lose"] = Content.Load<Texture2D>("smiley_lose");

        _font = Content.Load<SpriteFont>("DefaultFont");

        _menuGameX = 8;
        _menuGameW = (int)(_font.MeasureString("Игра").X * 0.75f) + 16;
        _menuHelpX = _menuGameX + _menuGameW + 4;
        _menuHelpW = (int)(_font.MeasureString("Справка").X * 0.75f) + 16;

        _solidColor = new Texture2D(GraphicsDevice, 1, 1);
        _solidColor.SetData(new[] { Color.White });
    }

    private void OnGameStateChanged(object sender, GameStateChangedEventArgs e)
    {
        switch (e.NewState)
        {
            case GameState.Won:
                SetSmiley("smiley_win");
                if (_engine.Difficulty != DifficultyLevel.Custom)
                {
                    _nameBuffer = "Игрок";
                    _screen = Screen.NamePrompt;
                }
                break;
            case GameState.Lost:
                SetSmiley("smiley_lose");
                _triggerCell = e.TriggerCell?.Position;
                break;
        }
        UpdateMineCounter();
        UpdateTimer();
    }

    private void UpdateMineCounter() => _lastMines = _engine.RemainingMines;
    private void UpdateTimer() => _lastSeconds = Math.Min((int)_engine.Elapsed.TotalSeconds, 999);
    private void SetSmiley(string name) => _smiley = name;

    protected override void Update(GameTime gameTime)
    {
        try
        {
            UpdateInput(gameTime);
            UpdateTimer();

            var mouseState = Mouse.GetState();
            if (SmileyRect.Contains(mouseState.Position) ||
               (_screen != Screen.Main && (_okButtonRect.Contains(mouseState.Position) || _githubLinkRect.Contains(mouseState.Position) || _prevLevelRect.Contains(mouseState.Position) || _nextLevelRect.Contains(mouseState.Position))))
                Mouse.SetCursor(MouseCursor.Hand);
            else
                Mouse.SetCursor(MouseCursor.Arrow);
        }
        catch (Exception ex)
        {
            LogException("Update", ex);
        }
        finally
        {
            base.Update(gameTime);
        }
    }

    private void UpdateInput(GameTime gameTime)
    {
        var m = Mouse.GetState();
        var pos = m.Position;

        if (m.MiddleButton == ButtonState.Pressed && _prevMouse.MiddleButton == ButtonState.Released)
        {
            if (_screen == Screen.Main && _openMenu == MenuKind.None)
            {
                var p = ToCellPoint(pos);
                if (p.HasValue)
                {
                    _engine.Chord(p.Value);
                    UpdateMineCounter();
                }
            }
        }

        bool bothPressed = m.LeftButton == ButtonState.Pressed && m.RightButton == ButtonState.Pressed;
        bool wasBoth = _prevMouse.LeftButton == ButtonState.Pressed && _prevMouse.RightButton == ButtonState.Pressed;
        if (bothPressed && !wasBoth && _screen == Screen.Main && _openMenu == MenuKind.None)
            _chordCell = ToCellPoint(pos);

        bool leftJustDown = m.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
        if (leftJustDown && _chordCell is null && !SmileyRect.Contains(pos)
            && (_engine.State == GameState.InProgress || _engine.State == GameState.NotStarted)
            && _screen == Screen.Main && _openMenu == MenuKind.None)
        {
            SetSmiley("smiley_oh");
        }

        bool insideBoard = BoardRect.Contains(pos);
        if (insideBoard && !_wasInsideBoard)
        {
            if (m.LeftButton == ButtonState.Released && m.RightButton == ButtonState.Released)
                _chordCell = null;
            if (m.LeftButton == ButtonState.Pressed
                && (_engine.State == GameState.InProgress || _engine.State == GameState.NotStarted)
                && _screen == Screen.Main && _openMenu == MenuKind.None)
                SetSmiley("smiley_oh");
        }
        if (!insideBoard && _wasInsideBoard)
        {
            if ((_engine.State == GameState.InProgress || _engine.State == GameState.NotStarted)
                && _screen == Screen.Main && _openMenu == MenuKind.None)
                SetSmiley("smiley");
        }
        _wasInsideBoard = insideBoard;

        if (m.LeftButton == ButtonState.Released && _prevMouse.LeftButton == ButtonState.Pressed)
            OnLeftUp(pos, gameTime);

        if (m.RightButton == ButtonState.Released && _prevMouse.RightButton == ButtonState.Pressed)
            OnRightUp(pos);

        if (EscJustPressed())
        {
            // Esc: сначала закрывает выпадающее меню (если открыто), затем возвращает
            // с экрана. Исключение — NamePrompt: Esc = отмена (без сохранения имени),
            // после чего открывается Records — как в WinForms-версии (§9.4).
            // На главном экране без меню Esc ничего не делает (игру НЕ закрывает).
            if (_openMenu != MenuKind.None)
                _openMenu = MenuKind.None;
            else if (_screen == Screen.NamePrompt)
                _screen = Screen.Records;
            else if (_screen != Screen.Main)
                _screen = Screen.Main;
        }

        UpdateMenuHover(pos);
        UpdateNamePromptKeyboard();

        _prevMouse = m;
        _prevKeyboard = Keyboard.GetState();
    }

    private void UpdateNamePromptKeyboard()
    {
        if (_screen != Screen.NamePrompt) return;

        var kb = Keyboard.GetState();

        if (kb.IsKeyDown(Keys.Enter) && _prevKeyboard.IsKeyDown(Keys.Enter) == false)
            SubmitName();

        if (kb.IsKeyDown(Keys.Back) && _prevKeyboard.IsKeyDown(Keys.Back) == false)
        {
            if (_nameBuffer.Length > 0)
                _nameBuffer = _nameBuffer.Substring(0, _nameBuffer.Length - 1);
        }
    }

    private void UpdateMenuHover(Point pos)
    {
        if (_screen != Screen.Main || !IsActive)
        {
            _menuHover = -1;
            return;
        }

        _menuHover = -1;

        if (_openMenu != MenuKind.None)
        {
            string[] items = _openMenu == MenuKind.Game ? _gameMenuItems : _helpMenuItems;
            int x = _openMenu == MenuKind.Game ? _menuGameX : _menuHelpX;
            int y = MenuHeight;
            int width = 0;
            foreach (var item in items) width = Math.Max(width, (int)(_font.MeasureString(item).X * 0.75f));
            width += 24;

            if (x + width > GraphicsDevice.Viewport.Width) x = GraphicsDevice.Viewport.Width - width;
            if (x < 0) x = 0;

            for (int i = 0; i < items.Length; i++)
            {
                int itemY = y + 2 + i * 24;
                if (pos.Y >= itemY && pos.Y < itemY + 24 && pos.X >= x && pos.X < x + width)
                {
                    _menuHover = 2 + i;
                    return;
                }
            }

            Rectangle gameRect = new(_menuGameX, 0, _menuGameW, MenuHeight);
            Rectangle helpRect = new(_menuHelpX, 0, _menuHelpW, MenuHeight);

            if (gameRect.Contains(pos)) _menuHover = 0;
            else if (helpRect.Contains(pos)) _menuHover = 1;
        }
        else
        {
            Rectangle gameRect = new(_menuGameX, 0, _menuGameW, MenuHeight);
            Rectangle helpRect = new(_menuHelpX, 0, _menuHelpW, MenuHeight);

            if (gameRect.Contains(pos)) _menuHover = 0;
            else if (helpRect.Contains(pos)) _menuHover = 1;
        }
    }

    private void OnLeftUp(Point pos, GameTime gameTime)
    {
        if (_screen != Screen.Main)
        {
            HandleScreenClick(pos);
            return;
        }

        if (_openMenu != MenuKind.None)
        {
            HandleDropdownClick(pos);
            return;
        }

        Rectangle gameRect = new(_menuGameX, 0, _menuGameW, MenuHeight);
        Rectangle helpRect = new(_menuHelpX, 0, _menuHelpW, MenuHeight);

        if (gameRect.Contains(pos))
        {
            _openMenu = MenuKind.Game;
            return;
        }
        if (helpRect.Contains(pos))
        {
            _openMenu = MenuKind.Help;
            return;
        }

        if (_suppressNextMouseUp)
        {
            _suppressNextMouseUp = false;
            ResetSmileyForCurrentState();
            return;
        }

        if (SmileyRect.Contains(pos))
        {
            StartGame(_engine.Settings);
            return;
        }

        if (_chordCell is CellPoint chord)
        {
            _chordCell = null;
            _suppressNextMouseUp = true;
            _engine.Chord(chord);
            UpdateMineCounter();
            ResetSmileyForCurrentState();
            return;
        }

        ResetSmileyForCurrentState();

        var point = ToCellPoint(pos);
        if (!point.HasValue) return;

        if (IsControlDown())
        {
            _engine.CycleMark(point.Value);
        }
        else
        {
            bool isDouble = _lastClickPoint == point.Value
                         && (gameTime.TotalGameTime - _lastClickTime).TotalMilliseconds <= DoubleClickMs;

            if (isDouble)
            {
                _engine.Chord(point.Value);
                _lastClickPoint = null;
            }
            else
            {
                _engine.OpenCell(point.Value);
                _lastClickPoint = point.Value;
                _lastClickTime = gameTime.TotalGameTime;
            }
        }
        UpdateMineCounter();
    }

    private void HandleDropdownClick(Point pos)
    {
        string[] items = _openMenu == MenuKind.Game ? _gameMenuItems : _helpMenuItems;
        int x = _openMenu == MenuKind.Game ? _menuGameX : _menuHelpX;
        int y = MenuHeight;
        int width = 0;
        foreach (var item in items) width = Math.Max(width, (int)(_font.MeasureString(item).X * 0.75f));
        width += 24;

        if (x + width > GraphicsDevice.Viewport.Width) x = GraphicsDevice.Viewport.Width - width;
        if (x < 0) x = 0;

        for (int i = 0; i < items.Length; i++)
        {
            int itemY = y + 2 + i * 24;
            if (pos.Y >= itemY && pos.Y < itemY + 24 && pos.X >= x && pos.X < x + width)
            {
                if (items[i] != "-") ExecuteMenuItem(i, _openMenu);
                _openMenu = MenuKind.None;
                _menuHover = -1;
                return;
            }
        }
        _openMenu = MenuKind.None;
        _menuHover = -1;
    }

    private void ExecuteMenuItem(int index, MenuKind menuKind)
    {
        switch (menuKind)
        {
            case MenuKind.Game:
                switch (index)
                {
                    case 0: StartGame(_engine.Settings); break;
                    case 2: StartGame(DifficultyPresets.Beginner); break;
                    case 3: StartGame(DifficultyPresets.Intermediate); break;
                    case 4: StartGame(DifficultyPresets.Expert); break;
                    case 6: Exit(); break;
                }
                break;

            case MenuKind.Help:
                switch (index)
                {
                    case 0: _screen = Screen.Records; _recordsTab = 0; break;
                    case 2: _screen = Screen.About; break;
                }
                break;
        }
    }

    private void HandleScreenClick(Point pos)
    {
        if (_screen == Screen.Records)
        {
            if (_prevLevelRect.Contains(pos)) { _recordsTab = (_recordsTab + 2) % 3; return; }
            if (_nextLevelRect.Contains(pos)) { _recordsTab = (_recordsTab + 1) % 3; return; }
        }
        else if (_screen == Screen.About)
        {
            if (_githubLinkRect.Contains(pos))
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = "https://github.com/miptleha/cs-minesweeper", UseShellExecute = true });
                }
                catch { }
                return;
            }
        }

        if (_okButtonRect.Contains(pos))
        {
            if (_screen == Screen.NamePrompt) SubmitName();
            else _screen = Screen.Main;
        }
    }

    private void SubmitName()
    {
        string finalName = string.IsNullOrWhiteSpace(_nameBuffer) ? "Аноним" : _nameBuffer;

        _engine.PlayerName = finalName;
        if (_recordStore is JsonRecordStore json)
            json.RenameRecord(_engine.Difficulty, _engine.Elapsed.TotalSeconds, finalName);

        _screen = Screen.Records;
        _recordsTab = 0;
    }

    private void OnTextInput(object sender, TextInputEventArgs e)
    {
        if (_screen != Screen.NamePrompt) return;

        if (!char.IsControl(e.Character) && _nameBuffer.Length < 30)
        {
            _nameBuffer += e.Character;
        }
    }

    private void OnRightUp(Point pos)
    {
        if (_openMenu != MenuKind.None || _screen != Screen.Main) return;

        if (_suppressNextMouseUp)
        {
            _suppressNextMouseUp = false;
            return;
        }

        if (_chordCell is CellPoint chord)
        {
            _chordCell = null;
            _suppressNextMouseUp = true;
            _engine.Chord(chord);
            UpdateMineCounter();
            return;
        }

        var point = ToCellPoint(pos);
        if (!point.HasValue) return;

        _engine.CycleMark(point.Value);
        UpdateMineCounter();
    }

    private CellPoint? ToCellPoint(Point pos)
    {
        int x = pos.X / CellSize;
        int y = (pos.Y - MenuHeight - TopPanelHeight) / CellSize;
        if (x < 0 || y < 0) return null;
        if (x >= _engine.Width || y >= _engine.Height) return null;
        return new CellPoint(x, y);
    }

    private bool IsControlDown()
    {
        var kb = Keyboard.GetState();
        return kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);
    }

    private bool EscJustPressed()
    {
        var kb = Keyboard.GetState();
        return kb.IsKeyDown(Keys.Escape) && _prevKeyboard.IsKeyDown(Keys.Escape) == false;
    }

    private Rectangle BoardRect => new(0, MenuHeight + TopPanelHeight, _engine.Width * CellSize, _engine.Height * CellSize);
    private Rectangle SmileyRect => new((GraphicsDevice.Viewport.Width - SmileySize) / 2, MenuHeight + PanelTopMargin, SmileySize, SmileySize);

    private void ResetSmileyForCurrentState()
    {
        SetSmiley(_engine.State switch
        {
            GameState.Won => "smiley_win",
            GameState.Lost => "smiley_lose",
            _ => "smiley",
        });
    }

    protected override void Draw(GameTime gameTime)
    {
        try
        {
            GraphicsDevice.Clear(new Color(240, 240, 240));
            _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);

            try
            {
                DrawMenuBar();
                DrawTopPanel();
                DrawBoard();
                if (_openMenu != MenuKind.None && _screen == Screen.Main) DrawDropdown();
                DrawActiveScreen(gameTime);
            }
            finally
            {
                _spriteBatch.End();
            }
        }
        catch (Exception ex)
        {
            LogException("Draw", ex);
        }

        base.Draw(gameTime);
    }

    private void LogException(string context, Exception ex)
    {
        try
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            System.IO.File.AppendAllText(path, $"[{DateTime.Now}] {context}:\n{ex}\n\n");
        }
        catch { }
    }

    private void DrawRect(Color color, Rectangle rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        _spriteBatch.Draw(_solidColor, rect, color);
    }

    private void DrawMenuBar()
    {
        DrawRect(new Color(240, 240, 240), new Rectangle(0, 0, GraphicsDevice.Viewport.Width, MenuHeight));
        DrawMenuHeader("Игра", 0, _menuGameX, _menuGameW);
        DrawMenuHeader("Справка", 1, _menuHelpX, _menuHelpW);
    }

    private void DrawMenuHeader(string text, int menuIndex, int x, int w)
    {
        int y = 4;
        bool isOpen = (menuIndex == 0 && _openMenu == MenuKind.Game) || (menuIndex == 1 && _openMenu == MenuKind.Help);
        bool isHover = _menuHover == menuIndex;

        if (isOpen || isHover)
            DrawRect(new Color(0, 120, 215), new Rectangle(x, y, w, MenuHeight - 4));

        Color textColor = (isOpen || isHover) ? Color.White : Color.Black;
        _spriteBatch.DrawString(_font, text, new Vector2(x + 8, y + 1), textColor, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);
    }

    private void DrawDropdown()
    {
        string[] items = _openMenu == MenuKind.Game ? _gameMenuItems : _helpMenuItems;
        int x = _openMenu == MenuKind.Game ? _menuGameX : _menuHelpX;
        int y = MenuHeight;

        int width = 0;
        foreach (var item in items) width = Math.Max(width, (int)(_font.MeasureString(item).X * 0.75f));
        width += 24;
        int height = items.Length * 24 + 4;

        if (x + width > GraphicsDevice.Viewport.Width) x = GraphicsDevice.Viewport.Width - width;
        if (x < 0) x = 0;

        DrawRect(new Color(0, 0, 0, 50), new Rectangle(x + 2, y + 2, width, height));
        DrawRect(Color.White, new Rectangle(x, y, width, height));
        DrawRect(new Color(160, 160, 160), new Rectangle(x, y, width, 1));
        DrawRect(new Color(160, 160, 160), new Rectangle(x, y, 1, height));
        DrawRect(new Color(160, 160, 160), new Rectangle(x, y + height - 1, width, 1));
        DrawRect(new Color(160, 160, 160), new Rectangle(x + width - 1, y, 1, height));

        for (int i = 0; i < items.Length; i++)
        {
            int itemY = y + 2 + i * 24;
            if (items[i] == "-")
            {
                DrawRect(new Color(200, 200, 200), new Rectangle(x + 4, itemY + 11, width - 8, 1));
            }
            else
            {
                bool isHover = _menuHover == 2 + i;
                if (isHover)
                    DrawRect(new Color(0, 120, 215), new Rectangle(x + 1, itemY, width - 2, 24));

                Color textColor = isHover ? Color.White : Color.Black;
                _spriteBatch.DrawString(_font, items[i], new Vector2(x + 12, itemY + 3), textColor, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);
            }
        }
    }

    private void DrawTopPanel()
    {
        int mineCounterX = 10;
        int mineCounterY = MenuHeight + PanelTopMargin;
        int timerX = GraphicsDevice.Viewport.Width - 10 - 72;
        int timerY = MenuHeight + PanelTopMargin;

        DrawRect(Color.Black, new Rectangle(mineCounterX - 2, mineCounterY - 2, 76, 36));
        DrawRect(Color.Black, new Rectangle(timerX - 2, timerY - 2, 76, 36));

        DrawLed(_lastMines, mineCounterX, mineCounterY, true);

        int smileX = (GraphicsDevice.Viewport.Width - SmileySize) / 2;
        int smileY = MenuHeight + PanelTopMargin;

        bool isHover = SmileyRect.Contains(Mouse.GetState().Position) && _screen == Screen.Main && _openMenu == MenuKind.None;
        Color smileBg = isHover ? new Color(229, 241, 251) : new Color(240, 240, 240);
        Color smileBorder = isHover ? new Color(0, 120, 215) : new Color(160, 160, 160);

        DrawRect(smileBorder, new Rectangle(smileX - 1, smileY - 1, SmileySize + 2, SmileySize + 2));
        DrawRect(smileBg, new Rectangle(smileX, smileY, SmileySize, SmileySize));

        int smileDrawX = smileX + (SmileySize - 28) / 2;
        int smileDrawY = smileY + (SmileySize - 28) / 2;
        if (_tex.TryGetValue(_smiley, out Texture2D smileTex))
            _spriteBatch.Draw(smileTex, new Rectangle(smileDrawX, smileDrawY, 28, 28), Color.White);

        DrawLed(_lastSeconds, timerX, timerY, false);
    }

    private void DrawBoard()
    {
        for (int x = 0; x < _engine.Width; x++)
            for (int y = 0; y < _engine.Height; y++)
                DrawCell(_engine.GetCell(new CellPoint(x, y)));

        if (_engine.State == GameState.Won)
        {
            for (int x = 0; x < _engine.Width; x++)
                for (int y = 0; y < _engine.Height; y++)
                {
                    var cell = _engine.GetCell(new CellPoint(x, y));
                    if (cell.State == CellState.Closed || cell.State == CellState.Flagged || cell.State == CellState.Question)
                    {
                        var rect = new Rectangle(x * CellSize, MenuHeight + TopPanelHeight + y * CellSize, CellSize, CellSize);
                        _spriteBatch.Draw(_tex["cell_flag"], rect, Color.White);
                    }
                }
        }
    }

    private void DrawCell(CellInfo info)
    {
        var rect = new Rectangle(info.Position.X * CellSize, MenuHeight + TopPanelHeight + info.Position.Y * CellSize, CellSize, CellSize);

        switch (info.State)
        {
            case CellState.Closed: _spriteBatch.Draw(_tex["cell_closed"], rect, Color.White); break;
            case CellState.Flagged:
                _spriteBatch.Draw(_tex["cell_closed"], rect, Color.White);
                _spriteBatch.Draw(_tex["cell_flag"], rect, Color.White); break;
            case CellState.Question:
                _spriteBatch.Draw(_tex["cell_closed"], rect, Color.White);
                _spriteBatch.Draw(_tex["cell_question"], rect, Color.White); break;
            case CellState.WrongFlag: _spriteBatch.Draw(_tex["cell_wrong_flag"], rect, Color.White); break;
            case CellState.Opened:
                if (info.Position == _triggerCell) _spriteBatch.Draw(_tex["cell_exploded"], rect, Color.White);
                else
                {
                    _spriteBatch.Draw(_tex["cell_empty"], rect, Color.White);
                    if (info.Content == CellContent.Number && info.AdjacentMines >= 1 && info.AdjacentMines <= 8)
                        _spriteBatch.Draw(_tex[$"digit_{info.AdjacentMines}"], rect, Color.White);
                    else if (info.Content == CellContent.Mine)
                        _spriteBatch.Draw(_tex["bomb"], rect, Color.White);
                }
                break;
        }
    }

    private void DrawLed(int value, int x, int y, bool isMineCounter)
    {
        if (isMineCounter && value < 0)
        {
            _spriteBatch.Draw(_tex["minus"], new Rectangle(x, y, LedW, LedH), Color.White);
            var s = Math.Abs(value).ToString().PadLeft(2, '0');
            s = s.Substring(0, Math.Min(2, s.Length));
            for (int i = 0; i < 2; i++) _spriteBatch.Draw(_tex[$"timer_{s[i]}"], new Rectangle(x + LedW + i * LedW, y, LedW, LedH), Color.White);
            return;
        }

        var str = value.ToString().PadLeft(3, '0');
        if (str.Length > 3) str = str.Substring(str.Length - 3);
        for (int i = 0; i < 3; i++) _spriteBatch.Draw(_tex[$"timer_{str[i]}"], new Rectangle(x + i * LedW, y, LedW, LedH), Color.White);
    }

    private void DrawActiveScreen(GameTime gameTime)
    {
        if (_screen == Screen.Main) return;

        _spriteBatch.Draw(_solidColor, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 100));

        switch (_screen)
        {
            case Screen.Records: DrawRecordsScreen(); break;
            case Screen.About: DrawAboutScreen(); break;
            case Screen.NamePrompt: DrawNamePromptScreen(gameTime); break;
        }
    }

    private void DrawWindow(Rectangle rect)
    {
        DrawRect(new Color(0, 0, 0, 50), new Rectangle(rect.X + 5, rect.Y + 5, rect.Width, rect.Height));
        DrawRect(Color.White, rect);
        DrawRect(new Color(160, 160, 160), new Rectangle(rect.X, rect.Y, rect.Width, 1));
        DrawRect(new Color(160, 160, 160), new Rectangle(rect.X, rect.Y, 1, rect.Height));
        DrawRect(new Color(160, 160, 160), new Rectangle(rect.X, rect.Y + rect.Height - 1, rect.Width, 1));
        DrawRect(new Color(160, 160, 160), new Rectangle(rect.X + rect.Width - 1, rect.Y, 1, rect.Height));
    }

    private void DrawRecordsScreen()
    {
        int centerX = GraphicsDevice.Viewport.Width / 2;
        int w = Math.Min(300, GraphicsDevice.Viewport.Width - 20);
        int h = Math.Min(240, GraphicsDevice.Viewport.Height - 20);
        Rectangle window = new Rectangle(centerX - w / 2, 40, w, h);
        DrawWindow(window);

        _spriteBatch.DrawString(_font, "Лучшие результаты", new Vector2(window.X + 20, window.Y + 15), Color.Black, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);

        string[] levelNames = { "Новичок", "Любитель", "Профессионал" };
        string levelText = levelNames[_recordsTab];
        Vector2 levelSize = _font.MeasureString(levelText) * 0.75f;

        int selectorY = window.Y + 40;
        _prevLevelRect = new Rectangle(centerX - (int)levelSize.X / 2 - 30, selectorY, 24, 24);
        _nextLevelRect = new Rectangle(centerX + (int)levelSize.X / 2 + 6, selectorY, 24, 24);

        DrawRect(new Color(220, 220, 220), _prevLevelRect);
        _spriteBatch.DrawString(_font, "<", new Vector2(_prevLevelRect.X + 7, _prevLevelRect.Y + 2), Color.Black, 0, Vector2.Zero, 0.75f, SpriteEffects.None, 0);

        _spriteBatch.DrawString(_font, levelText, new Vector2(centerX - (int)levelSize.X / 2, selectorY + 3), Color.DarkBlue, 0, Vector2.Zero, 0.75f, SpriteEffects.None, 0);

        DrawRect(new Color(220, 220, 220), _nextLevelRect);
        _spriteBatch.DrawString(_font, ">", new Vector2(_nextLevelRect.X + 7, _nextLevelRect.Y + 2), Color.Black, 0, Vector2.Zero, 0.75f, SpriteEffects.None, 0);

        DifficultyLevel activeLevel = (DifficultyLevel)_recordsTab;
        var records = _recordStore.LoadRecords(activeLevel).ToList();
        float maxTextWidth = window.Width - 60; // Ограничение по ширине для строки

        for (int i = 0; i < 5; i++)
        {
            string prefix = (i + 1) + ". ";
            string timeSuffix = "";
            string name = "---";

            if (i < records.Count)
            {
                timeSuffix = " (" + records[i].TimeSeconds.ToString("F1") + " с)";
                name = string.IsNullOrWhiteSpace(records[i].PlayerName) ? "Аноним" : records[i].PlayerName;
            }

            string finalName = name;

            // Обрезаем имя, если оно слишком длинное, чтобы время всегда влезало
            if (i < records.Count)
            {
                while (_font.MeasureString(prefix + finalName + "..." + timeSuffix).X * 0.75f > maxTextWidth && finalName.Length > 0)
                {
                    finalName = finalName.Substring(0, finalName.Length - 1);
                }
                if (finalName != name) finalName += "...";
            }

            string fullText = prefix + finalName + timeSuffix;

            _spriteBatch.DrawString(_font, fullText, new Vector2(window.X + 40, window.Y + 80 + i * 24), Color.Black, 0, Vector2.Zero, 0.75f, SpriteEffects.None, 0);
        }

        DrawDialogButton(window.X + window.Width / 2 - 40, window.Y + window.Height - 40, "OK");
    }

    private void DrawAboutScreen()
    {
        int centerX = GraphicsDevice.Viewport.Width / 2;
        int w = Math.Min(320, GraphicsDevice.Viewport.Width - 20);
        int h = Math.Min(200, GraphicsDevice.Viewport.Height - 20);
        Rectangle window = new Rectangle(centerX - w / 2, 50, w, h);
        DrawWindow(window);

        if (_tex.TryGetValue("smiley_win", out Texture2D icon))
            _spriteBatch.Draw(icon, new Rectangle(window.X + 20, window.Y + 20, 32, 32), Color.White);

        _spriteBatch.DrawString(_font, "Сапёр", new Vector2(window.X + 60, window.Y + 20), Color.Black, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);

        _spriteBatch.DrawString(_font, "Классическая игра", new Vector2(window.X + 20, window.Y + 65), Color.Black, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, "Автор: miptleha.", new Vector2(window.X + 20, window.Y + 85), Color.Black, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);

        string linkText = "Скачать с GitHub";
        Vector2 linkSize = _font.MeasureString(linkText) * 0.75f;
        _githubLinkRect = new Rectangle(window.X + 20, window.Y + 115, (int)linkSize.X, (int)linkSize.Y);
        _spriteBatch.DrawString(_font, linkText, new Vector2(_githubLinkRect.X, _githubLinkRect.Y), Color.Blue, 0, Vector2.Zero, 0.75f, SpriteEffects.None, 0);
        DrawRect(Color.Blue, new Rectangle(_githubLinkRect.X, _githubLinkRect.Bottom - 1, _githubLinkRect.Width, 1));

        DrawDialogButton(window.X + window.Width / 2 - 40, window.Y + window.Height - 40, "OK");
    }

    private void DrawNamePromptScreen(GameTime gameTime)
    {
        int centerX = GraphicsDevice.Viewport.Width / 2;
        int centerY = GraphicsDevice.Viewport.Height / 2;
        int w = Math.Min(300, GraphicsDevice.Viewport.Width - 20);
        int h = Math.Min(140, GraphicsDevice.Viewport.Height - 20);
        Rectangle window = new Rectangle(centerX - w / 2, centerY - h / 2, w, h);
        DrawWindow(window);

        _spriteBatch.DrawString(_font, "Введите ваше имя:", new Vector2(window.X + 20, window.Y + 20), Color.Black, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);

        DrawRect(Color.White, new Rectangle(window.X + 20, window.Y + 50, window.Width - 40, 24));
        DrawRect(new Color(160, 160, 160), new Rectangle(window.X + 20, window.Y + 50, window.Width - 40, 1));
        DrawRect(new Color(160, 160, 160), new Rectangle(window.X + 20, window.Y + 50, 1, 24));
        DrawRect(new Color(160, 160, 160), new Rectangle(window.X + 20, window.Y + 73, window.Width - 40, 1));
        DrawRect(new Color(160, 160, 160), new Rectangle(window.X + window.Width - 21, window.Y + 50, 1, 24));

        Vector2 textSize = _font.MeasureString(_nameBuffer) * 0.75f;
        int textX = window.X + 24;
        int textY = window.Y + 53;

        _spriteBatch.DrawString(_font, _nameBuffer, new Vector2(textX, textY), Color.Black, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);

        // Мигающая каретка (мигает каждые 500 мс)
        if ((int)(gameTime.TotalGameTime.TotalMilliseconds / 500) % 2 == 0)
        {
            DrawRect(Color.Black, new Rectangle(textX + (int)textSize.X + 1, textY + 2, 1, 14));
        }

        DrawDialogButton(window.X + window.Width / 2 - 40, window.Y + window.Height - 40, "OK");
    }

    private void DrawDialogButton(int x, int y, string text)
    {
        int textWidth = (int)(_font.MeasureString(text).X * 0.75f);
        int w = textWidth + 32;
        int h = 28;
        Rectangle rect = new Rectangle(x, y, w, h);
        _okButtonRect = rect;

        bool isHover = Mouse.GetState().Position.X >= x && Mouse.GetState().Position.X < x + w
                    && Mouse.GetState().Position.Y >= y && Mouse.GetState().Position.Y < y + h;

        Color bgColor = isHover ? new Color(0, 120, 215) : new Color(225, 225, 225);
        Color borderColor = isHover ? new Color(0, 84, 153) : new Color(173, 173, 173);
        Color textColor = isHover ? Color.White : Color.Black;

        DrawRect(borderColor, rect);
        DrawRect(bgColor, new Rectangle(x + 1, y + 1, w - 2, h - 2));
        _spriteBatch.DrawString(_font, text, new Vector2(x + (w - textWidth) / 2, y + 6), textColor, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);
    }
}
