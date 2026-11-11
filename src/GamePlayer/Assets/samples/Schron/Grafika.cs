// Grafika generowana w kodzie, bez plików:
//   bryły  – podłogi, ściany, urządzenia (IsoSprite.CreateBox),
//   glify  – ludzie, wrogowie, drzewa, skały, łupy: małe bitmapy zapisane jako napisy (jak w HeroesDS glyphtest).
// Scena = lista obiektów + IsoSorter (kolejność rysowania) + kamera (przesunięcie i zoom).

// Bitmapy: każdy znak to jeden „piksel”. Kropka = przezroczysty, x = kolor własny (np. koszula), X = jaśniejszy.
static class Glify
{
    public const int Piksel = 8; // tyle pikseli tekstury ma jeden piksel glifu (ostre krawędzie po powiększeniu)

    // Paleta 16 kolorów (ta sama co w LiteLua / glyphtest).
    static readonly Color[] Paleta =
    {
        new(0, 0, 0), new(26, 28, 44), new(177, 62, 83), new(239, 125, 87), new(255, 205, 117), new(167, 240, 112),
        new(56, 183, 100), new(75, 105, 47), new(41, 54, 111), new(59, 93, 201), new(65, 166, 246), new(115, 239, 247),
        new(244, 244, 244), new(148, 176, 194), new(86, 108, 134), new(51, 60, 87),
    };

    // Litera → numer koloru w palecie.
    const string Litery = "odrfylzZpbBcwgGn";

    public static readonly Dictionary<string, string[]> Wzory = new()
    {
        ["czlowiek"] = new[]
        {
            "..oooo..",
            ".onnnno.",
            ".offffo.",
            ".onffno.",
            ".oxxxxo.",
            "oxXxxxxo",
            "ofxxxxfo",
            ".oxxxxo.",
            ".oGGGGo.",
            ".oGooGo.",
            ".oo..oo.",
        },
        ["bandyta"] = new[]
        {
            "..oooo..",
            ".oGGGGo.",
            ".offffo.",
            ".orrrro.",
            ".oxxxxo.",
            "oxXxxxxo",
            "ofxxxogg",
            ".oxxxxo.",
            ".oGGGGo.",
            ".oGooGo.",
            ".oo..oo.",
        },
        ["pies"] = new[]
        {
            "o......oo.",
            "xo....oxxo",
            ".oxxxxxwxo",
            ".oxXxxxxo.",
            ".oxo..oxo.",
            ".oo...oo..",
        },
        ["mutant"] = new[]
        {
            "..oooooo..",
            ".oxxxxxxo.",
            ".oxyxxyxo.",
            ".oxxrrxxo.",
            "ooxxxxxxoo",
            "oxXxxxxxxo",
            "oxXxxxxxxo",
            "oxoxxxxoxo",
            "oxoxxxxoxo",
            ".ooxxxxoo.",
            "..oxooxo..",
            ".ooo..ooo.",
        },
        ["drzewo"] = new[]
        {
            "....o....",
            "...olo...",
            "...olzo..",
            "..ollzo..",
            "..ollzzo.",
            ".olllzzo.",
            ".olllzzzo",
            "olllzzzzo",
            "ooooGoooo",
            "...oGo...",
            "...oGo...",
        },
        ["suche"] = new[]
        {
            "n...n...n",
            ".n..n..n.",
            ".n..n.n..",
            "..n.nn...",
            "..nnn....",
            "...nn..n.",
            "...nnnn..",
            "...nn....",
            "...nn....",
            "..nnnn...",
        },
        ["skala"] = new[]
        {
            "..oooo..",
            ".ogggGo.",
            "ogwggGGo",
            "ogggGGGo",
            "oooooooo",
        },
        ["skrzynia"] = new[]
        {
            ".oooooo.",
            "offffffo",
            "oooyyooo",
            "offyyffo",
            "offffffo",
            "oooooooo",
        },
        ["wrak"] = new[]
        {
            "...oooooo...",
            "..occoccGo..",
            ".oGGGGGGGGo.",
            "orrfrrrrfrro",
            "orrrrfrrrrro",
            "oooGGooGGooo",
            "..oo....oo..",
        },
    };

    // Zamienia wzór na piksele tekstury. kolor = barwa dla „x”, jaśniejsza dla „X”.
    public static Color[] Piksele(string[] wzor, Color kolor, out int szerokosc, out int wysokosc)
    {
        int w = 0;
        foreach (var wiersz in wzor) w = Math.Max(w, wiersz.Length);
        szerokosc = w * Piksel;
        wysokosc = wzor.Length * Piksel;
        var jasny = new Color(kolor.R + (255 - kolor.R) / 3, kolor.G + (255 - kolor.G) / 3, kolor.B + (255 - kolor.B) / 3);
        var piksele = new Color[szerokosc * wysokosc];
        for (int r = 0; r < wzor.Length; r++)
            for (int q = 0; q < wzor[r].Length; q++)
            {
                char znak = wzor[r][q];
                int numer = Litery.IndexOf(znak);
                Color c;
                if (znak == 'x') c = kolor;
                else if (znak == 'X') c = jasny;
                else if (numer >= 0) c = Paleta[numer];
                else continue; // kropka albo nieznany znak = przezroczysty
                for (int y = 0; y < Piksel; y++)
                    for (int x = 0; x < Piksel; x++)
                        piksele[(r * Piksel + y) * szerokosc + q * Piksel + x] = c;
            }
        return piksele;
    }
}

// Obiekt sceny rysowany glifem: bryła (Position, Size) służy tylko do sortowania,
// a obrazek stoi „stopami” na środku jej podstawy.
class Duszek : IsoObject
{
    public float Skala = 0.5f; // rozmiar piksela glifu w pikselach świata, podzielony przez Glify.Piksel
    public bool Odbity;        // lustrzane odbicie (patrzy w lewo)
    public float Podskok;      // uniesienie w pikselach świata (chód)
}

class Grafika
{
    public readonly IsoWorld Swiat = new(32, 32);
    public readonly Dictionary<Texture2D, Vector2> Przesuniecia = new();
    readonly Dictionary<Vector3, Texture2D> tekstury = new();
    readonly Dictionary<string, Texture2D> glify = new();
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

    // Tekstura glifu w danym kolorze – też tworzona raz.
    public Texture2D Glif(string nazwa, Color kolor)
    {
        string klucz = $"{nazwa}/{kolor.R}/{kolor.G}/{kolor.B}";
        if (!glify.TryGetValue(klucz, out var tekstura))
        {
            var piksele = Glify.Piksele(Glify.Wzory[nazwa], kolor, out int szerokosc, out int wysokosc);
            tekstura = new Texture2D(gra.GraphicsDevice, szerokosc, wysokosc);
            tekstura.SetData(piksele);
            glify[klucz] = tekstura;
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

    // Tworzy glif. sx, sy, sz – bryła do sortowania; piksel – rozmiar piksela glifu w pikselach świata (kratka ma 64).
    public Duszek NowyGlif(string nazwa, Color kolor, float sx, float sy, float sz, float piksel = 3.5f, bool ruchomy = false) =>
        new Duszek
        {
            Size = new Vector3(sx, sy, sz),
            Sprite = grafika.Glif(nazwa, kolor),
            IsMoving = ruchomy,
            Skala = piksel / Glify.Piksel,
        };

    public Duszek DodajGlif(string nazwa, Color kolor, float x, float y, float z, float sx, float sy, float sz, float piksel = 3.5f)
    {
        var d = NowyGlif(nazwa, kolor, sx, sy, sz, piksel);
        d.Position = new Vector3(x, y, z);
        Obiekty.Add(d);
        return d;
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
            if (o is Duszek d)
            {
                float skala = d.Skala * Zoom;
                var stopy = NaEkran(o.Position.X + o.Size.X / 2, o.Position.Y + o.Size.Y / 2, o.Position.Z);
                var rog = new Vector2(stopy.X - o.Sprite.Width * skala / 2, stopy.Y - o.Sprite.Height * skala - d.Podskok * Zoom);
                gra.Batch.Draw(o.Sprite, rog, null, o.Color, 0f, Vector2.Zero, skala,
                    d.Odbity ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
                continue;
            }
            var pozycja = NaEkran(o.Position) + grafika.Przesuniecia[o.Sprite] * Zoom;
            gra.Batch.Draw(o.Sprite, pozycja, null, o.Color, 0f, Vector2.Zero, Zoom, SpriteEffects.None, 0f);
        }
    }
}
