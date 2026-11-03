using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

[assembly: InternalsVisibleTo("GamePlayer")]

namespace GameApi;

/// <summary>
/// Klasa bazowa dla gry pisanej na telefonie. Nadpisz Start, Update i Draw.
/// </summary>
public abstract class GameScript
{
    public GraphicsDevice GraphicsDevice { get; private set; } = null!;
    public SpriteBatch Batch { get; private set; } = null!;

    /// <summary>Biały piksel 1x1 – do rysowania prostokątów w dowolnym kolorze.</summary>
    public Texture2D Pixel { get; private set; } = null!;

    public int Width => GraphicsDevice.Viewport.Width;
    public int Height => GraphicsDevice.Viewport.Height;

    /// <summary>Kolor tła czyszczony przed każdą klatką.</summary>
    public Color Background { get; set; } = Color.Black;

    public Random Random { get; } = new();

    public TouchCollection Touches { get; private set; }
    public bool IsTouching => Touches.Count > 0;
    public Vector2 TouchPosition => Touches.Count > 0 ? Touches[0].Position : Vector2.Zero;

    /// <summary>Wywoływane raz, po przygotowaniu grafiki.</summary>
    public virtual void Start() { }

    /// <summary>Logika gry. dt to czas od poprzedniej klatki w sekundach.</summary>
    public virtual void Update(float dt) { }

    /// <summary>Rysowanie. Batch jest już otwarty (Begin/End robi host).</summary>
    public virtual void Draw() { }

    public void FillRect(float x, float y, float width, float height, Color color) =>
        Batch.Draw(Pixel, new Rectangle((int)x, (int)y, (int)width, (int)height), color);

    public void FillRect(Rectangle rect, Color color) => Batch.Draw(Pixel, rect, color);

    /// <summary>
    /// Wczytuje obrazek PNG/JPG po nazwie, np. LoadImage("pilka.png").
    /// Najpierw szuka we własnych obrazkach gracza, potem we wbudowanych.
    /// </summary>
    public Texture2D LoadImage(string name)
    {
        using var stream = _openImage(name);
        return Texture2D.FromStream(GraphicsDevice, stream, DefaultColorProcessors.PremultiplyAlpha);
    }

    /// <summary>Rysuje obrazek w punkcie (x, y) – lewy górny róg.</summary>
    public void DrawImage(Texture2D image, float x, float y, float scale = 1f) =>
        Batch.Draw(image, new Vector2(x, y), null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

    /// <summary>Rysuje obrazek wyśrodkowany w punkcie, z obrotem (radiany) i skalą.</summary>
    public void DrawImageCentered(Texture2D image, Vector2 center, float rotation = 0f, float scale = 1f) =>
        Batch.Draw(image, center, null, Color.White, rotation, new Vector2(image.Width / 2f, image.Height / 2f), scale, SpriteEffects.None, 0f);

    /// <summary>Rysuje obrazek rozciągnięty do prostokąta.</summary>
    public void DrawImage(Texture2D image, Rectangle destination) => Batch.Draw(image, destination, Color.White);

    Func<string, System.IO.Stream> _openImage = null!;

    internal void Attach(GraphicsDevice device, SpriteBatch batch, Texture2D pixel, Func<string, System.IO.Stream> openImage)
    {
        GraphicsDevice = device;
        Batch = batch;
        Pixel = pixel;
        _openImage = openImage;
    }

    internal void SetTouches(TouchCollection touches) => Touches = touches;
}
