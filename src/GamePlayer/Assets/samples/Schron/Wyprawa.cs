// Wyprawa: losowy teren na powierzchni, łupy do przeszukania i wrogowie.
// Dopóki nikt cię nie widzi, chodzisz swobodnie. Gdy wróg zauważy ekipę, zaczyna się walka turowa:
// każdy ma punkty akcji (PA) – krok kosztuje 1, atak 3, przeszukanie i leczenie 2.

class Jednostka
{
    public string Nazwa;
    public bool Wrog;
    public Ocalaly Kto;          // dla ludzi z ekipy
    public int X, Y;             // kratka, na której stoi (albo na którą właśnie wchodzi)
    public Vector2 Poz;          // płynna pozycja środka
    public readonly List<Point> Sciezka = new();
    public bool WKroku;
    public int Pa, MaxPa;
    public float Hp, MaxHp;      // tylko wrogowie; ludzie mają Kto.Zdrowie
    public int ObrMin, ObrMax, Zasieg; // Zasieg > 1 = strzela
    public bool Alarm;
    public Lup CelLupu;
    public float Wzrost = 0.8f;
    public Duszek Cialo;

    public bool Zyje => Wrog ? Hp > 0 : Kto.Zyje;
    public bool WRuchu => WKroku || Sciezka.Count > 0;
}

class Lup
{
    public string Nazwa;
    public int X, Y, Rzuty;
    public bool Pusty;
    public readonly List<IsoObject> Bryly = new();
}

class Wyprawa
{
    const int N = 18;
    const int ZasiegWzroku = 6;
    const float Predkosc = 5f;

    public readonly Scena Scena;
    public readonly Zasoby Plecak = new();
    readonly Schron g;
    readonly bool[,] blok = new bool[N, N];   // skały, ruiny, drzewa, łupy, skażona woda
    readonly bool[,] zaslona = new bool[N, N]; // wysokie przeszkody – zasłaniają widok i strzał
    readonly List<Jednostka> ekipa = new(), wrogowie = new();
    readonly List<Lup> lupy = new();
    readonly List<(string tekst, Vector3 gdzie, Color kolor, float czas)> napisy = new();
    readonly List<Jednostka> lancuch = new();
    readonly int srodek = N / 2;

    Jednostka wybrana;
    bool walka, turaWroga;
    int ktoryWrog;
    float opoznienie, czasWedrowki;
    bool znalezionoPistolet;

    static readonly (int dx, int dy)[] Kierunki = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    public Wyprawa(Schron g, List<Ocalaly> ludzie, int dzien)
    {
        this.g = g;
        Scena = new Scena(g, g.Grafika);
        var los = g.Random;

        // --- Teren ---
        for (int x = 0; x < N; x++)
            for (int y = 0; y < N; y++)
            {
                bool start = StrefaStartu(x, y);
                int rzut = los.Next(100);
                Color kolor;
                if (x == 0 && Math.Abs(y - srodek) <= 1) kolor = new Color(214, 190, 80);          // wyjście
                else if (y == srodek || y == srodek - 1 && x > 9) kolor = new Color(98, 98, 102);   // droga
                else if (!start && rzut < 4) { kolor = new Color(92, 150, 70); blok[x, y] = true; } // skażona woda
                else kolor = rzut < 55 ? new Color(122, 116, 70) : new Color(134, 110, 72);
                int odcien = los.Next(-8, 9);
                Scena.Dodaj(x, y, -0.2f, 1, 1, 0.2f, new Color(kolor.R + odcien, kolor.G + odcien, kolor.B + odcien));

                if (start || blok[x, y] || y == srodek) continue;
                int p = los.Next(100);
                if (p < 5) // skała
                {
                    blok[x, y] = zaslona[x, y] = true;
                    Scena.DodajGlif("skala", Color.White, x + 0.1f, y + 0.1f, 0, 0.8f, 0.8f, 0.8f, piksel: 5 + los.Next(2));
                }
                else if (p < 10) // drzewo (nie zasłania)
                {
                    blok[x, y] = true;
                    if (los.Next(3) == 0)
                        Scena.DodajGlif("drzewo", Color.White, x + 0.3f, y + 0.3f, 0, 0.4f, 0.4f, 1.6f, piksel: 5);
                    else
                        Scena.DodajGlif("suche", Color.White, x + 0.3f, y + 0.3f, 0, 0.4f, 0.4f, 1.3f, piksel: 4.5f);
                }
                else if (p < 16) // ruina
                {
                    blok[x, y] = zaslona[x, y] = true;
                    Scena.Dodaj(x, y, 0, 1, 1, 0.9f + los.Next(3) * 0.3f, new Color(150, 140, 128));
                }
            }

        // --- Łupy i wrogowie tylko tam, dokąd da się dojść ---
        int ileLupow = 6 + los.Next(3);
        for (int proba = 0; proba < 400 && lupy.Count < ileLupow; proba++)
        {
            int x = los.Next(3, N), y = los.Next(N);
            if (blok[x, y] || y == srodek || !Osiagalne()[x, y]) continue;
            DodajLup(x, y, los.Next(3));
        }

        var dojscie = Osiagalne();
        int ileWrogow = 3 + Math.Min(4, dzien / 2);
        for (int proba = 0; proba < 400 && wrogowie.Count < ileWrogow; proba++)
        {
            int x = los.Next(7, N), y = los.Next(N);
            if (blok[x, y] || !dojscie[x, y] || KtoStoi(x, y) != null) continue;
            DodajWroga(x, y, los.Next(100));
        }

        // --- Ekipa ---
        (int, int)[] miejsca = { (1, srodek), (1, srodek - 1), (1, srodek + 1), (2, srodek) };
        for (int i = 0; i < ludzie.Count; i++)
        {
            var o = ludzie[i];
            var j = new Jednostka { Nazwa = o.Imie, Kto = o, MaxPa = 6, Pa = 6, ObrMin = 4, ObrMax = 8, Zasieg = 1 };
            j.Cialo = Scena.NowyGlif("czlowiek", o.Kolor, 0.4f, 0.4f, 1.1f, ruchomy: true);
            Wstaw(j, miejsca[i % miejsca.Length].Item1, miejsca[i % miejsca.Length].Item2);
            ekipa.Add(j);
        }
        wybrana = ekipa[0];

        Scena.Odswiez();
        Scena.Zoom = g.Widok.Width / 520f;
        Scena.Centruj(new Vector3(4, srodek, 0), g.Widok);
        g.Log("Wyprawa! Dotknij pola, by iść. Szukaj skrzyń i wraków.");
    }

    // ================= Budowa mapy =================

    bool StrefaStartu(int x, int y) => x <= 3 && Math.Abs(y - srodek) <= 2;
    bool Wyjscie(int x, int y) => x <= 1 && Math.Abs(y - srodek) <= 2;
    static bool NaMapie(int x, int y) => x >= 0 && y >= 0 && x < N && y < N;

    void DodajLup(int x, int y, int rodzaj)
    {
        var lup = new Lup { X = x, Y = y };
        switch (rodzaj)
        {
            case 0:
                lup.Nazwa = "Skrzynia";
                lup.Rzuty = 2;
                lup.Bryly.Add(Scena.DodajGlif("skrzynia", Color.White, x + 0.2f, y + 0.2f, 0, 0.6f, 0.6f, 0.5f, piksel: 4));
                break;
            case 1:
                lup.Nazwa = "Wrak";
                lup.Rzuty = 3;
                lup.Bryly.Add(Scena.DodajGlif("wrak", Color.White, x + 0.05f, y + 0.2f, 0, 0.9f, 0.6f, 0.6f, piksel: 4.5f));
                break;
            default:
                lup.Nazwa = "Dom";
                lup.Rzuty = 4;
                zaslona[x, y] = true;
                lup.Bryly.Add(Scena.Dodaj(x + 0.05f, y + 0.05f, 0, 0.9f, 0.9f, 1.0f, new Color(196, 178, 140)));
                lup.Bryly.Add(Scena.Dodaj(x, y, 1.0f, 1, 1, 0.2f, new Color(150, 70, 60)));
                break;
        }
        blok[x, y] = true;
        lupy.Add(lup);
    }

    void DodajWroga(int x, int y, int rzut)
    {
        Jednostka w;
        if (rzut < 40)
        {
            w = new Jednostka { Nazwa = "Zdziczały pies", MaxHp = 10, MaxPa = 8, ObrMin = 6, ObrMax = 12, Zasieg = 1, Wzrost = 0.5f };
            w.Cialo = Scena.NowyGlif("pies", new Color(150, 105, 70), 0.5f, 0.4f, 0.6f, ruchomy: true);
        }
        else if (rzut < 75)
        {
            w = new Jednostka { Nazwa = "Bandyta", MaxHp = 16, MaxPa = 5, ObrMin = 9, ObrMax = 16, Zasieg = 5 };
            w.Cialo = Scena.NowyGlif("bandyta", new Color(150, 40, 40), 0.4f, 0.4f, 1.1f, ruchomy: true);
        }
        else
        {
            w = new Jednostka { Nazwa = "Mutant", MaxHp = 28, MaxPa = 4, ObrMin = 14, ObrMax = 24, Zasieg = 1, Wzrost = 1.2f };
            w.Cialo = Scena.NowyGlif("mutant", new Color(90, 160, 90), 0.5f, 0.5f, 1.4f, piksel: 4, ruchomy: true);
        }
        w.Wrog = true;
        w.Cialo.Odbity = true; // wrogowie patrzą w stronę wyjścia
        w.Hp = w.MaxHp;
        Wstaw(w, x, y);
        wrogowie.Add(w);
    }

    void Wstaw(Jednostka j, int x, int y)
    {
        j.X = x;
        j.Y = y;
        j.Poz = new Vector2(x + 0.5f, y + 0.5f);
        Scena.Obiekty.Add(j.Cialo);
        UstawBryly(j);
    }

    void UstawBryly(Jednostka j)
    {
        var c = j.Cialo.Size;
        j.Cialo.Position = new Vector3(j.Poz.X - c.X / 2, j.Poz.Y - c.Y / 2, 0);
    }

    // ================= Mapa: zajętość, droga, widoczność =================

    Jednostka KtoStoi(int x, int y)
    {
        foreach (var j in ekipa) if (j.Zyje && j.X == x && j.Y == y) return j;
        foreach (var j in wrogowie) if (j.Zyje && j.X == x && j.Y == y) return j;
        return null;
    }

    bool Wolne(int x, int y) => NaMapie(x, y) && !blok[x, y] && KtoStoi(x, y) == null;

    static int Odleglosc(int x1, int y1, int x2, int y2) => Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));
    static int Odleglosc(Jednostka a, Jednostka b) => Odleglosc(a.X, a.Y, b.X, b.Y);

    // Pola, do których da się dojść ze startu (bez patrzenia na jednostki).
    bool[,] Osiagalne()
    {
        var odwiedzone = new bool[N, N];
        var kolejka = new Queue<Point>();
        odwiedzone[1, srodek] = true;
        kolejka.Enqueue(new Point(1, srodek));
        while (kolejka.Count > 0)
        {
            var p = kolejka.Dequeue();
            foreach (var (dx, dy) in Kierunki)
            {
                int x = p.X + dx, y = p.Y + dy;
                if (!NaMapie(x, y) || blok[x, y] || odwiedzone[x, y]) continue;
                odwiedzone[x, y] = true;
                kolejka.Enqueue(new Point(x, y));
            }
        }
        return odwiedzone;
    }

    // Najkrótsza droga (BFS). obok = true: wystarczy stanąć na polu sąsiadującym z celem (cel jest zajęty).
    // Zwraca kolejne kratki bez startowej; null, gdy drogi nie ma.
    List<Point> Droga(int odX, int odY, int doX, int doY, bool obok)
    {
        if (obok ? Odleglosc(odX, odY, doX, doY) <= 1 : odX == doX && odY == doY)
            return new List<Point>();

        var skad = new Point[N, N];
        var odwiedzone = new bool[N, N];
        var kolejka = new Queue<Point>();
        odwiedzone[odX, odY] = true;
        kolejka.Enqueue(new Point(odX, odY));
        while (kolejka.Count > 0)
        {
            var p = kolejka.Dequeue();
            foreach (var (dx, dy) in Kierunki)
            {
                int x = p.X + dx, y = p.Y + dy;
                if (!NaMapie(x, y) || odwiedzone[x, y] || !Wolne(x, y)) continue;
                odwiedzone[x, y] = true;
                skad[x, y] = p;
                if (obok ? Odleglosc(x, y, doX, doY) <= 1 : x == doX && y == doY)
                {
                    var droga = new List<Point>();
                    for (var k = new Point(x, y); k.X != odX || k.Y != odY; k = skad[k.X, k.Y])
                        droga.Add(k);
                    droga.Reverse();
                    return droga;
                }
                kolejka.Enqueue(new Point(x, y));
            }
        }
        return null;
    }

    // Czy między dwoma polami nic nie zasłania (linia Bresenhama).
    bool Widac(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, blad = dx - dy;
        while (x0 != x1 || y0 != y1)
        {
            int e2 = 2 * blad;
            if (e2 > -dy) { blad -= dy; x0 += sx; }
            if (e2 < dx) { blad += dx; y0 += sy; }
            if ((x0 != x1 || y0 != y1) && zaslona[x0, y0]) return false;
        }
        return true;
    }

    // ================= Przebieg =================

    bool KtosWRuchu()
    {
        foreach (var j in ekipa) if (j.Zyje && j.WRuchu) return true;
        foreach (var j in wrogowie) if (j.Zyje && j.WRuchu) return true;
        return false;
    }

    public void Update(float dt)
    {
        foreach (var j in ekipa) Ruch(j, dt);
        foreach (var j in wrogowie) Ruch(j, dt);

        for (int i = napisy.Count - 1; i >= 0; i--)
        {
            var n = napisy[i];
            n.czas -= dt;
            n.gdzie.Z += dt * 0.8f;
            if (n.czas <= 0) napisy.RemoveAt(i);
            else napisy[i] = n;
        }

        if (walka && turaWroga)
            TuraWroga(dt);
        else if (!walka)
            Wedrowka(dt);
    }

    void Ruch(Jednostka j, float dt)
    {
        if (!j.Zyje) return;

        if (!j.WKroku && j.Sciezka.Count > 0)
        {
            var nastepne = j.Sciezka[0];
            j.Sciezka.RemoveAt(0);
            bool brakPa = walka && !j.Wrog && j.Pa <= 0;
            if (brakPa || !Wolne(nastepne.X, nastepne.Y))
                j.Sciezka.Clear();
            else
            {
                var stare = new Point(j.X, j.Y);
                j.X = nastepne.X;
                j.Y = nastepne.Y;
                j.WKroku = true;
                if (walka && !j.Wrog) j.Pa--;

                // Poza walką reszta ekipy idzie gęsiego za prowadzącym.
                int miejsce = lancuch.IndexOf(j);
                if (!walka && !j.Wrog && miejsce >= 0 && miejsce + 1 < lancuch.Count)
                {
                    var za = lancuch[miejsce + 1];
                    if (za.Zyje)
                    {
                        var droga = Droga(za.X, za.Y, stare.X, stare.Y, obok: false);
                        if (droga != null)
                        {
                            za.Sciezka.Clear();
                            za.Sciezka.AddRange(droga);
                        }
                    }
                }
            }
        }

        if (!j.WKroku) return;

        var cel = new Vector2(j.X + 0.5f, j.Y + 0.5f);
        var roznica = cel - j.Poz;
        float krok = Predkosc * dt;
        if (roznica.Length() <= krok)
        {
            j.Poz = cel;
            j.WKroku = false;
            j.Cialo.Podskok = 0;
            UstawBryly(j);
            PoKroku(j);
        }
        else
        {
            j.Poz += roznica / roznica.Length() * krok;
            j.Cialo.Odbity = roznica.X - roznica.Y < 0; // w lewo na ekranie
            j.Cialo.Podskok = MathF.Abs(MathF.Sin(g.Czas * 14)) * 2.5f;
            UstawBryly(j);
        }
    }

    void PoKroku(Jednostka j)
    {
        SprawdzAlarm();
        if (!j.Wrog && j.CelLupu != null && j.Sciezka.Count == 0)
        {
            var lup = j.CelLupu;
            j.CelLupu = null;
            if (Odleglosc(j.X, j.Y, lup.X, lup.Y) <= 1)
                Przeszukaj(j, lup);
        }
    }

    // Wróg, który widzi kogoś z ekipy, podnosi alarm i zaczyna się walka.
    void SprawdzAlarm()
    {
        bool nowy = false;
        foreach (var w in wrogowie)
        {
            if (!w.Zyje || w.Alarm) continue;
            foreach (var j in ekipa)
                if (j.Zyje && Odleglosc(w, j) <= ZasiegWzroku && Widac(w.X, w.Y, j.X, j.Y))
                {
                    w.Alarm = true;
                    nowy = true;
                    Napis("!", w, Color.Red);
                    break;
                }
        }
        if (nowy && !walka)
            ZacznijWalke();
    }

    void ZacznijWalke()
    {
        walka = true;
        turaWroga = false;
        foreach (var j in ekipa)
        {
            j.Sciezka.Clear();
            j.CelLupu = null;
            j.Pa = j.MaxPa;
        }
        g.Log("WALKA! Krok 1 PA, atak 3 PA. Dotknij wroga, by atakować.");
    }

    void SprawdzKoniecWalki()
    {
        foreach (var w in wrogowie)
            if (w.Zyje && w.Alarm) return;
        if (walka)
        {
            walka = false;
            turaWroga = false;
            g.Log("Teren czysty. Można iść dalej.");
        }
    }

    // Poza walką niezaalarmowani wrogowie kręcą się po okolicy.
    void Wedrowka(float dt)
    {
        czasWedrowki += dt;
        if (czasWedrowki < 1.2f) return;
        czasWedrowki = 0;
        foreach (var w in wrogowie)
        {
            if (!w.Zyje || w.WRuchu || g.Random.Next(100) >= 30) continue;
            var (dx, dy) = Kierunki[g.Random.Next(4)];
            if (Wolne(w.X + dx, w.Y + dy) && !StrefaStartu(w.X + dx, w.Y + dy))
                w.Sciezka.Add(new Point(w.X + dx, w.Y + dy));
        }
    }

    void KoniecTuryGracza()
    {
        foreach (var j in ekipa) j.Sciezka.Clear();
        foreach (var w in wrogowie) w.Pa = w.MaxPa;
        turaWroga = true;
        ktoryWrog = 0;
        opoznienie = 0.3f;
    }

    void TuraWroga(float dt)
    {
        if (KtosWRuchu()) return;
        opoznienie -= dt;
        if (opoznienie > 0) return;

        while (ktoryWrog < wrogowie.Count)
        {
            var w = wrogowie[ktoryWrog];
            var cel = w.Zyje && w.Alarm ? NajblizszyCel(w) : null;
            if (cel != null)
            {
                bool wZasiegu = MozeAtakowac(w, cel);
                if (wZasiegu && w.Pa >= 3)
                {
                    w.Pa -= 3;
                    AtakWroga(w, cel);
                    opoznienie = 0.45f;
                    return;
                }
                if (!wZasiegu && w.Pa >= 1)
                {
                    var droga = Droga(w.X, w.Y, cel.X, cel.Y, obok: true);
                    if (droga != null && droga.Count > 0)
                    {
                        w.Pa--;
                        w.Sciezka.Add(droga[0]);
                        return;
                    }
                }
            }
            ktoryWrog++;
        }

        // Koniec tury wrogów
        turaWroga = false;
        foreach (var j in ekipa) j.Pa = j.MaxPa;
        SprawdzAlarm();
        SprawdzKoniecWalki();
    }

    Jednostka NajblizszyCel(Jednostka w)
    {
        Jednostka cel = null;
        foreach (var j in ekipa)
            if (j.Zyje && (cel == null || Odleglosc(w, j) < Odleglosc(w, cel)))
                cel = j;
        return cel;
    }

    bool MozeAtakowac(Jednostka kto, Jednostka kogo)
    {
        int d = Odleglosc(kto, kogo);
        return d <= 1 || d <= kto.Zasieg && Widac(kto.X, kto.Y, kogo.X, kogo.Y);
    }

    // ================= Walka =================

    void Napis(string tekst, Jednostka nad, Color kolor) =>
        napisy.Add((tekst, new Vector3(nad.Poz.X, nad.Poz.Y, nad.Wzrost + 0.9f), kolor, 1.1f));

    void AtakWroga(Jednostka w, Jednostka cel)
    {
        int d = Odleglosc(w, cel);
        int szansa = d <= 1 ? 80 : 70 - 6 * d;
        if (g.Random.Next(100) >= szansa)
        {
            Napis("pudło", cel, Color.LightGray);
            return;
        }
        int obrazenia = g.Random.Next(w.ObrMin, w.ObrMax + 1);
        cel.Kto.Zdrowie -= obrazenia;
        Napis("-" + obrazenia, cel, Color.OrangeRed);
        if (!cel.Zyje)
        {
            g.Log($"{cel.Nazwa} ginie – {w.Nazwa.ToLower()}.");
            UsunBryly(cel);
            if (wybrana == cel) wybrana = NastepnyZywy();
            if (wybrana == null) g.KoniecWyprawy(false);
        }
    }

    void AtakGracza(Jednostka j, Jednostka w)
    {
        if (!walka)
        {
            w.Alarm = true;
            ZacznijWalke();
        }
        if (j.Pa < 3) { g.Log($"{j.Nazwa}: za mało PA na atak (trzeba 3)."); return; }

        int d = Odleglosc(j, w);
        bool strzal = d > 1;
        if (strzal)
        {
            if (!j.Kto.Pistolet) { g.Log($"{j.Nazwa} ma tylko pałkę – podejdź do wroga."); return; }
            if (g.Z.Amunicja <= 0) { g.Log("Nie ma amunicji!"); return; }
            if (d > 7 || !Widac(j.X, j.Y, w.X, w.Y)) { g.Log("Cel za daleko albo zasłonięty."); return; }
            g.Z.Amunicja--;
        }
        else if (j.Kto.Pistolet && g.Z.Amunicja > 0)
        {
            strzal = true; // strzał z przyłożenia
            g.Z.Amunicja--;
        }

        j.Pa -= 3;
        w.Alarm = true;
        int szansa = strzal ? Math.Clamp(98 - 7 * d, 30, 95) : 85;
        if (g.Random.Next(100) >= szansa)
        {
            Napis("pudło", w, Color.LightGray);
            return;
        }
        int obrazenia = strzal ? g.Random.Next(8, 15) : g.Random.Next(j.ObrMin, j.ObrMax + 1);
        w.Hp -= obrazenia;
        Napis("-" + obrazenia, w, Color.Yellow);
        if (!w.Zyje)
        {
            g.Log($"{w.Nazwa} pokonany.");
            UsunBryly(w);
            SprawdzKoniecWalki();
        }
    }

    void UsunBryly(Jednostka j)
    {
        Scena.Obiekty.Remove(j.Cialo);
        Scena.Odswiez();
    }

    Jednostka NastepnyZywy()
    {
        foreach (var j in ekipa) if (j.Zyje) return j;
        return null;
    }

    // ================= Łupy =================

    void Przeszukaj(Jednostka j, Lup lup)
    {
        if (lup.Pusty) { g.Log($"{lup.Nazwa}: już pusto."); return; }
        if (walka)
        {
            if (j.Pa < 2) { g.Log("Za mało PA na przeszukanie (trzeba 2)."); return; }
            j.Pa -= 2;
        }

        var los = g.Random;
        var znalezione = new Zasoby();
        string dodatek = "";
        for (int i = 0; i < lup.Rzuty; i++)
        {
            int r = los.Next(100);
            if (r < 24) znalezione.Jedzenie += los.Next(1, 4);
            else if (r < 46) znalezione.Woda += los.Next(1, 4);
            else if (r < 62) znalezione.Paliwo += los.Next(1, 3);
            else if (r < 84) znalezione.Zlom += los.Next(2, 6);
            else if (r < 91) znalezione.Leki += 1;
            else if (r < 97 || znalezionoPistolet) znalezione.Amunicja += los.Next(2, 5);
            else
            {
                // Pistolet dostaje pierwszy, kto go nie ma.
                znalezionoPistolet = true;
                var bezBroni = j.Kto.Pistolet ? ekipa.Find(e => e.Zyje && !e.Kto.Pistolet) : j;
                if (bezBroni != null)
                {
                    bezBroni.Kto.Pistolet = true;
                    dodatek = $" PISTOLET dla: {bezBroni.Nazwa}!";
                }
                znalezione.Amunicja += 3;
            }
        }
        Plecak.Dodaj(znalezione);
        // Amunicja i leki przydają się od razu, więc trafiają prosto do zapasów.
        g.Z.Amunicja += znalezione.Amunicja;
        g.Z.Leki += znalezione.Leki;
        Plecak.Amunicja -= znalezione.Amunicja;
        Plecak.Leki -= znalezione.Leki;

        lup.Pusty = true;
        foreach (var b in lup.Bryly)
            b.Color = new Color(b.Color.R / 2, b.Color.G / 2, b.Color.B / 2);
        g.Log($"{lup.Nazwa}: {Schron.Spis(znalezione)}.{dodatek}");
        Napis("łup!", j, Color.LimeGreen);
    }

    // ================= Dotyk w widoku =================

    public void Dotknij(Vector2 ekran)
    {
        if (turaWroga) return;
        float zasieg = 40 * Scena.Zoom;
        float najblizej = zasieg;
        Jednostka trafiona = null;
        Lup trafionyLup = null;

        foreach (var j in ekipa) Sprawdz(j);
        foreach (var j in wrogowie) Sprawdz(j);
        foreach (var lup in lupy)
        {
            float d = Vector2.Distance(ekran, Scena.NaEkran(lup.X + 0.5f, lup.Y + 0.5f, 0.4f));
            if (d < najblizej) { najblizej = d; trafionyLup = lup; trafiona = null; }
        }

        void Sprawdz(Jednostka j)
        {
            if (!j.Zyje) return;
            float d = Vector2.Distance(ekran, Scena.NaEkran(j.Poz.X, j.Poz.Y, j.Wzrost * 0.6f));
            if (d < najblizej) { najblizej = d; trafiona = j; }
        }

        if (trafiona != null && !trafiona.Wrog)
        {
            wybrana = trafiona;
            return;
        }

        var kto = wybrana;
        if (kto == null || !kto.Zyje || kto.WRuchu) return;

        if (trafiona != null)
        {
            AtakGracza(kto, trafiona);
            return;
        }

        if (trafionyLup != null)
        {
            if (Odleglosc(kto.X, kto.Y, trafionyLup.X, trafionyLup.Y) <= 1)
                Przeszukaj(kto, trafionyLup);
            else
            {
                var droga = Droga(kto.X, kto.Y, trafionyLup.X, trafionyLup.Y, obok: true);
                if (droga == null) { g.Log("Nie da się tam dojść."); return; }
                kto.CelLupu = trafionyLup;
                Idz(kto, droga);
            }
            return;
        }

        var iso = Scena.NaIso(ekran);
        int x = (int)MathF.Floor(iso.X), y = (int)MathF.Floor(iso.Y);
        if (!NaMapie(x, y)) return;
        if (!Wolne(x, y)) return;
        var sciezka = Droga(kto.X, kto.Y, x, y, obok: false);
        if (sciezka == null) { g.Log("Nie da się tam dojść."); return; }
        kto.CelLupu = null;
        Idz(kto, sciezka);
    }

    void Idz(Jednostka kto, List<Point> droga)
    {
        if (walka && droga.Count > kto.Pa)
        {
            if (kto.Pa == 0) { g.Log($"{kto.Nazwa} nie ma już PA – zakończ turę."); return; }
            droga.RemoveRange(kto.Pa, droga.Count - kto.Pa);
        }
        kto.Sciezka.Clear();
        kto.Sciezka.AddRange(droga);

        // Kolejność marszu: prowadzący, potem reszta.
        lancuch.Clear();
        lancuch.Add(kto);
        foreach (var j in ekipa)
            if (j != kto && j.Zyje)
                lancuch.Add(j);
    }

    // ================= Rysowanie =================

    public void RysujWidok()
    {
        Scena.Rysuj(g.Widok);
        float rozmiar = Math.Clamp(13 * Scena.Zoom, 14, 30);

        foreach (var lup in lupy)
            if (!lup.Pusty)
            {
                var p = Scena.NaEkran(lup.X + 0.5f, lup.Y + 0.5f, 1.6f);
                g.DrawTextCentered(lup.Nazwa, p.X, p.Y, rozmiar * 0.8f, new Color(240, 220, 150));
            }

        var wyjscie = Scena.NaEkran(0.5f, srodek + 0.5f, 0.6f);
        g.DrawTextCentered("◀ SCHRON", wyjscie.X, wyjscie.Y, rozmiar, Color.Yellow);

        foreach (var w in wrogowie)
        {
            if (!w.Zyje) continue;
            var p = Scena.NaEkran(w.Poz.X, w.Poz.Y, w.Wzrost + 0.75f);
            float szer = 46 * Scena.Zoom / 1.5f, wys = Math.Max(4, 5 * Scena.Zoom / 1.5f);
            g.FillRect(p.X - szer / 2, p.Y, szer, wys, new Color(60, 20, 20));
            g.FillRect(p.X - szer / 2, p.Y, szer * w.Hp / w.MaxHp, wys, w.Alarm ? Color.Red : new Color(200, 140, 60));
            g.DrawTextCentered(w.Nazwa, p.X, p.Y - rozmiar * 0.6f, rozmiar * 0.75f, w.Alarm ? new Color(255, 150, 140) : Color.LightGray);
        }

        foreach (var j in ekipa)
        {
            if (!j.Zyje) continue;
            var p = Scena.NaEkran(j.Poz.X, j.Poz.Y, 1.45f);
            bool zaznaczona = j == wybrana;
            g.DrawTextCentered(zaznaczona ? "▼ " + j.Nazwa : j.Nazwa, p.X, p.Y, rozmiar, zaznaczona ? Color.Yellow : Color.White);
        }

        foreach (var n in napisy)
        {
            var p = Scena.NaEkran(n.gdzie);
            g.DrawTextCentered(n.tekst, p.X, p.Y, rozmiar * 1.3f, n.kolor);
        }
    }

    public void RysujPanel()
    {
        string tytul = !walka ? "WYPRAWA – zwiad" : turaWroga ? "WALKA – tura wroga…" : "WALKA – twoja tura";
        g.Napis(tytul, 16, 12, 34, walka ? new Color(255, 120, 100) : Color.White);
        g.Napis("W plecakach: " + Schron.Spis(Plecak), 16, 68, 24, new Color(240, 220, 150));
        g.Napis($"Amunicja {g.Z.Amunicja}    Leki {g.Z.Leki}", 16, 100, 24, Color.LightGray);

        for (int i = 0; i < ekipa.Count; i++)
        {
            var j = ekipa[i];
            string stan = !j.Zyje ? "nie żyje" : (j.Kto.Pistolet ? "pistolet" : "pałka") + (walka ? $"   PA {j.Pa}/{j.MaxPa}" : "");
            if (g.Karta(i, ekipa.Count, j.Kto, j == wybrana, stan, tylkoZdrowie: true) && j.Zyje)
                wybrana = j;
        }

        var kto = wybrana;
        bool gotowy = kto != null && kto.Zyje && !turaWroga;

        if (g.Przycisk(8, 314, 348, 86, "Koniec tury", walka && !turaWroga && !KtosWRuchu()))
            KoniecTuryGracza();

        bool moznaLeczyc = gotowy && g.Z.Leki > 0 && kto.Kto.Zdrowie < 100 && (!walka || kto.Pa >= 2);
        if (g.Przycisk(364, 314, 348, 86, walka ? "Lecz (2 PA)" : "Lecz", moznaLeczyc))
        {
            g.Z.Leki--;
            if (walka) kto.Pa -= 2;
            kto.Kto.Zdrowie = Math.Min(100, kto.Kto.Zdrowie + 40);
            Napis("+40", kto, Color.LimeGreen);
        }

        // Powrót: poza walką wystarczy, że ktoś stoi przy wyjściu; w walce muszą tam być wszyscy.
        bool ktos = false, wszyscy = true;
        foreach (var j in ekipa)
        {
            if (!j.Zyje) continue;
            if (Wyjscie(j.X, j.Y)) ktos = true;
            else wszyscy = false;
        }
        bool powrot = !turaWroga && !KtosWRuchu() && (walka ? wszyscy : ktos);
        if (g.Przycisk(8, 408, 704, 86, walka ? "Uciekaj do schronu" : "Wróć do schronu", powrot))
        {
            g.KoniecWyprawy(true);
            return;
        }

        string rada = walka
            ? "Dotknij wroga: atak (3 PA). Pole: ruch (1 PA za kratkę)."
            : "Łupy zabierzesz, wracając na żółte pola (◀ SCHRON).";
        g.Napis(rada, 12, 506, 22, Color.LightGray);
        g.Napis("Dwa palce: przesuwanie i zoom widoku.", 12, 536, 22, Color.Gray);
    }
}
