using System;
using System.Collections.Generic;
using System.IO;
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

    /// <summary>Narysowane klatki na sekundę. Przy przeciążeniu MonoGame pomija Draw, więc liczenie w Update zawyżałoby wynik.</summary>
    public float Fps { get; private set; }

    // ---------- Dotyk ----------

    public TouchCollection Touches { get; private set; }

    /// <summary>Palec jest na ekranie (przez cały czas dotyku).</summary>
    public bool IsTouching => Touches.Count > 0;

    /// <summary>Palec właśnie dotknął ekranu – prawda tylko w jednej klatce.</summary>
    public bool JustTouched => HasTouch(TouchLocationState.Pressed);

    /// <summary>Palec właśnie został podniesiony – prawda tylko w jednej klatce.</summary>
    public bool JustReleased => HasTouch(TouchLocationState.Released);

    public Vector2 TouchPosition => Touches.Count > 0 ? Touches[0].Position : Vector2.Zero;

    bool HasTouch(TouchLocationState state)
    {
        foreach (var touch in Touches)
            if (touch.State == state)
                return true;
        return false;
    }

    // ---------- Cykl życia ----------

    /// <summary>Wywoływane raz, po przygotowaniu grafiki.</summary>
    public virtual void Start() { }

    /// <summary>Logika gry. dt to czas od poprzedniej klatki w sekundach.</summary>
    public virtual void Update(float dt) { }

    /// <summary>Rysowanie. Batch jest już otwarty (Begin/End robi host).</summary>
    public virtual void Draw() { }

    // ---------- Prostokąty ----------

    public void FillRect(float x, float y, float width, float height, Color color) =>
        Batch.Draw(Pixel, new Rectangle((int)x, (int)y, (int)width, (int)height), color);

    public void FillRect(Rectangle rect, Color color) => Batch.Draw(Pixel, rect, color);

    // ---------- Tekst ----------

    /// <summary>
    /// Rysuje tekst. (x, y) to lewy górny róg, size to wysokość czcionki w pikselach. Obsługuje polskie znaki.
    /// Przykład: DrawText($"Wynik: {score}", 30, 80, 48, Color.White);
    /// </summary>
    public void DrawText(string text, float x, float y, float size, Color color)
    {
        if (!string.IsNullOrEmpty(text))
            _text.Draw(Batch, text, new Vector2(x, y), size, color);
    }

    /// <summary>Rysuje tekst wyśrodkowany w punkcie (x, y).</summary>
    public void DrawTextCentered(string text, float x, float y, float size, Color color)
    {
        if (string.IsNullOrEmpty(text))
            return;
        var measured = _text.Measure(text, size);
        _text.Draw(Batch, text, new Vector2(x - measured.X / 2, y - measured.Y / 2), size, color);
    }

    /// <summary>Zwraca szerokość i wysokość tekstu w pikselach.</summary>
    public Vector2 MeasureText(string text, float size) =>
        string.IsNullOrEmpty(text) ? Vector2.Zero : _text.Measure(text, size);

    // ---------- Obrazki ----------

    /// <summary>
    /// Wczytuje obrazek PNG/JPG po nazwie, np. LoadImage("pilka.png").
    /// Szuka w images/ projektu, potem we wspólnym CsharpGame/images, potem we wbudowanych.
    /// Można też podać pełną ścieżkę, np. "/sdcard/Pictures/kot.png".
    /// </summary>
    public Texture2D LoadImage(string name)
    {
        using var stream = _openImage(name);
        return Texture2D.FromStream(GraphicsDevice, stream, DefaultColorProcessors.PremultiplyAlpha);
    }

    /// <summary>
    /// Nazwy obrazków pasujących do wzorca (także z podfolderów), gotowe dla LoadImage.
    /// Przeszukuje images/ projektu i wspólne CsharpGame/images. Przykład: ListImages("tile_*.png").
    /// </summary>
    public IReadOnlyList<string> ListImages(string pattern = "*.png") => _listImages(pattern);

    /// <summary>Rysuje obrazek w punkcie (x, y) – lewy górny róg.</summary>
    public void DrawImage(Texture2D image, float x, float y, float scale = 1f) =>
        Batch.Draw(image, new Vector2(x, y), null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

    /// <summary>Rysuje obrazek wyśrodkowany w punkcie, z obrotem (radiany) i skalą.</summary>
    public void DrawImageCentered(Texture2D image, Vector2 center, float rotation = 0f, float scale = 1f) =>
        Batch.Draw(image, center, null, Color.White, rotation, new Vector2(image.Width / 2f, image.Height / 2f), scale, SpriteEffects.None, 0f);

    /// <summary>Rysuje obrazek rozciągnięty do prostokąta.</summary>
    public void DrawImage(Texture2D image, Rectangle destination) => Batch.Draw(image, destination, Color.White);

    // ---------- Podłączenie do hosta ----------

    Func<string, Stream> _openImage = null!;
    Func<string, IReadOnlyList<string>> _listImages = null!;
    TextRenderer _text = null!;

    internal void Attach(GraphicsDevice device, SpriteBatch batch, Texture2D pixel,
        Func<string, Stream> openImage, Func<string, IReadOnlyList<string>> listImages, byte[] font)
    {
        GraphicsDevice = device;
        Batch = batch;
        Pixel = pixel;
        _openImage = openImage;
        _listImages = listImages;
        _text = new TextRenderer(font);
    }

    internal void SetTouches(TouchCollection touches) => Touches = touches;

    internal void SetFps(float fps) => Fps = fps;
}
