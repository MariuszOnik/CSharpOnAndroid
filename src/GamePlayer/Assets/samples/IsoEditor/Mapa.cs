// Dane mapy: dwie warstwy liczb. Każda liczba to numer klatki z atlasu, -1 = puste pole.
// Mapa nic nie wie o rysowaniu ani dotyku – to tylko tablice, zapis i odczyt.
class Mapa
{
    public readonly int Rozmiar;
    public int[,] Teren;    // grass_and_water.png
    public int[,] Obiekty;  // farma.png

    public Mapa(int rozmiar)
    {
        Rozmiar = rozmiar;
        Teren = new int[rozmiar, rozmiar];
        Obiekty = new int[rozmiar, rozmiar];
        Wyczysc();
    }

    public void Wyczysc()
    {
        for (int x = 0; x < Rozmiar; x++)
            for (int y = 0; y < Rozmiar; y++)
            {
                Teren[x, y] = 0;     // klatka 0 = trawa
                Obiekty[x, y] = -1;
            }
    }

    public bool WSrodku(int x, int y) => x >= 0 && y >= 0 && x < Rozmiar && y < Rozmiar;

    // ---------- Cofanie: kopia obu warstw ----------

    public (int[,] teren, int[,] obiekty) Kopia() => ((int[,])Teren.Clone(), (int[,])Obiekty.Clone());

    public void Przywroc((int[,] teren, int[,] obiekty) kopia)
    {
        Teren = kopia.teren;
        Obiekty = kopia.obiekty;
    }

    // ---------- Zapis do tekstu ----------
    // Pierwsza linia: rozmiar. Potem Rozmiar linii terenu i Rozmiar linii obiektów, liczby rozdzielone spacją.

    public string DoTekstu()
    {
        var tekst = new System.Text.StringBuilder();
        tekst.AppendLine(Rozmiar.ToString());
        foreach (var warstwa in new[] { Teren, Obiekty })
            for (int y = 0; y < Rozmiar; y++)
            {
                for (int x = 0; x < Rozmiar; x++)
                    tekst.Append(warstwa[x, y]).Append(x < Rozmiar - 1 ? " " : "");
                tekst.AppendLine();
            }
        return tekst.ToString();
    }

    public static Mapa ZTekstu(string tekst)
    {
        var linie = tekst.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var mapa = new Mapa(int.Parse(linie[0]));
        int n = mapa.Rozmiar;
        for (int y = 0; y < n; y++)
        {
            var teren = linie[1 + y].Split(' ');
            var obiekty = linie[1 + n + y].Split(' ');
            for (int x = 0; x < n; x++)
            {
                mapa.Teren[x, y] = int.Parse(teren[x]);
                mapa.Obiekty[x, y] = int.Parse(obiekty[x]);
            }
        }
        return mapa;
    }
}
