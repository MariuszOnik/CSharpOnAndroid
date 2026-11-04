// Generuje sprite prostopadłościanu w rzucie izometrycznym (piksel po pikselu).
// Biały z cieniowaniem – kolor nadaje się przy rysowaniu.
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameApi;

public static class IsoSprite
{
    /// <summary>
    /// Tworzy teksturę bryły o rozmiarze size (x, y, z) w kratkach.
    /// offset = przesunięcie lewego górnego rogu tekstury względem ekranowej pozycji minimalnego rogu bryły.
    /// </summary>
    public static Texture2D CreateBox(GraphicsDevice device, IsoWorld world, Vector3 size, out Vector2 offset)
    {
        var pixels = RenderBox(world, size, out int width, out int height, out offset);
        var texture = new Texture2D(device, width, height);
        texture.SetData(pixels);
        return texture;
    }

    internal static Color[] RenderBox(IsoWorld world, Vector3 size, out int width, out int height, out Vector2 offset)
    {
        // Rogi bryły względem jej minimalnego rogu, w pikselach (y w dół).
        var zero = world.ToScreen(Vector3.Zero);
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            var p = world.ToScreen(new Vector3((i & 1) * size.X, ((i >> 1) & 1) * size.Y, ((i >> 2) & 1) * size.Z)) - zero;
            minX = Math.Min(minX, p.X);
            maxX = Math.Max(maxX, p.X);
            minY = Math.Min(minY, p.Y);
            maxY = Math.Max(maxY, p.Y);
        }

        width = (int)Math.Ceiling(maxX - minX) + 2;
        height = (int)Math.Ceiling(maxY - minY) + 2;
        offset = new Vector2(minX - 1, minY - 1);

        float edge = 1.4f / world.TileSize; // grubość krawędzi w kratkach
        var pixels = new Color[width * height];
        for (int j = 0; j < height; j++)
            for (int i = 0; i < width; i++)
            {
                float screenX = offset.X + i + 0.5f;
                float screenY = -(offset.Y + j + 0.5f); // y w górę, jak w Unity
                int brightness = FaceBrightness(world, size, screenX, screenY, edge);
                if (brightness > 0)
                    pixels[j * width + i] = new Color(brightness, brightness, brightness, 255);
            }
        return pixels;
    }

    // Zwraca jasność widocznej ściany pod pikselem albo 0, gdy piksel jest poza bryłą.
    static int FaceBrightness(IsoWorld w, Vector3 size, float screenX, float screenY, float edge)
    {
        float height = w.TileHeight;
        float det = w.M11 * w.M22 - w.M12 * w.M21;

        // Góra (z = size.Z)
        {
            float py = screenY - size.Z * height;
            float x = (w.M22 * screenX - w.M12 * py) / det;
            float y = (-w.M21 * screenX + w.M11 * py) / det;
            if (x >= 0 && x <= size.X && y >= 0 && y <= size.Y)
                return IsEdge(x, size.X, y, size.Y, edge) ? 110 : 255;
        }
        // Ściana y = 0 (prawa)
        if (Math.Abs(w.M11) > 1e-4f)
        {
            float x = screenX / w.M11;
            float z = (screenY - w.M21 * x) / height;
            if (x >= 0 && x <= size.X && z >= 0 && z <= size.Z)
                return IsEdge(x, size.X, z, size.Z, edge * w.TileSize / height) ? 80 : 200;
        }
        // Ściana x = 0 (lewa)
        if (Math.Abs(w.M12) > 1e-4f)
        {
            float y = screenX / w.M12;
            float z = (screenY - w.M22 * y) / height;
            if (y >= 0 && y <= size.Y && z >= 0 && z <= size.Z)
                return IsEdge(y, size.Y, z, size.Z, edge * w.TileSize / height) ? 60 : 145;
        }
        return 0;
    }

    static bool IsEdge(float a, float maxA, float b, float maxB, float edge) =>
        a < edge || a > maxA - edge || b < edge || b > maxB - edge;
}
