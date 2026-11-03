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

    internal void Attach(GraphicsDevice device, SpriteBatch batch, Texture2D pixel)
    {
        GraphicsDevice = device;
        Batch = batch;
        Pixel = pixel;
    }

    internal void SetTouches(TouchCollection touches) => Touches = touches;
}
