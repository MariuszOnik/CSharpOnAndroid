// EDYTOR MAPY IZOMETRYCZNEJ – sterowanie tylko dotykiem.
//   1 palec na mapie     – maluje wybraną klatką (albo gumką)
//   2 palce              – przesuwanie i zoom (szczypanie)
//   pasek na dole        – paleta: przeciągnij, żeby przewinąć, dotknij, żeby wybrać
//   Teren / Obiekty      – warstwa, na której malujesz
//   Zapisz / Wczytaj     – plik mapa.txt w folderze tego projektu
// Atlasy: images/grass_and_water.png (teren, klatki 64×64) i images/farma.png (obiekty, klatki 36×48).
// Poradnik krok po kroku: przycisk „?” w edytorze → „Tutorial 2”.
using System.IO;

class IsoEditor : GameScript
{
    const int RozmiarMapy = 16;
    const int SzerokoscPola = 150, WysokoscPaska = 190, WysokoscZakladek = 80;
    const int LimitCofania = 50;

    // Romb kafelka w grass_and_water.png ma 64×32 px, a IsoWorld(32) daje romb 2×32 = 64 px szerokości.
    readonly IsoWorld swiat = new(32, 32);
    Sprite[] teren, obiekty;
    Mapa mapa;

    // Kamera: ekran = świat * zoom + przesuniecie
    float zoom = 1.5f;
    Vector2 przesuniecie;

    // Edycja
    bool warstwaObiektow;
    int wybranyTeren, wybranyObiekt;  // -1 = gumka
    bool siatka = true;
    readonly List<(int[,] teren, int[,] obiekty)> historia = new();
    Point? kursor;

    // Dotyk
    enum Gest { Brak, Malowanie, Paleta, Kamera, Przycisk }
    Gest gest;
    bool cosZmieniono;
    float czasGestu;
    Vector2 startDotyku, ostatniDotyk;
    float przewiniecie, przewiniecieStart;
    Vector2 poprzedniSrodek;
    float poprzedniaOdleglosc;
    readonly List<TouchLocation> palce = new();

    // Interfejs
    readonly List<(Rectangle obszar, string napis, Action akcja, bool wlaczony)> przyciski = new();
    string komunikat = "";
    float czasKomunikatu;

    // ================= Start =================

    public override void Start()
    {
        Background = new Color(25, 35, 45);

        // Pivot = środek górnej ściany kafelka. W grass_and_water to piksel (32, 28) klatki 64×64,
        // w farma.png wszystkie klatki są ułożone tak, że to piksel (18, 35) klatki 36×48.
        teren = LoadSprites("grass_and_water.png", 64, 64, new Vector2(0.5f, 28f / 64));
        obiekty = LoadSprites("farma.png", 36, 48, new Vector2(0.5f, 35f / 48))[..42]; // dalej w atlasie są puste pola

        mapa = new Mapa(RozmiarMapy);
        Wczytaj(cicho: true);
        Wysrodkuj();
    }

    void Wysrodkuj()
    {
        var srodekMapy = swiat.ToScreen(new Vector3(mapa.Rozmiar / 2f, mapa.Rozmiar / 2f, 0));
        przesuniecie = new Vector2(Width / 2f, (Height - WysokoscPaska) / 2f) - srodekMapy * zoom;
    }

    // ================= Pomocnicze =================

    Sprite[] Paleta => warstwaObiektow ? obiekty : teren;
    int[,] Warstwa => warstwaObiektow ? mapa.Obiekty : mapa.Teren;

    int Wybrany
    {
        get => warstwaObiektow ? wybranyObiekt : wybranyTeren;
        set { if (warstwaObiektow) wybranyObiekt = value; else wybranyTeren = value; }
    }

    Rectangle PasekPalety => new(0, Height - WysokoscPaska, Width, WysokoscPaska);

    // Pole palety nr i; i = -1 to gumka (pierwsze pole), potem klatki atlasu.
    Rectangle PolePalety(int i) =>
        new((int)((i + 1) * SzerokoscPola - przewiniecie), Height - WysokoscPaska, SzerokoscPola, WysokoscPaska);

    float MaksPrzewiniecie => Math.Max(0, (Paleta.Length + 1) * SzerokoscPola - Width);

    // Ekran → świat (odwrotność kamery) → kratka izo.
    Point PolePodPalcem(Vector2 ekran)
    {
        var naSwiecie = (ekran - przesuniecie) / zoom;
        var iso = swiat.ToIso(naSwiecie);
        return new Point((int)MathF.Floor(iso.X), (int)MathF.Floor(iso.Y));
    }

    void Pokaz(string tekst)
    {
        komunikat = tekst;
        czasKomunikatu = 2;
    }

    // ================= Update =================

    public override void Update(float dt)
    {
        UlozPrzyciski();
        czasKomunikatu -= dt;
        czasGestu += dt;

        palce.Clear();
        foreach (var t in Touches)
            if (t.State != TouchLocationState.Released)
                palce.Add(t);

        if (palce.Count == 0)
        {
            ZakonczGest();
            return;
        }

        if (palce.Count >= 2)
        {
            // Drugi palec tuż po pierwszym: to był początek przesuwania, a nie malowanie – cofamy.
            if (gest == Gest.Malowanie && czasGestu < 0.3f)
                CofnijPociagniecie();
            if (gest != Gest.Kamera)
            {
                gest = Gest.Kamera;
                poprzedniaOdleglosc = 0;
                kursor = null;
            }
            Kamera(palce[0].Position, palce[1].Position);
            return;
        }

        var p = palce[0].Position;
        ostatniDotyk = p;
        switch (gest)
        {
            case Gest.Brak:
                ZacznijGest(p);
                break;
            case Gest.Malowanie:
                Maluj(p);
                break;
            case Gest.Paleta:
                przewiniecie = Math.Clamp(przewiniecieStart - (p.X - startDotyku.X), 0, MaksPrzewiniecie);
                break;
            // Kamera: po zdjęciu jednego z dwóch palców czekamy, aż zniknie i drugi.
        }
    }

    void ZacznijGest(Vector2 p)
    {
        czasGestu = 0;
        startDotyku = p;

        foreach (var przycisk in przyciski)
            if (przycisk.obszar.Contains(p))
            {
                przycisk.akcja();
                gest = Gest.Przycisk;
                return;
            }

        if (PasekPalety.Contains(p))
        {
            gest = Gest.Paleta;
            przewiniecieStart = przewiniecie;
            return;
        }

        gest = Gest.Malowanie;
        ZapamietajDoCofania();
        cosZmieniono = false;
        Maluj(p);
    }

    void ZakonczGest()
    {
        if (gest == Gest.Malowanie && !cosZmieniono)
            historia.RemoveAt(historia.Count - 1); // nic nie namalowano – nie ma czego cofać

        // Krótkie dotknięcie palety (bez przewijania) wybiera klatkę.
        if (gest == Gest.Paleta && MathF.Abs(ostatniDotyk.X - startDotyku.X) < 25)
        {
            int i = (int)((ostatniDotyk.X + przewiniecie) / SzerokoscPola) - 1;
            if (i < Paleta.Length)
                Wybrany = i;
        }

        gest = Gest.Brak;
        kursor = null;
    }

    void Maluj(Vector2 p)
    {
        var pole = PolePodPalcem(p);
        kursor = pole;
        if (!mapa.WSrodku(pole.X, pole.Y) || Warstwa[pole.X, pole.Y] == Wybrany)
            return;
        Warstwa[pole.X, pole.Y] = Wybrany;
        cosZmieniono = true;
    }

    // Punkt świata, który był pod środkiem palców, zostaje pod nowym środkiem – to daje naraz przesuwanie i zoom.
    void Kamera(Vector2 a, Vector2 b)
    {
        var srodek = (a + b) / 2;
        float odleglosc = Vector2.Distance(a, b);
        if (poprzedniaOdleglosc > 10 && odleglosc > 10)
        {
            var punktSwiata = (poprzedniSrodek - przesuniecie) / zoom;
            zoom = Math.Clamp(zoom * odleglosc / poprzedniaOdleglosc, 0.4f, 5f);
            przesuniecie = srodek - punktSwiata * zoom;
        }
        poprzedniSrodek = srodek;
        poprzedniaOdleglosc = odleglosc;
    }

    // ================= Cofanie, zapis =================

    void ZapamietajDoCofania()
    {
        historia.Add(mapa.Kopia());
        if (historia.Count > LimitCofania)
            historia.RemoveAt(0);
    }

    void CofnijPociagniecie()
    {
        if (historia.Count == 0)
            return;
        mapa.Przywroc(historia[^1]);
        historia.RemoveAt(historia.Count - 1);
    }

    void Cofnij()
    {
        if (historia.Count == 0)
        {
            Pokaz("Nie ma czego cofać");
            return;
        }
        CofnijPociagniecie();
        Pokaz("Cofnięto");
    }

    void Nowa()
    {
        ZapamietajDoCofania();
        mapa.Wyczysc();
        Pokaz("Nowa mapa (Cofnij przywraca starą)");
    }

    void Zapisz()
    {
        try
        {
            File.WriteAllText(ProjectFile("mapa.txt"), mapa.DoTekstu());
            Pokaz("Zapisano mapa.txt");
        }
        catch (Exception e)
        {
            Pokaz("Błąd zapisu: " + e.Message);
        }
    }

    void Wczytaj(bool cicho)
    {
        var plik = ProjectFile("mapa.txt");
        if (!File.Exists(plik))
        {
            if (!cicho) Pokaz("Nie ma jeszcze mapa.txt");
            return;
        }
        try
        {
            mapa = Mapa.ZTekstu(File.ReadAllText(plik));
            historia.Clear(); // stare kopie mogą mieć inny rozmiar mapy
            if (!cicho) Pokaz("Wczytano mapa.txt");
        }
        catch (Exception)
        {
            Pokaz("Plik mapa.txt jest uszkodzony");
        }
    }

    // ================= Przyciski =================

    // Układ liczony co klatkę, bo telefon można obrócić. Prawy górny róg zajmuje ✕ aplikacji.
    void UlozPrzyciski()
    {
        przyciski.Clear();
        int w = (Width - 190) / 3, h = 90, y1 = 40, y2 = 140;
        przyciski.Add((new Rectangle(20, y1, w, h), "Cofnij", Cofnij, false));
        przyciski.Add((new Rectangle(30 + w, y1, w, h), "Siatka", () => siatka = !siatka, siatka));
        przyciski.Add((new Rectangle(40 + 2 * w, y1, w, h), "Nowa", Nowa, false));
        przyciski.Add((new Rectangle(20, y2, w, h), "Zapisz", Zapisz, false));
        przyciski.Add((new Rectangle(30 + w, y2, w, h), "Wczytaj", () => Wczytaj(cicho: false), false));
        przyciski.Add((new Rectangle(40 + 2 * w, y2, w, h), "Środek", Wysrodkuj, false));

        int y = Height - WysokoscPaska - WysokoscZakladek, polowa = Width / 2;
        przyciski.Add((new Rectangle(0, y, polowa, WysokoscZakladek), "Teren",
            () => { warstwaObiektow = false; przewiniecie = 0; }, !warstwaObiektow));
        przyciski.Add((new Rectangle(polowa, y, Width - polowa, WysokoscZakladek), "Obiekty",
            () => { warstwaObiektow = true; przewiniecie = 0; }, warstwaObiektow));
    }

    // ================= Draw =================

    public override void Draw()
    {
        // Świat: kamera + ostre piksele.
        var kamera = Matrix.CreateScale(zoom) * Matrix.CreateTranslation(przesuniecie.X, przesuniecie.Y, 0);
        Batch.End();
        Batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: kamera);
        RysujMape();
        if (siatka)
            RysujSiatke();
        if (kursor is { } k && mapa.WSrodku(k.X, k.Y))
            RysujRomb(k.X, k.Y, Color.Yellow, 2 / zoom);
        Batch.End();

        // Interfejs: współrzędne ekranu.
        Batch.Begin(samplerState: SamplerState.PointClamp);
        RysujInterfejs();
        Batch.End();

        Batch.Begin(); // aplikacja dorysuje jeszcze ✕ i sama zamknie Batch
    }

    // Od najdalszej przekątnej (duże x + y, góra ekranu) do najbliższej – bliższe zasłaniają dalsze.
    void RysujMape()
    {
        int n = mapa.Rozmiar;
        for (int suma = 2 * n - 2; suma >= 0; suma--)
            for (int x = Math.Max(0, suma - n + 1); x <= Math.Min(n - 1, suma); x++)
            {
                int y = suma - x;
                var srodekPola = swiat.ToScreen(new Vector3(x + 0.5f, y + 0.5f, 0));

                int t = mapa.Teren[x, y], o = mapa.Obiekty[x, y];
                if (t >= 0 && t < teren.Length)
                    DrawSprite(teren[t], srodekPola);
                if (o >= 0 && o < obiekty.Length)
                    DrawSprite(obiekty[o], srodekPola, 0, 2f); // farma ma kafelki 32 px – skala 2 daje 64 px
            }
    }

    void RysujSiatke()
    {
        int n = mapa.Rozmiar;
        var kolor = Color.White * 0.2f;
        for (int i = 0; i <= n; i++)
        {
            Linia(swiat.ToScreen(new Vector3(i, 0, 0)), swiat.ToScreen(new Vector3(i, n, 0)), 1 / zoom, kolor);
            Linia(swiat.ToScreen(new Vector3(0, i, 0)), swiat.ToScreen(new Vector3(n, i, 0)), 1 / zoom, kolor);
        }
    }

    void RysujRomb(int x, int y, Color kolor, float grubosc)
    {
        var a = swiat.ToScreen(new Vector3(x, y, 0));
        var b = swiat.ToScreen(new Vector3(x + 1, y, 0));
        var c = swiat.ToScreen(new Vector3(x + 1, y + 1, 0));
        var d = swiat.ToScreen(new Vector3(x, y + 1, 0));
        Linia(a, b, grubosc, kolor);
        Linia(b, c, grubosc, kolor);
        Linia(c, d, grubosc, kolor);
        Linia(d, a, grubosc, kolor);
    }

    void Linia(Vector2 a, Vector2 b, float grubosc, Color kolor)
    {
        var d = b - a;
        Batch.Draw(Pixel, a, null, kolor, MathF.Atan2(d.Y, d.X), new Vector2(0, 0.5f),
            new Vector2(d.Length(), grubosc), SpriteEffects.None, 0);
    }

    void RysujInterfejs()
    {
        foreach (var (obszar, napis, _, wlaczony) in przyciski)
        {
            FillRect(obszar, wlaczony ? new Color(58, 155, 76) : new Color(44, 49, 58));
            DrawTextCentered(napis, obszar.Center.X, obszar.Center.Y, 34, Color.White);
        }

        FillRect(PasekPalety, new Color(20, 22, 28));
        for (int i = -1; i < Paleta.Length; i++)
        {
            var pole = PolePalety(i);
            if (pole.Right < 0 || pole.Left > Width)
                continue; // poza ekranem
            if (i == Wybrany)
                FillRect(new Rectangle(pole.X + 4, pole.Y + 4, pole.Width - 8, pole.Height - 8), new Color(80, 92, 115));

            if (i == -1)
                DrawTextCentered("Gumka", pole.Center.X, pole.Center.Y, 32, Color.LightPink);
            else if (warstwaObiektow)
                DrawSprite(obiekty[i], new Vector2(pole.Center.X, pole.Y + 125), 0, 2.6f);
            else
                DrawSprite(teren[i], new Vector2(pole.Center.X, pole.Y + 95), 0, 2f);
        }

        string info = kursor is { } k ? $"Pole {k.X}, {k.Y}" : $"Zoom {zoom:0.0}  FPS {Fps:0}";
        DrawText(info, 20, 245, 30, Color.LightGray);
        if (czasKomunikatu > 0)
            DrawText(komunikat, 20, 285, 34, Color.Yellow);
    }
}
