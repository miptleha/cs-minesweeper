using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MinesweeperFna;

/// <summary>
/// Каркас игры «Сапёр» на FNA. Пока что — пустое окно с фирменным фоном.
/// </summary>
public class MinesweeperGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;

    public MinesweeperGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        // Заголовок окна задаётся после инициализации окна.
        Window.Title = "Сапёр";

        base.Initialize();
    }

    private Texture2D? _smiley;

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // DoD: минимум один ассет реально загружается
        using var png = new MemoryStream(Assets.GetPng("smiley"));
        _smiley = Texture2D.FromStream(GraphicsDevice, png);
    }

    protected override void Update(GameTime gameTime)
    {
        // Выход по Escape (проверка геймпада в болванке не нужна).
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        base.Draw(gameTime);
    }
}
