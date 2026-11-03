using Android.App;
using GameApi;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace GamePlayer;

/// <summary>Gra MonoGame, która uruchamia skompilowany GameScript.</summary>
public class GameHost : Game
{
    /// <summary>Typ skryptu do uruchomienia – ustawiany przez edytor przed startem GameActivity.</summary>
    public static Type? PendingScriptType;

    static string? _runtimeError;

    public static string? TakeRuntimeError() => Interlocked.Exchange(ref _runtimeError, null);

    readonly Activity _activity;
    SpriteBatch _batch = null!;
    Texture2D _pixel = null!;
    GameScript? _script;

    public GameHost(Activity activity)
    {
        _activity = activity;
        new GraphicsDeviceManager(this) { IsFullScreen = true };
        Content.RootDirectory = "Content";
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);

        if (PendingScriptType == null)
        {
            Fail("brak skompilowanego skryptu");
            return;
        }

        Guard(() =>
        {
            _script = (GameScript)Activator.CreateInstance(PendingScriptType)!;
            _script.Attach(GraphicsDevice, _batch, _pixel, OpenImage);
            _script.Start();
        });
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
        {
            _activity.Finish();
            return;
        }

        if (_script != null)
        {
            _script.SetTouches(TouchPanel.GetState());
            Guard(() => _script.Update((float)gameTime.ElapsedGameTime.TotalSeconds));
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(_script?.Background ?? Color.Black);
        if (_script != null)
        {
            _batch.Begin();
            Guard(() => _script.Draw());
            _batch.End();
        }

        base.Draw(gameTime);
    }

    /// <summary>Folder na własne obrazki gracza (na razie wgrywane ręcznie, np. przez adb).</summary>
    public static string UserImagesDir(Activity activity) => Path.Combine(activity.FilesDir!.AbsolutePath, "images");

    // Strumień musi być przewijalny, a strumienie z Assets nie są – kopiujemy do pamięci.
    Stream OpenImage(string name)
    {
        var userFile = Path.Combine(UserImagesDir(_activity), name);
        if (File.Exists(userFile))
            return File.OpenRead(userFile);

        try
        {
            using var asset = _activity.Assets!.Open("images/" + name);
            var memory = new MemoryStream();
            asset.CopyTo(memory);
            memory.Position = 0;
            return memory;
        }
        catch (Java.IO.FileNotFoundException)
        {
            throw new FileNotFoundException($"Nie ma obrazka '{name}'.");
        }
    }

    // Wyjątek w kodzie gracza nie może wywalić aplikacji: wracamy do edytora z opisem błędu.
    void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Fail(e.GetType().Name + ": " + e.Message);
        }
    }

    void Fail(string message)
    {
        _script = null;
        _runtimeError = message;
        _activity.Finish();
    }
}
