// Grafika generowana w kodzie: każda rzecz w grze to jedna albo kilka kolorowych brył z IsoSprite.CreateBox.
// Scena = lista brył + IsoSorter (kolejność rysowania) + kamera (przesunięcie i zoom).

class Grafika
{
    public readonly IsoWorld Swiat = new(32, 32);
    public readonly Dictionary<Texture2D, Vector2> Przesuniecia = new();
    readonly Dictionary<Vector3, Texture2D> tekstury = new();
    readonly GameScript gra;

    public Grafika(GameScript gra) { this.gra = gra; }

    // Tekstura bryły o danym rozmiarze – tworzona raz, potem brana z pamięci.
    public Texture2D Bryla(Vector3 rozmiar)
    {
        var klucz = new Vector3(MathF.Round(rozmiar.X * 100) / 100, MathF.Round(rozmiar.Y * 100) / 100, MathF.Round(rozmiar.Z * 100) / 100);
        if (!tekstury.TryGetValue(klucz, out var tekstura))
        {
            tekstura = IsoSprite.CreateBox(gra.GraphicsDevice, Swiat, klucz, out var przesuniecie);
            tekstury[klucz] = tekstura;
            Przesuniecia[tekstura] = przesuniecie;
        }
        return tekstura;
    }
}

class Scena
{
    public readonly List<IsoObject> Obiekty = new();
    public float Zoom = 1.5f;
    public Vector2 Kamera; // ekran = świat * Zoom + Kamera

    readonly GameScript gra;
    readonly Grafika grafika;
    readonly IsoSorter sorter;

    public Scena(GameScript gra, Grafika grafika)
    {
        this.gra = gra;
        this.grafika = grafika;
        sorter = new IsoSorter(grafika.Swiat);
    }

    // Tworzy bryłę, ale jeszcze jej nie dodaje do sceny.
    public IsoObject Nowy(float sx, float sy, float sz, Color kolor, bool ruchomy = false)
    {
        var rozmiar = new Vector3(sx, sy, sz);
        return new IsoObject { Size = rozmiar, Sprite = grafika.Bryla(rozmiar), Color = kolor, IsMoving = ruchomy };
    }

    public IsoObject Dodaj(float x, float y, float z, float sx, float sy, float sz, Color kolor, bool ruchomy = false)
    {
        var o = Nowy(sx, sy, sz, kolor, ruchomy);
        o.Position = new Vector3(x, y, z);
        Obiekty.Add(o);
        return o;
    }

    // Po dodaniu albo usunięciu nieruchomych brył sorter musi policzyć je od nowa.
    public void Odswiez() => sorter.InvalidateStatic();

    public void Usun(IsoObject o)
    {
        if (o != null && Obiekty.Remove(o))
            Odswiez();
    }

    public Vector2 NaEkran(Vector3 iso) => grafika.Swiat.ToScreen(iso) * Zoom + Kamera;
    public Vector2 NaEkran(float x, float y, float z) => NaEkran(new Vector3(x, y, z));
    public Vector3 NaIso(Vector2 ekran, float z = 0) => grafika.Swiat.ToIso((ekran - Kamera) / Zoom, z);

    public void Centruj(Vector3 iso, Rectangle widok) =>
        Kamera = new Vector2(widok.Center.X, widok.Center.Y) - grafika.Swiat.ToScreen(iso) * Zoom;

    public void Rysuj(Rectangle widok)
    {
        var kolejnosc = sorter.Sort(Obiekty,
            (widok.Left - Kamera.X) / Zoom, (widok.Top - Kamera.Y) / Zoom,
            (widok.Right - Kamera.X) / Zoom, (widok.Bottom - Kamera.Y) / Zoom);
        foreach (var o in kolejnosc)
        {
            var pozycja = NaEkran(o.Position) + grafika.Przesuniecia[o.Sprite] * Zoom;
            gra.Batch.Draw(o.Sprite, pozycja, null, o.Color, 0f, Vector2.Zero, Zoom, SpriteEffects.None, 0f);
        }
    }
}
