// STRES TEST sortowania izometrycznego na klockach Kenneya (images/ tego projektu).
// Przyciski u góry:
//   [Tryb]  PROSTY = wszystkie pary co klatkę (O(n²)), SZYBKI = IsoSorter.Sort
//   [+100] / [-100]  ludzików chodzących po ulicach
//   [Mapa]  8 → 16 → 32 → 48 kratek
//   [Zoom]  1 → 0.5 → 0.25
//   [Sprawdź]  porównuje kolejność z testem wszystkich par – liczy błędy sortowania
// Przeciągnij palcem po mapie, żeby ją przesunąć.
using System.Diagnostics;

class IsoStress : GameScript
{
    const float AnchorX = 66, AnchorY = 93, FloorHeight = 0.5f;
    const float ButtonsTop = 230, ButtonHeight = 110;
    const int SimpleSortLimit = 4000; // O(n²) przy większej liczbie zawiesiłby telefon na długie sekundy
    static readonly int[] MapSizes = { 8, 16, 32, 48 };
    static readonly float[] Zooms = { 1f, 0.5f, 0.25f };
    static readonly string[] Labels = { "Tryb", "+100", "-100", "Mapa", "Zoom", "Sprawdź" };

    readonly IsoWorld world = new(66, 66); // skala 1:1, początek w (0,0) – kamera robi resztę
    IsoSorter sorter;
    readonly Random random = new(7);
    string error;

    // sprite'y
    readonly List<Texture2D> groundFloors = new(), floors = new(), roofs = new();
    Texture2D asphalt, walkerSprite;
    readonly Dictionary<Texture2D, Vector2> offsets = new();
    readonly Dictionary<IsoObject, List<(Texture2D texture, Vector2 offset)>> buildings = new();

    // świat
    readonly List<IsoObject> objects = new();
    readonly List<Walker> walkers = new();
    int mapIndex, zoomIndex, mapSize;
    bool fastMode = true;
    Vector2 camera;

    // statystyki
    float statsTime, sortTime, drawTime;
    int statsFrames;
    readonly string[] statsLines = new string[5];
    string verifyResult = "—";
    long lastPairs, lastDependencies;
    int lastVisible;
    List<IsoObject> lastOrder = new();

    // dotyk
    bool panning;
    Vector2 lastTouch;

    public static bool IsStreet(int x, int y) => x % 3 == 0 || y % 3 == 0;

    // ---------------- start ----------------

    public override void Start()
    {
        Background = new Color(60, 90, 140);
        sorter = new IsoSorter(world);

        foreach (var name in ListImages("buildingTiles_*.png"))
        {
            var texture = LoadImage(name);
            if (texture.Width >= 130) groundFloors.Add(texture);
            else if (texture.Width >= 95 && texture.Height >= 80) floors.Add(texture);
            else if (texture.Width >= 95 && texture.Height >= 50) roofs.Add(texture);
        }
        if (groundFloors.Count == 0 || floors.Count == 0)
        {
            error = "Brak sprite'ów buildingTiles_*.png w images/";
            return;
        }

        asphalt = CreateBox(new Vector3(1, 1, 0.5f));
        walkerSprite = CreateBox(new Vector3(0.3f, 0.3f, 0.5f));
        BuildMap();
        AddWalkers(100);
    }

    Texture2D CreateBox(Vector3 size)
    {
        var texture = IsoSprite.CreateBox(GraphicsDevice, world, size, out var offset);
        offsets[texture] = offset;
        return texture;
    }

    void BuildMap()
    {
        mapSize = MapSizes[mapIndex];
        int walkerCount = walkers.Count;
        objects.Clear();
        buildings.Clear();
        walkers.Clear();

        for (int x = 0; x < mapSize; x++)
            for (int y = 0; y < mapSize; y++)
            {
                if (IsStreet(x, y))
                {
                    objects.Add(new IsoObject
                    {
                        Position = new Vector3(x, y, -0.5f),
                        Size = new Vector3(1, 1, 0.5f),
                        Color = (x + y) % 2 == 0 ? new Color(88, 90, 98) : new Color(80, 82, 90),
                        Sprite = asphalt,
                    });
                    continue;
                }

                var groundFloor = groundFloors[random.Next(groundFloors.Count)];
                int floorCount = random.Next(4);
                var layers = new List<(Texture2D, Vector2)> { (groundFloor, Vector2.Zero) };
                float side = (groundFloor.Width - 99) / 2, top = 0;
                for (int i = 0; i < floorCount; i++)
                {
                    top -= 33;
                    layers.Add((floors[random.Next(floors.Count)], new Vector2(side, top)));
                }
                if (roofs.Count > 0)
                    layers.Add((roofs[random.Next(roofs.Count)], new Vector2(side, top - 2)));

                var building = new IsoObject
                {
                    Position = new Vector3(x, y, -0.5f),
                    Size = new Vector3(1, 1, 0.5f + FloorHeight * (floorCount + 1) + 0.4f),
                };
                buildings[building] = layers;
                objects.Add(building);
            }

        sorter.InvalidateStatic();
        AddWalkers(walkerCount);

        // Kamera: środek mapy na środku ekranu.
        var center = world.ToScreen(new Vector3(mapSize / 2f, mapSize / 2f, 0));
        camera = new Vector2(Width / 2f, Height * 0.58f) - center * Zooms[zoomIndex];
    }

    void AddWalkers(int count)
    {
        for (int i = 0; i < count; i++)
        {
            int x, y;
            do
            {
                x = random.Next(mapSize);
                y = random.Next(mapSize);
            } while (!IsStreet(x, y));

            var walker = new Walker
            {
                FromX = x, FromY = y, ToX = x, ToY = y,
                Speed = 1.5f + (float)random.NextDouble() * 1.5f,
                Object = new IsoObject
                {
                    IsMoving = true,
                    Size = new Vector3(0.3f, 0.3f, 0.5f),
                    Sprite = walkerSprite,
                    Color = new Color(150 + random.Next(105), 40 + random.Next(80), 40 + random.Next(80)),
                },
            };
            walkers.Add(walker);
            objects.Add(walker.Object);
        }
    }

    void RemoveWalkers(int count)
    {
        for (int i = 0; i < count && walkers.Count > 0; i++)
        {
            objects.Remove(walkers[^1].Object);
            walkers.RemoveAt(walkers.Count - 1);
        }
    }

    // ---------------- logika ----------------

    public override void Update(float dt)
    {
        if (error != null) return;
        HandleTouch();

        foreach (var walker in walkers)
            walker.Update(dt, mapSize, random);

        statsTime += dt;
        if (statsTime >= 0.5f)
            UpdateStats();
    }

    void UpdateStats()
    {
        float sort = statsFrames > 0 ? sortTime / statsFrames : 0, draw = statsFrames > 0 ? drawTime / statsFrames : 0;
        statsLines[0] = fastMode ? "Tryb: SZYBKI (siatka + pamięć + widoczne)" : "Tryb: PROSTY (wszystkie pary)";
        statsLines[1] = $"Obiekty: {objects.Count}  ludzie: {walkers.Count}  widoczne: {lastVisible}  mapa {mapSize}x{mapSize}";
        statsLines[2] = $"Pary: {lastPairs:N0}  zależności: {lastDependencies:N0}" + (fastMode ? $"  (stat. {sorter.StaticPairCount:N0} raz)" : "");
        statsLines[3] = $"Sort: {sort:0.0} ms  rysowanie: {draw:0.0} ms  FPS: {Fps:0}";
        statsLines[4] = $"Sprawdzenie: {verifyResult}";
        statsTime = 0;
        sortTime = 0;
        drawTime = 0;
        statsFrames = 0;
    }

    void HandleTouch()
    {
        if (!IsTouching)
        {
            panning = false;
            return;
        }

        var touch = TouchPosition;
        if (JustTouched)
        {
            int button = ButtonAt(touch);
            if (button >= 0)
            {
                Press(button);
            }
            else if (touch.Y > ButtonsTop + 2 * ButtonHeight + 260)
            {
                panning = true;
                lastTouch = touch;
            }
            return;
        }

        if (panning)
        {
            camera += touch - lastTouch;
            lastTouch = touch;
        }
    }

    int ButtonAt(Vector2 point)
    {
        if (point.Y < ButtonsTop || point.Y > ButtonsTop + 2 * ButtonHeight) return -1;
        int column = (int)(point.X / (Width / 3f)), row = (int)((point.Y - ButtonsTop) / ButtonHeight);
        return Math.Clamp(row * 3 + column, 0, 5);
    }

    void Press(int button)
    {
        switch (button)
        {
            case 0: fastMode = !fastMode; break;
            case 1: AddWalkers(100); break;
            case 2: RemoveWalkers(100); break;
            case 3: mapIndex = (mapIndex + 1) % MapSizes.Length; BuildMap(); break;
            case 4: NextZoom(); break;
            case 5: verifyResult = Verify(); break;
        }
    }

    void NextZoom()
    {
        var screenCenter = new Vector2(Width / 2f, Height * 0.58f);
        var worldPoint = (screenCenter - camera) / Zooms[zoomIndex];
        zoomIndex = (zoomIndex + 1) % Zooms.Length;
        camera = screenCenter - worldPoint * Zooms[zoomIndex];
    }

    // Dla każdej pary nakładających się na ekranie obiektów: jeśli A jest za B, A musi być narysowany wcześniej.
    string Verify()
    {
        var stopwatch = Stopwatch.StartNew();
        foreach (var o in lastOrder) sorter.UpdateBounds(o);

        long pairs = 0, errors = 0;
        for (int i = 0; i < lastOrder.Count; i++)
            for (int j = i + 1; j < lastOrder.Count; j++)
            {
                var a = lastOrder[i];
                var b = lastOrder[j];
                if (!IsoSorter.Overlap(a, b)) continue;
                pairs++;
                // a narysowany przed b – błąd, jeśli to b jest za a
                if (IsoSorter.IsBehind(b.Position, b.Size, a.Position, a.Size) &&
                    !IsoSorter.IsBehind(a.Position, a.Size, b.Position, b.Size))
                    errors++;
            }
        return $"{(fastMode ? "SZYBKI" : "PROSTY")}: {errors} błędów na {pairs:N0} par ({stopwatch.ElapsedMilliseconds} ms)";
    }

    // ---------------- rysowanie ----------------

    public override void Draw()
    {
        if (error != null)
        {
            DrawText(error, 30, 250, 30, Color.White);
            return;
        }

        float zoom = Zooms[zoomIndex];
        var stopwatch = Stopwatch.StartNew();
        var order = Sort(zoom);
        sortTime += (float)stopwatch.Elapsed.TotalMilliseconds;
        lastOrder = order;

        stopwatch.Restart();
        foreach (var o in order)
        {
            if (buildings.TryGetValue(o, out var layers))
            {
                var corner = world.ToScreen(new Vector3(o.Position.X, o.Position.Y, 0)) * zoom + camera;
                var topLeft = corner - new Vector2(AnchorX, AnchorY) * zoom;
                foreach (var (texture, offset) in layers)
                    Batch.Draw(texture, topLeft + offset * zoom, null, Color.White, 0, Vector2.Zero, zoom, SpriteEffects.None, 0);
            }
            else
            {
                var position = world.ToScreen(o.Position) * zoom + camera + offsets[o.Sprite] * zoom;
                Batch.Draw(o.Sprite, position, null, o.Color, 0, Vector2.Zero, zoom, SpriteEffects.None, 0);
            }
        }
        drawTime += (float)stopwatch.Elapsed.TotalMilliseconds;
        statsFrames++;

        DrawPanel();
    }

    List<IsoObject> Sort(float zoom)
    {
        if (!fastMode && objects.Count > SimpleSortLimit)
        {
            fastMode = true;
            verifyResult = $"PROSTY wyłączony: ponad {SimpleSortLimit} obiektów";
        }

        if (fastMode)
        {
            var order = sorter.Sort(objects, -camera.X / zoom, -camera.Y / zoom, (Width - camera.X) / zoom, (Height - camera.Y) / zoom);
            lastPairs = sorter.PairCount + sorter.StaticPairCount;
            lastDependencies = sorter.DependencyCount;
            lastVisible = sorter.VisibleCount;
            return order;
        }

        var all = IsoSorter.SortAllPairs(objects);
        lastPairs = (long)objects.Count * (objects.Count - 1) / 2;
        lastDependencies = objects.Sum(o => (long)o.DependencyCount);
        lastVisible = objects.Count;
        return all;
    }

    void DrawPanel()
    {
        FillRect(0, 0, Width, ButtonsTop + 2 * ButtonHeight + 250, new Color(20, 24, 40, 220));
        float width = Width / 3f;
        for (int i = 0; i < Labels.Length; i++)
        {
            float x = (i % 3) * width, y = ButtonsTop + (i / 3) * ButtonHeight;
            var color = i == 0 ? (fastMode ? new Color(46, 125, 50) : new Color(170, 60, 50)) : new Color(55, 71, 79);
            FillRect(x + 6, y + 6, width - 12, ButtonHeight - 12, color);
            DrawTextCentered(Labels[i], x + width / 2, y + ButtonHeight / 2, 40, Color.White);
        }

        float lineY = ButtonsTop + 2 * ButtonHeight + 20;
        for (int i = 0; i < statsLines.Length; i++)
            DrawText(statsLines[i], 24, lineY + i * 46, 30, i == 3 ? Color.Yellow : Color.White);
    }
}
