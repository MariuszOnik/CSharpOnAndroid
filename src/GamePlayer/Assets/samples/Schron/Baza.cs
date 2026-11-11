// Schron: powierzchnia (poziom 0) i do trzech poziomów pod ziemią, połączonych drabinami przez włazy.
// Każdy poziom to korytarz 8×2 kratki: z przodu chodnik, z tyłu miejsca na urządzenia.

class Baza
{
    public const int Szerokosc = 8;
    public const float Odstep = 4.5f; // różnica wysokości między poziomami (w kratkach)
    public const int MaxPoziomow = 3;
    public const int KosztPoziomu = 12;
    const float SekundNaGodzine = 4f;
    const float Predkosc = 3f;        // kratki na sekundę

    public readonly Scena Scena;
    public readonly List<Urzadzenie> Urzadzenia = new();
    public int Poziomy = 1;
    public float Godzina = 8;
    public int Tempo = 1;
    public float ZapasPradu = 6;
    public Ocalaly Wybrany;

    // Stan panelu
    bool menuBudowy, wyborEkipy;
    int budowane = -1;
    readonly List<Ocalaly> ekipa = new();
    bool zgloszonoBrakPradu;

    readonly Schron g;

    static readonly Color Beton = new(128, 132, 140), Sciana = new(124, 134, 150), Metal = new(70, 74, 82);
    static readonly Color Ziemia = new(112, 104, 66), Drewno = new(150, 104, 58);

    public Baza(Schron g)
    {
        this.g = g;
        Scena = new Scena(g, g.Grafika);

        Postaw(Urzadzenie.Generator, 1, 0);
        Postaw(Urzadzenie.Zbiornik, 1, 2);
        Postaw(Urzadzenie.Stol, 1, 3);
        Postaw(Urzadzenie.Prycza, 1, 4);
        Postaw(Urzadzenie.Prycza, 1, 5);

        for (int i = 0; i < g.Ludzie.Count; i++)
        {
            var o = g.Ludzie[i];
            o.Cialo = Scena.NowyGlif("czlowiek", o.Kolor, 0.4f, 0.4f, 1.1f, ruchomy: true);
            Ustaw(o, 1, 2.5f + i * 1.2f);
        }
        Wybrany = g.Ludzie[0];
        Przebuduj();

        Dopasuj();
    }

    // Ustawia zoom i kamerę tak, żeby cały schron (z powierzchnią) mieścił się w widoku.
    void Dopasuj()
    {
        var swiat = g.Grafika.Swiat;
        float lewo = swiat.ToScreen(new Vector3(0, 4, 0)).X, prawo = swiat.ToScreen(new Vector3(Szerokosc, 0, 0)).X;
        float gora = swiat.ToScreen(new Vector3(Szerokosc, 4, 1.8f)).Y, dol = swiat.ToScreen(new Vector3(0, 0, Z(Poziomy) - 0.25f)).Y;
        Scena.Zoom = Math.Min(g.Widok.Width / (prawo - lewo + 50), g.Widok.Height / (dol - gora + 40));
        Scena.Kamera = new Vector2(g.Widok.Center.X, g.Widok.Center.Y) - new Vector2((lewo + prawo) / 2, (gora + dol) / 2) * Scena.Zoom;
    }

    public bool Prad => ZapasPradu > 0;
    public int Dzien => 1 + (int)(Godzina / 24);

    // Drabina między poziomem l a l+1 stoi na zmianę z lewej i z prawej strony.
    public static int DrabinaX(int l) => l % 2 == 0 ? 1 : 6;

    static float Z(int poziom) => -poziom * Odstep;

    static float Do(float a, float b, float krok) => Math.Abs(b - a) <= krok ? b : a + Math.Sign(b - a) * krok;

    // ================= Budowa sceny =================

    public void Przebuduj()
    {
        Scena.Obiekty.Clear();

        // Powierzchnia z włazem
        Podloga(0, 4, Ziemia, DrabinaX(0));
        Scena.Dodaj(DrabinaX(0), 2.02f, 0, 1, 0.08f, 0.9f, Metal); // otwarta klapa
        Scena.DodajGlif("drzewo", Color.White, 4.1f, 2.8f, 0, 0.4f, 0.4f, 1.6f, piksel: 5);
        Scena.DodajGlif("suche", Color.White, 6.9f, 3.3f, 0, 0.3f, 0.3f, 1.2f, piksel: 4);
        Scena.DodajGlif("skala", Color.White, 6.1f, 2.4f, 0, 0.6f, 0.5f, 0.4f, piksel: 4);
        Scena.DodajGlif("skala", Color.White, 2.6f, 3.2f, 0, 0.4f, 0.4f, 0.3f, piksel: 3);

        for (int l = 1; l <= Poziomy; l++)
        {
            Podloga(l, 2, Beton, l < Poziomy ? DrabinaX(l) : -1);
            Scena.Dodaj(0, 2, Z(l), Szerokosc, 0.15f, 1.8f, Sciana);
            Scena.Dodaj(Szerokosc, 0, Z(l), 0.15f, 2.15f, 1.8f, Sciana);
        }

        for (int l = 0; l < Poziomy; l++)
            Drabina(l);

        foreach (var u in Urzadzenia)
            Bryly(u);

        foreach (var o in g.Ludzie)
            if (o.Zyje && !o.NaWyprawie)
            {
                Scena.Obiekty.Add(o.Cialo);
                UstawBryly(o);
            }

        Scena.Odswiez();
    }

    // Płyta podłogi z dziurą na właz w tylnym rzędzie (dziura = -1: bez dziury).
    void Podloga(int poziom, float glebia, Color kolor, int dziura)
    {
        float z = Z(poziom) - 0.25f;
        Scena.Dodaj(0, 0, z, Szerokosc, 1, 0.25f, kolor);
        if (dziura < 0)
            Scena.Dodaj(0, 1, z, Szerokosc, 1, 0.25f, kolor);
        else
        {
            if (dziura > 0)
                Scena.Dodaj(0, 1, z, dziura, 1, 0.25f, kolor);
            if (dziura < Szerokosc - 1)
                Scena.Dodaj(dziura + 1, 1, z, Szerokosc - 1 - dziura, 1, 0.25f, kolor);
        }
        if (glebia > 2)
            Scena.Dodaj(0, 2, z, Szerokosc, glebia - 2, 0.25f, kolor);
    }

    // Drabina z poziomu l+1 w górę, przez właz, trochę ponad podłogę poziomu l.
    void Drabina(int l)
    {
        float x = DrabinaX(l), dol = Z(l + 1), wysokosc = Odstep + 0.5f;
        Scena.Dodaj(x + 0.2f, 1.7f, dol, 0.08f, 0.08f, wysokosc, Drewno);
        Scena.Dodaj(x + 0.72f, 1.7f, dol, 0.08f, 0.08f, wysokosc, Drewno);
        for (float z = 0.3f; z < wysokosc - 0.1f; z += 0.45f)
            Scena.Dodaj(x + 0.28f, 1.7f, dol + z, 0.44f, 0.08f, 0.06f, Drewno);
    }

    void Bryly(Urzadzenie u)
    {
        float x = u.Komorka, y = 1, z = Z(u.Poziom);
        switch (u.Typ)
        {
            case Urzadzenie.Prycza:
                Scena.Dodaj(x + 0.08f, y + 0.25f, z, 0.84f, 0.6f, 0.3f, new Color(120, 96, 80));
                Scena.Dodaj(x + 0.1f, y + 0.3f, z + 0.3f, 0.22f, 0.5f, 0.1f, new Color(230, 230, 220));
                break;
            case Urzadzenie.Stol:
                Scena.Dodaj(x + 0.15f, y + 0.3f, z, 0.7f, 0.5f, 0.45f, Drewno);
                Scena.Dodaj(x + 0.4f, y + 0.45f, z + 0.45f, 0.15f, 0.15f, 0.18f, new Color(200, 60, 50));
                break;
            case Urzadzenie.Zbiornik:
                Scena.Dodaj(x + 0.15f, y + 0.2f, z, 0.7f, 0.7f, 1.1f, new Color(70, 130, 200));
                Scena.Dodaj(x + 0.35f, y + 0.4f, z + 1.1f, 0.3f, 0.3f, 0.1f, Metal);
                break;
            case Urzadzenie.Generator:
                Scena.Dodaj(x + 0.1f, y + 0.25f, z, 0.8f, 0.55f, 0.6f, new Color(200, 160, 40));
                Scena.Dodaj(x + 0.2f, y + 0.35f, z + 0.6f, 0.15f, 0.15f, 0.45f, Metal);
                break;
            case Urzadzenie.Grzadka:
                Scena.Dodaj(x + 0.05f, y + 0.2f, z, 0.9f, 0.7f, 0.3f, new Color(100, 70, 45));
                Scena.Dodaj(x + 0.15f, y + 0.3f, z + 0.3f, 0.7f, 0.5f, 0.3f, new Color(70, 170, 70));
                break;
            case Urzadzenie.Warsztat:
                Scena.Dodaj(x + 0.05f, y + 0.3f, z, 0.9f, 0.5f, 0.55f, new Color(110, 115, 125));
                Scena.Dodaj(x + 0.15f, y + 0.4f, z + 0.55f, 0.3f, 0.25f, 0.2f, new Color(200, 60, 50));
                break;
        }
    }

    void Postaw(int typ, int poziom, int komorka) =>
        Urzadzenia.Add(new Urzadzenie { Typ = typ, Poziom = poziom, Komorka = komorka });

    Urzadzenie UrzadzenieW(int poziom, int komorka)
    {
        foreach (var u in Urzadzenia)
            if (u.Poziom == poziom && u.Komorka == komorka)
                return u;
        return null;
    }

    // Wolne miejsce: pod ziemią, nie pod drabiną, nie nad włazem i bez urządzenia.
    bool MoznaBudowac(int poziom, int komorka)
    {
        if (poziom < 1 || poziom > Poziomy || komorka < 0 || komorka >= Szerokosc) return false;
        if (komorka == DrabinaX(poziom - 1)) return false;
        if (poziom < MaxPoziomow && komorka == DrabinaX(poziom)) return false;
        return UrzadzenieW(poziom, komorka) == null;
    }

    // ================= Ocalali =================

    void Ustaw(Ocalaly o, int poziom, float x)
    {
        o.Poziom = poziom;
        o.X = x;
        o.Y = 0.5f;
        o.Wys = Z(poziom);
        o.Stan = Zajecie.Wolny;
        o.Wspina = false;
        o.Cel = null;
    }

    public void PowrotZWyprawy(Ocalaly o, int ktory)
    {
        o.NaWyprawie = false;
        Ustaw(o, 0, 3f + ktory * 0.9f);
    }

    void UstawBryly(Ocalaly o)
    {
        var c = o.Cialo;
        // Kierunek patrzenia: w prawo na ekranie to rosnące X - Y.
        float stary = c.Position.X - c.Position.Y, nowy = o.X - o.Y;
        if (nowy > stary + 0.001f) c.Odbity = false;
        else if (nowy < stary - 0.001f) c.Odbity = true;

        c.Position = new Vector3(o.X - 0.2f, o.Y - 0.2f, o.Wys);
        bool idzie = o.Stan == Zajecie.Idzie && !o.Wspina && Tempo > 0;
        c.Podskok = idzie ? MathF.Abs(MathF.Sin(g.Czas * 12)) * 2.5f : 0;
        c.Color = o.Stan == Zajecie.Spi ? new Color(120, 120, 150) : Color.White; // śpiący jest przyciemniony
    }

    void Zwolnij(Ocalaly o)
    {
        foreach (var u in Urzadzenia)
            if (u.Uzytkownik == o)
                u.Uzytkownik = null;
    }

    void IdzDo(Ocalaly o, int poziom, float x, Zajecie zamiar = Zajecie.Wolny, Urzadzenie cel = null)
    {
        Zwolnij(o);
        o.CelPoziom = poziom;
        o.CelX = Math.Clamp(x, 0.3f, Szerokosc - 0.3f);
        o.Zamiar = zamiar;
        o.Cel = cel;
        o.Stan = Zajecie.Idzie;
    }

    // Najbliższe urządzenie danego typu (dla pryczy – wolne).
    Urzadzenie Znajdz(Ocalaly o, int typ)
    {
        Urzadzenie najlepsze = null;
        float najblizej = float.MaxValue;
        foreach (var u in Urzadzenia)
        {
            if (u.Typ != typ) continue;
            if (typ == Urzadzenie.Prycza && u.Uzytkownik != null && u.Uzytkownik != o) continue;
            float odleglosc = Math.Abs(u.Poziom - o.Poziom) * 20 + Math.Abs(u.Komorka + 0.5f - o.X);
            if (odleglosc < najblizej)
            {
                najblizej = odleglosc;
                najlepsze = u;
            }
        }
        return najlepsze;
    }

    // Wysyła ocalałego do odpowiedniego urządzenia. Zwraca false, gdy się nie da.
    bool Rozkaz(Ocalaly o, Zajecie zamiar, Urzadzenie cel = null, bool cicho = false)
    {
        int typ = zamiar == Zajecie.Je ? Urzadzenie.Stol : zamiar == Zajecie.Pije ? Urzadzenie.Zbiornik
            : zamiar == Zajecie.Spi ? Urzadzenie.Prycza : Urzadzenie.Warsztat;
        string brak = null;
        if (zamiar == Zajecie.Je && g.Z.Jedzenie <= 0) brak = "Nie ma jedzenia!";
        else if (zamiar == Zajecie.Pije && g.Z.Woda <= 0) brak = "Nie ma wody!";
        else if (zamiar == Zajecie.Pracuje && g.Z.Zlom < 2) brak = "Na amunicję trzeba 2 złomu.";
        else
        {
            cel ??= Znajdz(o, typ);
            if (cel == null)
                brak = typ == Urzadzenie.Prycza ? "Nie ma wolnej pryczy." : $"Brakuje urządzenia: {Urzadzenie.Nazwy[typ]}.";
            else if (typ == Urzadzenie.Prycza && cel.Uzytkownik != null && cel.Uzytkownik != o)
                brak = "Ta prycza jest zajęta.";
        }

        if (brak != null)
        {
            if (!cicho) g.Log(brak);
            return false;
        }

        IdzDo(o, cel.Poziom, cel.Komorka + 0.5f, zamiar, cel);
        if (typ == Urzadzenie.Prycza)
            cel.Uzytkownik = o; // rezerwacja
        return true;
    }

    void Dotarl(Ocalaly o)
    {
        o.Stan = Zajecie.Wolny;
        switch (o.Zamiar)
        {
            case Zajecie.Je:
                if (g.Z.Jedzenie <= 0) { g.Log("Nie ma jedzenia!"); break; }
                g.Z.Jedzenie--;
                o.Stan = Zajecie.Je;
                o.CzasAkcji = 1f;
                break;
            case Zajecie.Pije:
                if (g.Z.Woda <= 0) { g.Log("Nie ma wody!"); break; }
                g.Z.Woda--;
                o.Stan = Zajecie.Pije;
                o.CzasAkcji = 0.5f;
                break;
            case Zajecie.Spi:
                o.Stan = Zajecie.Spi;
                break;
            case Zajecie.Pracuje:
                if (g.Z.Zlom < 2) { g.Log("Na amunicję trzeba 2 złomu."); break; }
                g.Z.Zlom -= 2;
                o.Stan = Zajecie.Pracuje;
                o.CzasAkcji = 3f;
                break;
        }
        o.Zamiar = Zajecie.Wolny;
    }

    void Ruch(Ocalaly o, float t)
    {
        float krok = Predkosc * t;
        if (o.Wspina)
        {
            if (o.Y < 1.3f)
            {
                o.Y = Do(o.Y, 1.3f, krok);
                return;
            }
            float cel = Z(o.CelWspinaczki);
            o.Wys = Do(o.Wys, cel, krok);
            if (o.Wys == cel)
            {
                o.Poziom = o.CelWspinaczki;
                o.Wspina = false;
            }
            return;
        }

        if (o.Poziom != o.CelPoziom)
        {
            int nastepny = o.Poziom < o.CelPoziom ? o.Poziom + 1 : o.Poziom - 1;
            float drabina = DrabinaX(Math.Min(o.Poziom, nastepny)) + 0.5f;
            if (o.Y > 0.5f && o.X != drabina)
            {
                o.Y = Do(o.Y, 0.5f, krok); // najpierw wyjście z włazu na chodnik
                return;
            }
            o.X = Do(o.X, drabina, krok);
            if (o.X == drabina)
            {
                o.Wspina = true;
                o.CelWspinaczki = nastepny;
            }
            return;
        }

        if (o.Y > 0.5f)
        {
            o.Y = Do(o.Y, 0.5f, krok);
            return;
        }
        o.X = Do(o.X, o.CelX, krok);
        if (o.X == o.CelX)
            Dotarl(o);
    }

    // ================= Upływ czasu =================

    public void Update(float dt)
    {
        float t = dt * Tempo;
        if (t <= 0) return;

        Symuluj(t / SekundNaGodzine);
        foreach (var o in g.Ludzie)
        {
            if (!o.Zyje || o.NaWyprawie) continue;
            if (o.Stan == Zajecie.Idzie)
                Ruch(o, t);
            UstawBryly(o);
        }
    }

    // Czas, który minął „poza ekranem” (np. podczas wyprawy).
    public void UplywCzasu(float godziny)
    {
        for (float h = 0; h < godziny; h += 0.25f)
            Symuluj(0.25f);
    }

    void Symuluj(float h)
    {
        Godzina += h;

        // Generator
        if (ZapasPradu > 0) ZapasPradu -= h;
        if (ZapasPradu <= 0)
        {
            if (g.Z.Paliwo > 0)
            {
                g.Z.Paliwo--;
                ZapasPradu = 8;
                zgloszonoBrakPradu = false;
            }
            else if (!zgloszonoBrakPradu)
            {
                zgloszonoBrakPradu = true;
                g.Log("Generator stanął – brak paliwa!");
            }
        }

        // Produkcja
        foreach (var u in Urzadzenia)
        {
            if (!Prad) break;
            if (u.Typ == Urzadzenie.Zbiornik)
            {
                u.Postep += h;
                if (u.Postep >= 6) { u.Postep -= 6; g.Z.Woda++; }
            }
            else if (u.Typ == Urzadzenie.Grzadka)
            {
                u.Postep = Math.Min(12, u.Postep + h);
                if (u.Postep >= 12 && g.Z.Woda > 0) { u.Postep = 0; g.Z.Woda--; g.Z.Jedzenie++; }
            }
        }

        // Potrzeby ludzi
        foreach (var o in g.Ludzie)
        {
            if (!o.Zyje) continue;
            bool spi = o.Stan == Zajecie.Spi && !o.NaWyprawie;
            o.Glod = Math.Min(100, o.Glod + 3f * h);
            o.Pragnienie = Math.Min(100, o.Pragnienie + 4.5f * h);
            o.Zmeczenie = Math.Clamp(o.Zmeczenie + (spi ? -12f : 2.5f) * h, 0, 100);

            float zmiana = 0;
            if (o.Glod >= 100) zmiana -= 4;
            if (o.Pragnienie >= 100) zmiana -= 6;
            if (o.Zmeczenie >= 100) zmiana -= 2;
            if (zmiana == 0 && o.Glod < 60 && o.Pragnienie < 60) zmiana = spi ? 2f : 0.6f;
            o.Zdrowie = Math.Min(100, o.Zdrowie + zmiana * h);

            if (!o.Zyje)
            {
                Smierc(o, o.Pragnienie >= 100 ? "z pragnienia" : o.Glod >= 100 ? "z głodu" : "z wycieńczenia");
                continue;
            }
            if (o.NaWyprawie) continue;

            switch (o.Stan)
            {
                case Zajecie.Je:
                    o.CzasAkcji -= h;
                    if (o.CzasAkcji <= 0) { o.Glod = Math.Max(0, o.Glod - 60); o.Stan = Zajecie.Wolny; }
                    break;
                case Zajecie.Pije:
                    o.CzasAkcji -= h;
                    if (o.CzasAkcji <= 0) { o.Pragnienie = Math.Max(0, o.Pragnienie - 70); o.Stan = Zajecie.Wolny; }
                    break;
                case Zajecie.Pracuje:
                    o.CzasAkcji -= h;
                    if (o.CzasAkcji <= 0)
                    {
                        g.Z.Amunicja += 3;
                        o.Stan = Zajecie.Wolny;
                        g.Log($"{o.Imie}: gotowe 3 naboje.");
                    }
                    break;
                case Zajecie.Spi:
                    if (o.Zmeczenie <= 0)
                    {
                        Zwolnij(o);
                        o.Stan = Zajecie.Wolny;
                        g.Log($"{o.Imie} wstaje wypoczęty.");
                    }
                    break;
                case Zajecie.Wolny:
                    // Bezczynny sam zadba o najpilniejszą potrzebę.
                    if (o.Pragnienie > 80 && g.Z.Woda > 0) Rozkaz(o, Zajecie.Pije, cicho: true);
                    else if (o.Glod > 80 && g.Z.Jedzenie > 0) Rozkaz(o, Zajecie.Je, cicho: true);
                    else if (o.Zmeczenie > 90) Rozkaz(o, Zajecie.Spi, cicho: true);
                    break;
            }
        }
    }

    void Smierc(Ocalaly o, string powod)
    {
        g.Log($"{o.Imie} umiera {powod}.");
        Zwolnij(o);
        Scena.Obiekty.Remove(o.Cialo);
        Scena.Odswiez();
        if (Wybrany == o) Wybrany = null;
        ekipa.Remove(o);
    }

    // ================= Dotyk w widoku =================

    // Która komórka podłogi leży pod palcem? Sprawdza po kolei wszystkie poziomy.
    bool KomorkaPod(Vector2 ekran, out int poziom, out float x, out float y)
    {
        for (int l = 0; l <= Poziomy; l++)
        {
            var iso = Scena.NaIso(ekran, Z(l));
            if (iso.X >= 0 && iso.X < Szerokosc && iso.Y >= 0 && iso.Y < (l == 0 ? 4 : 2))
            {
                poziom = l;
                x = iso.X;
                y = iso.Y;
                return true;
            }
        }
        poziom = 0;
        x = y = 0;
        return false;
    }

    public void Dotknij(Vector2 ekran)
    {
        if (budowane >= 0)
        {
            // Stawianie urządzenia: palec może trafić w chodnik albo w tylny rząd tej samej komórki.
            if (KomorkaPod(ekran, out int bp, out float bx, out _) && MoznaBudowac(bp, (int)bx))
            {
                g.Z.Zlom -= Urzadzenie.Koszty[budowane];
                Postaw(budowane, bp, (int)bx);
                g.Log($"Zbudowano: {Urzadzenie.Nazwy[budowane]}.");
                budowane = -1;
                Przebuduj();
            }
            else
                g.Log("Tu nie można budować – wybierz miejsce z plusem.");
            return;
        }

        float zasieg = 42 * Scena.Zoom;

        // 1. Ocalały
        Ocalaly trafiony = null;
        float najblizej = zasieg;
        foreach (var o in g.Ludzie)
        {
            if (!o.Zyje || o.NaWyprawie) continue;
            float d = Vector2.Distance(ekran, Scena.NaEkran(o.X, o.Y, o.Wys + 0.6f));
            if (d < najblizej) { najblizej = d; trafiony = o; }
        }
        if (trafiony != null)
        {
            if (wyborEkipy) PrzelaczEkipe(trafiony);
            else Wybrany = trafiony;
            return;
        }
        if (wyborEkipy) return;

        // 2. Urządzenie
        Urzadzenie urzadzenie = null;
        najblizej = zasieg;
        foreach (var u in Urzadzenia)
        {
            float d = Vector2.Distance(ekran, Scena.NaEkran(u.Komorka + 0.5f, 1.5f, Z(u.Poziom) + 0.4f));
            if (d < najblizej) { najblizej = d; urzadzenie = u; }
        }
        if (urzadzenie != null)
        {
            UzyjUrzadzenia(urzadzenie);
            return;
        }

        // 3. Podłoga – idź tam
        if (Wybrany != null && KomorkaPod(ekran, out int poziom, out float x, out _))
            IdzDo(Wybrany, poziom, x);
    }

    void UzyjUrzadzenia(Urzadzenie u)
    {
        switch (u.Typ)
        {
            case Urzadzenie.Generator:
                g.Log(Prad ? $"Generator działa. Paliwa starczy na {ZapasPradu + g.Z.Paliwo * 8:0} h." : "Generator stoi – potrzebne paliwo.");
                return;
            case Urzadzenie.Grzadka:
                g.Log($"Grządka: {u.Postep / 12 * 100:0}% do zbioru" + (Prad ? "." : " (brak prądu)."));
                return;
        }
        if (Wybrany == null)
        {
            g.Log($"{u.Nazwa}: {Urzadzenie.Opisy[u.Typ]}.");
            return;
        }
        var zamiar = u.Typ == Urzadzenie.Prycza ? Zajecie.Spi : u.Typ == Urzadzenie.Stol ? Zajecie.Je
            : u.Typ == Urzadzenie.Zbiornik ? Zajecie.Pije : Zajecie.Pracuje;
        Rozkaz(Wybrany, zamiar, u);
    }

    void PrzelaczEkipe(Ocalaly o)
    {
        if (!ekipa.Remove(o))
            ekipa.Add(o);
    }

    // ================= Rysowanie =================

    public void RysujWidok()
    {
        Scena.Rysuj(g.Widok);
        if (!Prad)
            g.FillRect(g.Widok, Color.Black * 0.35f);

        float rozmiar = Math.Clamp(13 * Scena.Zoom, 14, 30);

        foreach (var u in Urzadzenia)
        {
            var p = Scena.NaEkran(u.Komorka + 0.5f, 1.5f, Z(u.Poziom) + 1.5f);
            string napis = u.Typ == Urzadzenie.Prycza && u.Uzytkownik != null && u.Uzytkownik.Stan == Zajecie.Spi ? "Zzz" : u.Nazwa;
            g.DrawTextCentered(napis, p.X, p.Y, rozmiar * 0.8f, new Color(200, 205, 215));
        }

        if (budowane >= 0)
            for (int l = 1; l <= Poziomy; l++)
                for (int k = 0; k < Szerokosc; k++)
                    if (MoznaBudowac(l, k))
                    {
                        var p = Scena.NaEkran(k + 0.5f, 1.5f, Z(l) + 0.3f);
                        g.DrawTextCentered("+", p.X, p.Y, rozmiar * 2.2f, Color.LimeGreen);
                    }

        foreach (var o in g.Ludzie)
        {
            if (!o.Zyje || o.NaWyprawie) continue;
            var p = Scena.NaEkran(o.X, o.Y, o.Wys + 1.45f);
            bool zaznaczony = wyborEkipy ? ekipa.Contains(o) : o == Wybrany;
            g.DrawTextCentered(zaznaczony ? "▼ " + o.Imie : o.Imie, p.X, p.Y, rozmiar, zaznaczony ? Color.Yellow : Color.White);
        }
    }

    public void RysujPanel()
    {
        // Nagłówek: czas i tempo
        int minuty = (int)(Godzina * 60);
        g.Napis($"Dzień {Dzien}   {minuty / 60 % 24:00}:{minuty % 60:00}", 16, 12, 34, Color.White);
        if (g.Przycisk(432, 6, 88, 52, "II", wybrany: Tempo == 0)) Tempo = 0;
        if (g.Przycisk(526, 6, 88, 52, "▶", wybrany: Tempo == 1)) Tempo = 1;
        if (g.Przycisk(620, 6, 88, 52, "▶▶", wybrany: Tempo == 4)) Tempo = 4;

        g.RysujZasoby(68, Prad ? "Prąd: jest" : "Prąd: BRAK");

        // Karty ocalałych
        for (int i = 0; i < g.Ludzie.Count; i++)
        {
            var o = g.Ludzie[i];
            bool zaznaczony = wyborEkipy ? ekipa.Contains(o) : o == Wybrany;
            if (g.Karta(i, g.Ludzie.Count, o, zaznaczony, o.Opis()) && o.Zyje && !o.NaWyprawie)
            {
                if (wyborEkipy) PrzelaczEkipe(o);
                else Wybrany = o;
            }
        }

        if (wyborEkipy) PanelEkipy();
        else if (budowane >= 0) PanelStawiania();
        else if (menuBudowy) PanelBudowy();
        else PanelAkcji();
    }

    void PanelAkcji()
    {
        var o = Wybrany;
        bool jest = o != null && o.Zyje;
        if (g.Przycisk(8, 314, 229, 86, "Jedz", jest)) Rozkaz(o, Zajecie.Je);
        if (g.Przycisk(245, 314, 229, 86, "Pij", jest)) Rozkaz(o, Zajecie.Pije);
        if (g.Przycisk(482, 314, 230, 86, "Śpij", jest)) Rozkaz(o, Zajecie.Spi);

        if (g.Przycisk(8, 408, 229, 86, "Lecz", jest && g.Z.Leki > 0 && o.Zdrowie < 100))
        {
            g.Z.Leki--;
            o.Zdrowie = Math.Min(100, o.Zdrowie + 40);
            g.Log($"{o.Imie} opatruje rany.");
        }
        if (g.Przycisk(245, 408, 229, 86, "Rób amunicję", jest)) Rozkaz(o, Zajecie.Pracuje);
        if (g.Przycisk(482, 408, 230, 86, "Stop", jest && o.Stan != Zajecie.Wolny))
        {
            Zwolnij(o);
            if (o.Wspina || o.Y > 0.5f) IdzDo(o, o.Wspina ? o.CelWspinaczki : o.Poziom, o.X); // dokończ wspinaczkę
            else o.Stan = Zajecie.Wolny;
        }

        if (g.Przycisk(8, 502, 348, 86, "Buduj")) menuBudowy = true;
        if (g.Przycisk(364, 502, 348, 86, "Wyprawa"))
        {
            wyborEkipy = true;
            ekipa.Clear();
            if (jest) ekipa.Add(o);
        }
    }

    void PanelBudowy()
    {
        int[] typy = { Urzadzenie.Prycza, Urzadzenie.Stol, Urzadzenie.Zbiornik, Urzadzenie.Grzadka, Urzadzenie.Warsztat };
        for (int i = 0; i < typy.Length; i++)
        {
            int typ = typy[i], koszt = Urzadzenie.Koszty[typ];
            if (g.Przycisk(8 + i % 2 * 356, 314 + i / 2 * 70, 348, 64, $"{Urzadzenie.Nazwy[typ]}  ({koszt} złomu)", g.Z.Zlom >= koszt))
            {
                budowane = typ;
                menuBudowy = false;
                g.Log($"{Urzadzenie.Nazwy[typ]}: {Urzadzenie.Opisy[typ]}.");
            }
        }
        bool moznaKopac = Poziomy < MaxPoziomow && g.Z.Zlom >= KosztPoziomu;
        if (g.Przycisk(364, 454, 348, 64, Poziomy < MaxPoziomow ? $"Nowy poziom  ({KosztPoziomu} złomu)" : "Nowy poziom (maks.)", moznaKopac))
        {
            g.Z.Zlom -= KosztPoziomu;
            Poziomy++;
            menuBudowy = false;
            Przebuduj();
            Dopasuj();
            g.Log($"Wykopano poziom {Poziomy}.");
        }
        if (g.Przycisk(8, 524, 704, 64, "Wróć")) menuBudowy = false;
    }

    void PanelStawiania()
    {
        g.Napis($"{Urzadzenie.Nazwy[budowane]}: dotknij miejsca z zielonym plusem.", 12, 330, 26, Color.LimeGreen);
        if (g.Przycisk(8, 502, 704, 86, "Anuluj")) budowane = -1;
    }

    void PanelEkipy()
    {
        g.Napis("Kto idzie na wyprawę? Dotknij kart albo ludzi.", 12, 322, 26, Color.Yellow);
        g.Napis("Każdy bierze 1 wodę. Na zewnątrz czekają łupy i wrogowie.", 12, 358, 22, Color.LightGray);
        g.Napis("Reszta zostaje w schronie – wrócisz po ok. 6 godzinach.", 12, 388, 22, Color.LightGray);

        ekipa.RemoveAll(o => !o.Zyje);
        bool gotowi = ekipa.Count > 0 && g.Z.Woda >= ekipa.Count;
        string napis = ekipa.Count == 0 ? "Ruszaj" : g.Z.Woda >= ekipa.Count ? $"Ruszaj ({ekipa.Count} os.)" : "Za mało wody";
        if (g.Przycisk(8, 502, 348, 86, napis, gotowi))
        {
            g.Z.Woda -= ekipa.Count;
            wyborEkipy = false;
            foreach (var o in ekipa) Zwolnij(o);
            g.StartWyprawy(new List<Ocalaly>(ekipa));
            ekipa.Clear();
            return;
        }
        if (g.Przycisk(364, 502, 348, 86, "Anuluj")) wyborEkipy = false;
    }
}
