using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameApi;

/// <summary>Rysuje tekst z pliku TTF. Znaki trafiają do wspólnej tekstury, więc zmieniające się napisy nic nie kosztują.</summary>
sealed class TextRenderer
{
    readonly FontSystem _fonts = new();

    public TextRenderer(byte[] ttf) => _fonts.AddFont(ttf);

    public void Draw(SpriteBatch batch, string text, Vector2 position, float size, Color color) =>
        _fonts.GetFont(size).DrawText(batch, text, position, color);

    public Vector2 Measure(string text, float size) => _fonts.GetFont(size).MeasureString(text);
}
