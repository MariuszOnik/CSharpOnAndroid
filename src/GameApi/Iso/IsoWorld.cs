// Matematyka izometrii – port IsoWorld.IsoToScreen / ScreenToIso z unity-iso-tools (MIT, patrz THIRD-PARTY-NOTICES.md).
// Unity: macierz = Scale(1, ratio, 1) * TRS(obrót o (90 - angle) wokół Z, skala tileSize*√2, tileHeight).
// Tu ta sama macierz rozpisana ręcznie (2x2 + wysokość), a oś Y odwrócona, bo w MonoGame Y rośnie w dół.
using System;
using Microsoft.Xna.Framework;

namespace GameApi;

/// <summary>Przelicza współrzędne izometryczne (x, y, z w kratkach) na piksele ekranu i z powrotem.</summary>
public class IsoWorld
{
    public float TileSize { get; }
    public float TileHeight { get; }
    public float TileRatio { get; }
    public float TileAngle { get; }

    /// <summary>Gdzie na ekranie leży punkt izo (0, 0, 0).</summary>
    public Vector2 Origin { get; set; }

    // Macierz 2x2: ekran(x, y w górę) = M * izo(x, y)
    internal readonly float M11, M12, M21, M22;
    readonly float _i11, _i12, _i21, _i22; // odwrotność

    public IsoWorld(float tileSize = 32f, float tileHeight = 32f, float tileRatio = 0.5f, float tileAngle = 45f)
    {
        TileSize = tileSize;
        TileHeight = tileHeight;
        TileRatio = tileRatio;
        TileAngle = tileAngle;

        double angle = (90.0 - tileAngle) * Math.PI / 180.0;
        float cos = (float)Math.Cos(angle), sin = (float)Math.Sin(angle);
        float scale = tileSize * MathF.Sqrt(2f);

        M11 = cos * scale;
        M12 = -sin * scale;
        M21 = sin * scale * tileRatio;
        M22 = cos * scale * tileRatio;

        float det = M11 * M22 - M12 * M21;
        _i11 = M22 / det;
        _i12 = -M12 / det;
        _i21 = -M21 / det;
        _i22 = M11 / det;
    }

    /// <summary>Punkt izo (x, y, z) → piksel ekranu.</summary>
    public Vector2 ToScreen(Vector3 iso)
    {
        float x = M11 * iso.X + M12 * iso.Y;
        float y = M21 * iso.X + M22 * iso.Y + iso.Z * TileHeight;
        return new Vector2(Origin.X + x, Origin.Y - y);
    }

    /// <summary>Piksel ekranu → punkt izo na wysokości z (domyślnie podłoga).</summary>
    public Vector3 ToIso(Vector2 screen, float z = 0f)
    {
        float x = screen.X - Origin.X;
        float y = Origin.Y - screen.Y - z * TileHeight;
        return new Vector3(_i11 * x + _i12 * y, _i21 * x + _i22 * y, z);
    }
}
