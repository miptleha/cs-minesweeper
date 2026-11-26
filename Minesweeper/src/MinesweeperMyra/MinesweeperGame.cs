using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D.UI;

namespace MinesweeperMyra;

public class MinesweeperGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private Desktop? _desktop;
    private Texture2D? _smiley;

    public MinesweeperGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        Window.Title = "Сапёр";

        // Myra инициализируется поверх MonoGame
        MyraEnvironment.Game = this;
        _desktop = new Desktop();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        // DoD: минимум один ассет реально загружается
        _smiley = Content.Load<Texture2D>("smiley");
        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        _desktop?.Render();

        base.Draw(gameTime);
    }
}
