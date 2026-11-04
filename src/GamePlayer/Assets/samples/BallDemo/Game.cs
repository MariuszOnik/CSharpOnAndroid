// Twoja gra. Kliknij "Graj", żeby ją skompilować i uruchomić.
// Wstecz wraca do edytora.

class MojaGra : GameScript
{
    Vector2 gracz;
    Vector2 pilka;
    Vector2 predkosc = new Vector2(400, 300);
    int odbicia;
    float obrot;
    Texture2D obrazekPilki;

    public override void Start()
    {
        obrazekPilki = LoadImage("pilka.png");
        Background = new Color(20, 24, 40);
        gracz = new Vector2(Width / 2, Height - 200);
        pilka = new Vector2(Width / 2, Height / 3);
    }

    public override void Update(float dt)
    {
        // Gracz podąża za palcem.
        if (IsTouching)
            gracz = Vector2.Lerp(gracz, TouchPosition, 10 * dt);

        pilka += predkosc * dt;
        obrot += 5 * dt;

        if (pilka.X < 0 || pilka.X > Width - 40) predkosc.X = -predkosc.X;
        if (pilka.Y < 0 || pilka.Y > Height - 40) predkosc.Y = -predkosc.Y;

        var rGracz = new Rectangle((int)gracz.X - 100, (int)gracz.Y - 20, 200, 40);
        var rPilka = new Rectangle((int)pilka.X, (int)pilka.Y, 40, 40);
        if (rGracz.Intersects(rPilka) && predkosc.Y > 0)
        {
            predkosc.Y = -predkosc.Y * 1.05f;
            odbicia++;
        }
    }

    public override void Draw()
    {
        FillRect(gracz.X - 100, gracz.Y - 20, 200, 40, Color.Orange);
        // Piłka jest 40x40, a obrazek 64x64, więc skala 40/64.
        DrawImageCentered(obrazekPilki, pilka + new Vector2(20, 20), obrot, 40f / 64f);

        DrawText($"Odbicia: {odbicia}", 30, 60, 48, Color.LimeGreen);
    }
}
