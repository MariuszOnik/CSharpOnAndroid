using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameApi;

/// <summary>
/// Fragment obrazka (np. jedna klatka z atlasu) z punktem zaczepienia (pivot).
/// Pivot jest względny: (0, 0) = lewy górny róg, (0.5, 0.5) = środek, (0.5, 1) = środek dołu (stopy postaci).
/// </summary>
public class Sprite
{
    public Texture2D Texture { get; }

    /// <summary>Prostokąt wycinka w pikselach obrazka.</summary>
    public Rectangle Source { get; }

    /// <summary>Punkt zaczepienia względem wycinka, 0..1. Domyślnie środek.</summary>
    public Vector2 Pivot { get; set; }

    public int Width => Source.Width;
    public int Height => Source.Height;

    /// <param name="texture">Obrazek (atlas).</param>
    /// <param name="source">Wycinek; null = cały obrazek.</param>
    /// <param name="pivot">Punkt zaczepienia 0..1; null = środek.</param>
    public Sprite(Texture2D texture, Rectangle? source = null, Vector2? pivot = null)
    {
        Texture = texture;
        Source = source ?? texture.Bounds;
        Pivot = pivot ?? new Vector2(0.5f);
    }

    /// <summary>Ustawia pivot w pikselach wycinka, np. SetPivotPixels(16, 30) dla stóp postaci 32×32.</summary>
    public Sprite SetPivotPixels(float x, float y)
    {
        Pivot = new Vector2(x / Width, y / Height);
        return this;
    }

    /// <summary>Ta sama klatka z innym pivotem (np. dla pocisku i dla postaci z jednego atlasu).</summary>
    public Sprite WithPivot(float x, float y) => new(Texture, Source, new Vector2(x, y));

    /// <summary>
    /// Tnie atlas na siatkę klatek frameWidth×frameHeight, wierszami od lewego górnego rogu.
    /// margin – odstęp od brzegu obrazka, spacing – odstęp między klatkami (w pikselach).
    /// </summary>
    public static Sprite[] Slice(Texture2D atlas, int frameWidth, int frameHeight, Vector2? pivot = null, int margin = 0, int spacing = 0)
    {
        if (frameWidth <= 0 || frameHeight <= 0)
            throw new ArgumentException("Rozmiar klatki musi być większy od zera.");

        var frames = new List<Sprite>();
        for (int y = margin; y + frameHeight <= atlas.Height - margin; y += frameHeight + spacing)
            for (int x = margin; x + frameWidth <= atlas.Width - margin; x += frameWidth + spacing)
                frames.Add(new Sprite(atlas, new Rectangle(x, y, frameWidth, frameHeight), pivot));
        return frames.ToArray();
    }

    /// <summary>
    /// Klatka animacji dla czasu time (sekundy) przy fps klatkach na sekundę.
    /// loop = false zatrzymuje się na ostatniej klatce.
    /// </summary>
    public static Sprite Animate(IReadOnlyList<Sprite> frames, float time, float fps, bool loop = true)
    {
        int index = (int)MathF.Floor(time * fps);
        index = loop ? ((index % frames.Count) + frames.Count) % frames.Count : Math.Clamp(index, 0, frames.Count - 1);
        return frames[index];
    }
}
