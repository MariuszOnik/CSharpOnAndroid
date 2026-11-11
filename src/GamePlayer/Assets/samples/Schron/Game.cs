// SCHRON – przetrwanie w bunkrze, w rzucie izometrycznym. Cała grafika powstaje w kodzie (bryły z IsoSprite).
//
// Górna połowa ekranu to gra, dolna to interfejs.
//   Widok:  dotknij człowieka – wybór; dotknij urządzenia – użyj go; dotknij podłogi – idź tam.
//           Przeciągnij palcem – przesuwanie, dwa palce – zoom.
//   Schron: ludzie głodnieją, chce im się pić i spać. Generator spala paliwo i daje prąd,
//           zbiornik filtruje wodę, grządka daje jedzenie. Za złom budujesz urządzenia i kopiesz nowe poziomy.
//   Wyprawa: losowy teren z łupami i wrogami. Walka jest turowa (punkty akcji).
//
// Pliki: Game.cs (ten – ekran, dotyk, panel), Baza.cs (schron), Wyprawa.cs (teren i walka),
//        Dane.cs (zapasy, ludzie, urządzenia), Grafika.cs (bryły i scena izo).

class Schron : GameScript
{
    public enum Tryb { Baza, Wyprawa, Koniec }

    public Tryb Stan;
    public Zasoby Z;
    public readonly List<Ocalaly> Ludzie = new();
    public Grafika Grafika;
    public Rectangle Widok, Panel; // górna połowa: gra, dolna: interfejs
    public float S = 1;            // skala interfejsu (panel projektowany na 720×800)

    Baza baza;
    Wyprawa wyprawa;
    List<Ocalaly> ekipaWyprawy;
    readonly List<string> komunikaty = new();
    int przetrwaneDni;

    // Dotyk
    enum Gest { Brak, Panel, Widok, DwaPalce }
    Gest gest;
    Vector2 startDotyku, ostatniDotyk, poprzedniSrodek;
    float poprzedniaOdleglosc;
    bool przeciagniete;
    Vector2? klik; // dotknięcie panelu czekające na obsłużenie w Draw

    // ================= Start =================

    public override void Start()
    {
        Background = new Color(24, 22, 26);
        Grafika = new Grafika(this);
        UstawEkran();
        NowaGra();
    }

    void NowaGra()
    {
        Z = new Zasoby { Jedzenie = 8, Woda = 8, Paliwo = 5, Zlom = 6, Leki = 1, Amunicja = 6 };
        Ludzie.Clear();
        Ludzie.Add(new Ocalaly { Imie = "Adam", Kolor = new Color(70, 110, 200), Pistolet = true });
        Ludzie.Add(new Ocalaly { Imie = "Ewa", Kolor = new Color(200, 80, 110), Glod = 20 });
        Ludzie.Add(new Ocalaly { Imie = "Kuba", Kolor = new Color(80, 170, 100), Zmeczenie = 30 });
        komunikaty.Clear();
        wyprawa = null;
        baza = new Baza(this);
        Stan = Tryb.Baza;
        Log("Dotknij człowieka, potem urządzenia albo podłogi.");
        Log("Zapasy topnieją – wyślij kogoś na wyprawę po łupy.");
    }

    // Pionowo: gra na górze, panel na dole. Poziomo: gra z lewej, panel z prawej.
    void UstawEkran()
    {
        if (Height >= Width)
        {
            Widok = new Rectangle(0, 0, Width, Height / 2);
            Panel = new Rectangle(0, Height / 2, Width, Height - Height / 2);
        }
        else
        {
            Widok = new Rectangle(0, 0, Width / 2, Height);
            Panel = new Rectangle(Width / 2, 0, Width - Width / 2, Height);
        }
        S = Math.Min(Panel.Width / 720f, Panel.Height / 800f);
    }

    public void Log(string tekst)
    {
        komunikaty.Add(tekst);
        if (komunikaty.Count > 30)
            komunikaty.RemoveAt(0);
    }

    public static string Spis(Zasoby z)
    {
        var czesci = new List<string>();
        if (z.Jedzenie > 0) czesci.Add($"jedzenie +{z.Jedzenie}");
        if (z.Woda > 0) czesci.Add($"woda +{z.Woda}");
        if (z.Paliwo > 0) czesci.Add($"paliwo +{z.Paliwo}");
        if (z.Zlom > 0) czesci.Add($"złom +{z.Zlom}");
        if (z.Leki > 0) czesci.Add($"leki +{z.Leki}");
        if (z.Amunicja > 0) czesci.Add($"amunicja +{z.Amunicja}");
        return czesci.Count == 0 ? "nic" : string.Join(", ", czesci);
    }

    // ================= Przejścia między schronem a wyprawą =================

    public void StartWyprawy(List<Ocalaly> ekipa)
    {
        ekipaWyprawy = ekipa;
        foreach (var o in ekipa)
            o.NaWyprawie = true;
        baza.Przebuduj();
        wyprawa = new Wyprawa(this, ekipa, baza.Dzien);
        Stan = Tryb.Wyprawa;
        gest = Gest.Brak;
    }

    public void KoniecWyprawy(bool powrot)
    {
        if (wyprawa == null) return;
        if (powrot)
        {
            Z.Dodaj(wyprawa.Plecak);
            Log("Ekipa wróciła. Łup: " + Spis(wyprawa.Plecak) + ".");
        }
        else
            Log("Nikt nie wrócił z wyprawy…");

        int ktory = 0;
        foreach (var o in ekipaWyprawy)
            baza.PowrotZWyprawy(o, ktory++);
        wyprawa = null;
        Stan = Tryb.Baza;
        gest = Gest.Brak;
        baza.Wybrany = Ludzie.Find(o => o.Zyje);
        baza.UplywCzasu(6);
        baza.Przebuduj();
    }

    Scena AktywnaScena => Stan == Tryb.Wyprawa && wyprawa != null ? wyprawa.Scena : baza.Scena;

    // ================= Update =================

    public override void Update(float dt)
    {
        dt = Math.Min(dt, 0.1f);
        UstawEkran();
        ObsluzDotyk();

        if (Stan == Tryb.Baza)
        {
            baza.Update(dt);
            if (!Ludzie.Exists(o => o.Zyje))
            {
                przetrwaneDni = baza.Dzien;
                Stan = Tryb.Koniec;
            }
        }
        else if (Stan == Tryb.Wyprawa)
            wyprawa?.Update(dt);
    }

    void ObsluzDotyk()
    {
        int ile = 0;
        Vector2 p0 = Vector2.Zero, p1 = Vector2.Zero;
        foreach (var t in Touches)
        {
            if (t.State == TouchLocationState.Released) continue;
            if (ile == 0) p0 = t.Position;
            else if (ile == 1) p1 = t.Position;
            ile++;
        }

        var scena = AktywnaScena;

        if (ile == 0)
        {
            // Palec podniesiony bez przeciągania = dotknięcie widoku.
            if (gest == Gest.Widok && !przeciagniete && Stan != Tryb.Koniec)
            {
                if (Stan == Tryb.Baza) baza.Dotknij(startDotyku);
                else wyprawa?.Dotknij(startDotyku);
            }
            gest = Gest.Brak;
            return;
        }

        if (gest == Gest.Brak)
        {
            startDotyku = ostatniDotyk = p0;
            przeciagniete = false;
            if (Panel.Contains(p0))
            {
                gest = Gest.Panel;
                klik = p0;
            }
            else
                gest = Gest.Widok;
        }

        if (gest == Gest.Panel)
            return;

        if (ile >= 2)
        {
            var srodek = (p0 + p1) / 2;
            float odleglosc = Math.Max(1, Vector2.Distance(p0, p1));
            if (gest == Gest.DwaPalce)
            {
                // Zoom wokół punktu między palcami + przesuwanie.
                float nowy = Math.Clamp(scena.Zoom * odleglosc / poprzedniaOdleglosc, 0.6f, 5f);
                scena.Kamera = srodek - (poprzedniSrodek - scena.Kamera) * (nowy / scena.Zoom);
                scena.Zoom = nowy;
            }
            gest = Gest.DwaPalce;
            poprzedniSrodek = srodek;
            poprzedniaOdleglosc = odleglosc;
            return;
        }

        if (gest == Gest.Widok)
        {
            if (Vector2.Distance(p0, startDotyku) > 18 * S)
                przeciagniete = true;
            if (przeciagniete)
                scena.Kamera += p0 - ostatniDotyk;
            ostatniDotyk = p0;
        }
        // Po geście dwoma palcami jeden pozostały palec nic nie robi, aż do podniesienia.
    }

    // ================= Draw =================

    public override void Draw()
    {
        if (Stan == Tryb.Wyprawa && wyprawa != null) wyprawa.RysujWidok();
        else baza.RysujWidok();

        FillRect(Panel, new Color(30, 33, 40));
        FillRect(new Rectangle(Panel.X, Panel.Y, Height >= Width ? Panel.Width : (int)(3 * S), Height >= Width ? (int)(3 * S) : Panel.Height), new Color(90, 96, 110));

        if (Stan == Tryb.Koniec) PanelKonca();
        else if (Stan == Tryb.Wyprawa && wyprawa != null) wyprawa.RysujPanel();
        else baza.RysujPanel();

        // Ostatnie komunikaty na dole panelu, najnowszy najjaśniejszy.
        if (Stan != Tryb.Koniec)
        {
            const int Linie = 5;
            for (int i = 0; i < Linie; i++)
            {
                int indeks = komunikaty.Count - Linie + i;
                if (indeks < 0) continue;
                Napis(Przytnij(komunikaty[indeks], 700, 21), 12, 606 + i * 37, 21, i == Linie - 1 ? Color.White : new Color(150, 155, 165));
            }
        }

        klik = null;
    }

    void PanelKonca()
    {
        Napis("Schron ucichł.", 16, 60, 54, new Color(255, 120, 100));
        Napis($"Przetrwano dni: {przetrwaneDni}", 16, 150, 36, Color.White);
        if (Przycisk(8, 314, 704, 100, "Nowa gra"))
            NowaGra();
    }

    // ================= Klocki interfejsu (współrzędne panelu 720×800) =================

    public Rectangle P(float x, float y, float w, float h) =>
        new((int)(Panel.X + x * S), (int)(Panel.Y + y * S), (int)(w * S), (int)(h * S));

    public void Napis(string tekst, float x, float y, float rozmiar, Color kolor) =>
        DrawText(tekst, Panel.X + x * S, Panel.Y + y * S, rozmiar * S, kolor);

    // Skraca tekst, żeby zmieścił się w podanej szerokości.
    string Przytnij(string tekst, float szerokosc, float rozmiar)
    {
        if (MeasureText(tekst, rozmiar * S).X <= szerokosc * S) return tekst;
        while (tekst.Length > 4 && MeasureText(tekst + "…", rozmiar * S).X > szerokosc * S)
            tekst = tekst.Substring(0, tekst.Length - 1);
        return tekst + "…";
    }

    public bool Klik(Rectangle obszar) => klik.HasValue && obszar.Contains(klik.Value);

    public bool Przycisk(float x, float y, float w, float h, string napis, bool aktywny = true, bool wybrany = false)
    {
        var r = P(x, y, w, h);
        FillRect(r, wybrany ? new Color(180, 130, 40) : aktywny ? new Color(66, 78, 98) : new Color(42, 45, 52));
        FillRect(new Rectangle(r.X, r.Bottom - (int)(5 * S), r.Width, (int)(5 * S)), Color.Black * 0.3f);
        DrawTextCentered(napis, r.Center.X, r.Center.Y, 28 * S, aktywny ? Color.White : new Color(110, 112, 118));
        return aktywny && Klik(r);
    }

    public void Pasek(float x, float y, float w, float h, float ulamek, Color kolor)
    {
        FillRect(P(x, y, w, h), new Color(20, 22, 26));
        FillRect(P(x, y, w * Math.Clamp(ulamek, 0, 1), h), kolor);
    }

    public void RysujZasoby(float y, string dodatek)
    {
        Color Kolor(int ile) => ile == 0 ? new Color(255, 110, 90) : ile <= 2 ? new Color(250, 200, 90) : new Color(205, 210, 220);
        Napis($"Jedzenie {Z.Jedzenie}", 16, y, 24, Kolor(Z.Jedzenie));
        Napis($"Woda {Z.Woda}", 250, y, 24, Kolor(Z.Woda));
        Napis($"Paliwo {Z.Paliwo}", 484, y, 24, Kolor(Z.Paliwo));
        Napis($"Złom {Z.Zlom}", 16, y + 32, 24, new Color(205, 210, 220));
        Napis($"Leki {Z.Leki}", 150, y + 32, 24, new Color(205, 210, 220));
        Napis($"Amunicja {Z.Amunicja}", 250, y + 32, 24, new Color(205, 210, 220));
        Napis(dodatek, 484, y + 32, 24, dodatek.Contains("BRAK") ? new Color(255, 110, 90) : new Color(140, 220, 140));
    }

    // Karta człowieka: imię, stan i paski. Zwraca true, gdy została dotknięta.
    public bool Karta(int ktora, int ile, Ocalaly o, bool wybrana, string stan, bool tylkoZdrowie = false)
    {
        int kolumny = Math.Max(3, ile);
        float w = (704f - (kolumny - 1) * 8) / kolumny, x = 8 + ktora * (w + 8), y = 138;
        var r = P(x, y, w, 166);
        FillRect(r, wybrana ? new Color(74, 70, 46) : new Color(44, 48, 58));
        FillRect(P(x, y, w, 6), o.Zyje ? o.Kolor : new Color(70, 70, 70));
        if (wybrana)
            FillRect(P(x, y + 160, w, 6), Color.Yellow);

        Napis(o.Imie, x + 10, y + 12, 28, o.Zyje ? Color.White : Color.Gray);
        Napis(stan, x + 10, y + 46, 20, new Color(170, 175, 185));

        if (o.Zyje)
        {
            Wiersz("Zdr", o.Zdrowie / 100, new Color(220, 70, 70), 0);
            if (!tylkoZdrowie)
            {
                // Potrzeby rosną od 0 do 100, więc pasek pokazuje „ile zostało”.
                Wiersz("Syt", 1 - o.Glod / 100, new Color(220, 160, 60), 1);
                Wiersz("Woda", 1 - o.Pragnienie / 100, new Color(80, 150, 230), 2);
                Wiersz("Sen", 1 - o.Zmeczenie / 100, new Color(160, 120, 220), 3);
            }
        }
        return Klik(r);

        void Wiersz(string nazwa, float ulamek, Color kolor, int wiersz)
        {
            float wy = y + 76 + wiersz * 21;
            Napis(nazwa, x + 10, wy - 3, 16, new Color(150, 155, 165));
            Pasek(x + 58, wy, w - 68, 13, ulamek, ulamek < 0.2f ? new Color(255, 60, 60) : kolor);
        }
    }
}
