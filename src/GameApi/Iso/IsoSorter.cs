// Sortowanie izometryczne – port z unity-iso-tools (MIT, patrz THIRD-PARTY-NOTICES.md):
//  • IsBehind – 1:1 z IsoScreenSolver.IsIsoObjectDepends,
//  • Place – logika RecursivePlaceIsoObject z IsoSortingSolver.
// Optymalizacje Sort są te same co w oryginale:
//  1. pary sprawdzamy tylko, gdy prostokąty obiektów na ekranie się nakładają
//     (oryginał: IsoQuadTree, tu: siatka kubełków – działa tak samo dla mapy kafelkowej),
//  2. zależności między nieruchomymi liczone raz i trzymane w pamięci (oryginał: flaga Dirty),
//  3. sortujemy i rysujemy tylko widoczne obiekty (oryginał: curVisibles).
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace GameApi;

/// <summary>Układa obiekty izometryczne w kolejności rysowania.</summary>
public class IsoSorter
{
    const float CellSize = 132f; // rozmiar kubełka w pikselach świata

    readonly IsoWorld _world;
    readonly Dictionary<long, List<IsoObject>> _staticGrid = new();
    readonly Dictionary<long, List<IsoObject>> _movingGrid = new();
    readonly List<IsoObject> _visible = new();
    readonly List<IsoObject> _order = new();
    bool _staticReady;
    int _frame, _mark;

    /// <summary>Statystyki ostatniego Sort.</summary>
    public long PairCount { get; private set; }
    public long DependencyCount { get; private set; }
    public long StaticPairCount { get; private set; }
    public int VisibleCount { get; private set; }

    /// <param name="world">Świat w skali 1:1. Widok przekazany do Sort jest w jego pikselach.</param>
    public IsoSorter(IsoWorld world) => _world = world;

    /// <summary>Wywołaj po dodaniu, usunięciu lub przesunięciu nieruchomych obiektów (np. nowa mapa).</summary>
    public void InvalidateStatic() => _staticReady = false;

    /// <summary>
    /// Zwraca widoczne obiekty w kolejności rysowania (od najdalszego).
    /// Widok to prostokąt ekranu w pikselach świata. Lista jest używana ponownie w następnym wywołaniu.
    /// </summary>
    public List<IsoObject> Sort(List<IsoObject> objects, float viewMinX, float viewMinY, float viewMaxX, float viewMaxY)
    {
        _frame++;
        PairCount = 0;
        DependencyCount = 0;
        if (!_staticReady)
            BuildStatic(objects);

        foreach (var cell in _movingGrid.Values)
            cell.Clear();

        _visible.Clear();
        foreach (var o in objects)
        {
            if (o.IsMoving)
            {
                UpdateBounds(o);
                Insert(_movingGrid, o);
            }
            if (o.MaxX > viewMinX && o.MinX < viewMaxX && o.MaxY > viewMinY && o.MinY < viewMaxY)
            {
                o.VisibleFrame = _frame;
                _visible.Add(o);
            }
        }
        VisibleCount = _visible.Count;

        // Ruchome kontra wszystko, co leży w tych samych kubełkach.
        foreach (var moving in _visible)
        {
            if (!moving.IsMoving)
                continue;

            _mark++;
            int x0 = Cell(moving.MinX), x1 = Cell(moving.MaxX);
            int y0 = Cell(moving.MinY), y1 = Cell(moving.MaxY);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    var key = Key(x, y);
                    if (_staticGrid.TryGetValue(key, out var statics))
                        Check(moving, statics);
                    if (_movingGrid.TryGetValue(key, out var movings))
                        Check(moving, movings);
                }
        }

        _order.Clear();
        foreach (var o in _visible)
            Place(o);
        _order.Reverse(); // od najdalszego
        return _order;
    }

    /// <summary>Przelicza prostokąt obiektu na ekranie.</summary>
    public void UpdateBounds(IsoObject o)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            var p = _world.ToScreen(new Vector3(
                o.Position.X + (i & 1) * o.Size.X,
                o.Position.Y + ((i >> 1) & 1) * o.Size.Y,
                o.Position.Z + ((i >> 2) & 1) * o.Size.Z));
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }
        o.MinX = minX;
        o.MinY = minY;
        o.MaxX = maxX;
        o.MaxY = maxY;
    }

    /// <summary>Czy prostokąty obiektów na ekranie się nakładają (po UpdateBounds).</summary>
    public static bool Overlap(IsoObject a, IsoObject b) =>
        a.MaxX > b.MinX && a.MinX < b.MaxX && a.MaxY > b.MinY && a.MinY < b.MaxY;

    static int Cell(float pixels) => (int)MathF.Floor(pixels / CellSize);

    static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;

    static void Insert(Dictionary<long, List<IsoObject>> grid, IsoObject o)
    {
        int x0 = Cell(o.MinX), x1 = Cell(o.MaxX);
        int y0 = Cell(o.MinY), y1 = Cell(o.MaxY);
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            {
                var key = Key(x, y);
                if (!grid.TryGetValue(key, out var cell))
                    grid[key] = cell = new List<IsoObject>();
                cell.Add(o);
            }
    }

    void BuildStatic(List<IsoObject> objects)
    {
        _staticGrid.Clear();
        StaticPairCount = 0;
        foreach (var o in objects)
        {
            o.StaticBehind.Clear();
            if (o.IsMoving)
                continue;
            UpdateBounds(o);
            Insert(_staticGrid, o);
        }

        foreach (var cell in _staticGrid.Values)
            for (int i = 0; i < cell.Count; i++)
                for (int j = i + 1; j < cell.Count; j++)
                {
                    var a = cell[i];
                    var b = cell[j];
                    // Ta sama para może być w kilku kubełkach – liczymy ją tylko w kubełku, w którym zaczyna się ich część wspólna.
                    if (!IsFirstSharedCell(a, b, cell) || !Overlap(a, b))
                        continue;
                    StaticPairCount++;
                    if (IsBehind(a.Position, a.Size, b.Position, b.Size))
                        a.StaticBehind.Add(b);
                    if (IsBehind(b.Position, b.Size, a.Position, a.Size))
                        b.StaticBehind.Add(a);
                }
        _staticReady = true;
    }

    bool IsFirstSharedCell(IsoObject a, IsoObject b, List<IsoObject> cell)
    {
        var key = Key(Cell(MathF.Max(a.MinX, b.MinX)), Cell(MathF.Max(a.MinY, b.MinY)));
        return _staticGrid.TryGetValue(key, out var first) && ReferenceEquals(first, cell);
    }

    void Check(IsoObject moving, List<IsoObject> cell)
    {
        foreach (var other in cell)
        {
            if (other == moving || other.CheckedMark == _mark)
                continue;
            other.CheckedMark = _mark;
            // Para dwóch widocznych ruchomych – liczymy tylko raz.
            if (other.IsMoving && other.VisibleFrame == _frame && other.Id < moving.Id)
                continue;
            if (!Overlap(moving, other))
                continue;
            PairCount++;
            if (IsBehind(moving.Position, moving.Size, other.Position, other.Size))
            {
                AddMovingDependency(moving, other);
                DependencyCount++;
            }
            if (IsBehind(other.Position, other.Size, moving.Position, moving.Size))
            {
                AddMovingDependency(other, moving);
                DependencyCount++;
            }
        }
    }

    void AddMovingDependency(IsoObject o, IsoObject behind)
    {
        if (o.MovingFrame != _frame)
        {
            o.MovingBehind.Clear();
            o.MovingFrame = _frame;
        }
        o.MovingBehind.Add(behind);
    }

    // RecursivePlaceIsoObject: najpierw obiekty przed nami, potem my.
    void Place(IsoObject o)
    {
        if (o.PlacedFrame == _frame)
            return;
        o.PlacedFrame = _frame;
        foreach (var d in o.StaticBehind)
            if (d.VisibleFrame == _frame)
                Place(d);
        if (o.MovingFrame == _frame)
            foreach (var d in o.MovingBehind)
                if (d.VisibleFrame == _frame)
                    Place(d);
        _order.Add(o);
    }

    // ---------- Wszystkie pary: wolne (O(n²)), do porównań i małych scen ----------

    /// <summary>Zwraca wszystkie obiekty w kolejności rysowania (od najdalszego), sprawdzając każdą parę.</summary>
    public static List<IsoObject> SortAllPairs(List<IsoObject> objects, float startDepth = 1f, float stepDepth = 0.1f)
    {
        foreach (var o in objects)
        {
            o.Behind.Clear();
            o.Placed = false;
        }

        for (int i = 0; i < objects.Count; i++)
            for (int j = i + 1; j < objects.Count; j++)
            {
                var a = objects[i];
                var b = objects[j];
                if (IsBehind(a.Position, a.Size, b.Position, b.Size))
                    a.Behind.Add(b);
                if (IsBehind(b.Position, b.Size, a.Position, a.Size))
                    b.Behind.Add(a);
            }

        float depth = startDepth;
        foreach (var o in objects)
            depth = PlaceAllPairs(o, stepDepth, depth);

        var result = new List<IsoObject>(objects);
        result.Sort((x, y) => y.Depth.CompareTo(x.Depth)); // od najdalszego
        return result;
    }

    static float PlaceAllPairs(IsoObject o, float stepDepth, float depth)
    {
        if (o.Placed)
            return depth;
        o.Placed = true;
        foreach (var d in o.Behind)
            depth = PlaceAllPairs(d, stepDepth, depth);
        o.Depth = depth;
        return depth + stepDepth;
    }

    /// <summary>Czy bryła A leży za bryłą B (A trzeba narysować wcześniej). 1:1 z IsoScreenSolver.IsIsoObjectDepends.</summary>
    public static bool IsBehind(Vector3 aMin, Vector3 aSize, Vector3 bMin, Vector3 bSize)
    {
        var aMaxX = aMin.X + aSize.X;
        var aMaxY = aMin.Y + aSize.Y;
        var aMaxZ = aMin.Z + aSize.Z;

        var bMaxX = bMin.X + bSize.X;
        var bMaxY = bMin.Y + bSize.Y;
        var bMaxZ = bMin.Z + bSize.Z;

        var aBehind = aMaxX > bMin.X && aMaxY > bMin.Y && bMaxZ > aMin.Z;
        if (!aBehind)
            return false;

        var bBehind = bMaxX > aMin.X && bMaxY > aMin.Y && aMaxZ > bMin.Z;
        if (!bBehind)
            return true;

        // Bryły nachodzą na siebie: rozstrzyga oś o najmniejszym zachodzeniu.
        var dAx = aMaxX - bMin.X;
        var dAy = aMaxY - bMin.Y;
        var dAz = bMaxZ - aMin.Z;

        var dBx = bMaxX - aMin.X;
        var dBy = bMaxY - aMin.Y;
        var dBz = aMaxZ - bMin.Z;

        var penX = aSize.X + bSize.X - MathF.Abs(dBx - dAx);
        var penY = aSize.Y + bSize.Y - MathF.Abs(dBy - dAy);
        var penZ = aSize.Z + bSize.Z - MathF.Abs(dBz - dAz);

        if (penX <= penY && penX <= penZ)
            return dAx > dBx;
        if (penY <= penX && penY <= penZ)
            return dAy > dBy;
        return dAz > dBz;
    }
}
