// Twoja gra. Kliknij "Graj", żeby ją skompilować i uruchomić.
// Wstecz wraca do edytora.

class MojaGra : GameScript
{
    Vector2 gracz;
    Vector2 pilka;
    Vector2 predkosc = new Vector2(400, 300);
    int odbicia;

    public override void Start()
    {
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
        FillRect(pilka.X, pilka.Y, 40, 40, Color.White);

        // Licznik odbić jako kwadraciki u góry.
        for (int i = 0; i < odbicia; i++)
            FillRect(20 + i * 30, 60, 20, 20, Color.LimeGreen);
    }
}
