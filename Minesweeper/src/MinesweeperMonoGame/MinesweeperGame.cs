using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MinesweeperMonoGame;

public class MinesweeperGame : Game
{
    private GraphicsDeviceManager _graphics;

    /// <summary>
    /// Менеджер графики.
    /// </summary>
    public MinesweeperGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        // Размер окна по умолчанию — 9×9 клеток по 32 пикселя + запас.
        // Точные значения будут уточнены в Phase 6.
        _graphics.PreferredBackBufferWidth = 320;
        _graphics.PreferredBackBufferHeight = 360;
        _graphics.ApplyChanges();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        // TODO: загрузка контента через this.Content
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        // TODO: логика обновления

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        // TODO: отрисовка

        base.Draw(gameTime);
    }
}
