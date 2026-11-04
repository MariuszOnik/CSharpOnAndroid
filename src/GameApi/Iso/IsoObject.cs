using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameApi;

/// <summary>Prostopadłościan w świecie izometrycznym – jednostka sortowania.</summary>
public class IsoObject
{
    static int _nextId;

    public string? Name;

    /// <summary>Minimalny róg w kratkach izo.</summary>
    public Vector3 Position;

    /// <summary>Szerokość (x), głębokość (y), wysokość (z) w kratkach.</summary>
    public Vector3 Size;

    public Color Color = Color.White;
    public Texture2D? Sprite;

    /// <summary>Ruchome obiekty są sortowane co klatkę, a zależności nieruchomych liczone są raz.</summary>
    public bool IsMoving;

    /// <summary>Liczba zależności po ostatnim IsoSorter.SortAllPairs.</summary>
    public int DependencyCount => Behind.Count;

    internal readonly int Id = _nextId++;

    // Prostokąt na ekranie w pikselach świata.
    internal float MinX, MinY, MaxX, MaxY;

    // SortAllPairs (odpowiednik IsoObject.Internal)
    internal readonly List<IsoObject> Behind = new();
    internal bool Placed;
    internal float Depth;

    // IsoSorter.Sort
    internal readonly List<IsoObject> StaticBehind = new();
    internal readonly List<IsoObject> MovingBehind = new();
    internal int VisibleFrame, PlacedFrame, CheckedMark, MovingFrame;
}
