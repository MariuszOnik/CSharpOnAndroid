// Dane gry: zapasy, ocalali i urządzenia schronu.

class Zasoby
{
    public int Jedzenie, Woda, Paliwo, Zlom, Leki, Amunicja;

    public void Dodaj(Zasoby inne)
    {
        Jedzenie += inne.Jedzenie;
        Woda += inne.Woda;
        Paliwo += inne.Paliwo;
        Zlom += inne.Zlom;
        Leki += inne.Leki;
        Amunicja += inne.Amunicja;
    }
}

enum Zajecie { Wolny, Idzie, Je, Pije, Spi, Pracuje }

class Ocalaly
{
    public string Imie;
    public Color Kolor;
    public float Zdrowie = 100, Glod, Pragnienie, Zmeczenie; // potrzeby 0..100, im więcej, tym gorzej
    public bool Pistolet;
    public bool NaWyprawie;
    public bool Zyje => Zdrowie > 0;

    // Położenie w schronie: poziom (0 = powierzchnia), X wzdłuż korytarza, Y w głąb, Wys = wysokość (z).
    public int Poziom;
    public float X, Y = 0.5f, Wys;

    // Dokąd idzie i co zrobi na miejscu.
    public Zajecie Stan;
    public int CelPoziom;
    public float CelX;
    public Zajecie Zamiar;
    public Urzadzenie Cel;
    public float CzasAkcji; // ile godzin zostało do końca jedzenia, picia albo pracy
    public bool Wspina;
    public int CelWspinaczki;

    public IsoObject Cialo, Glowa;

    public string Opis()
    {
        if (!Zyje) return "nie żyje";
        if (NaWyprawie) return "na wyprawie";
        switch (Stan)
        {
            case Zajecie.Idzie: return "idzie";
            case Zajecie.Je: return "je";
            case Zajecie.Pije: return "pije";
            case Zajecie.Spi: return "śpi";
            case Zajecie.Pracuje: return "pracuje";
        }
        return "czeka";
    }
}

class Urzadzenie
{
    public const int Prycza = 0, Stol = 1, Zbiornik = 2, Generator = 3, Grzadka = 4, Warsztat = 5;
    public static readonly string[] Nazwy = { "Prycza", "Stół", "Zbiornik", "Generator", "Grządka", "Warsztat" };
    public static readonly int[] Koszty = { 4, 3, 8, 0, 8, 6 };
    public static readonly string[] Opisy =
    {
        "tu się śpi",
        "tu się je",
        "filtruje wodę (1 na 6 h, wymaga prądu), tu się pije",
        "spala 1 paliwo na 8 h i daje prąd",
        "1 jedzenie na 12 h, zużywa 1 wodę, wymaga prądu",
        "2 złomu → 3 amunicji (3 h pracy)",
    };

    public int Typ, Poziom, Komorka;
    public Ocalaly Uzytkownik; // kto śpi na pryczy
    public float Postep;       // godziny produkcji
    public string Nazwa => Nazwy[Typ];
}
