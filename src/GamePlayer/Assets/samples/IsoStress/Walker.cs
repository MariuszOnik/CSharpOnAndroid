// Ludzik chodzący po ulicach. Osobny plik: projekt może mieć ich dowolnie wiele.
class Walker
{
    public IsoObject Object;
    public int FromX, FromY, ToX, ToY, DirX, DirY;
    public float Progress = 1;
    public float Speed;

    public void Update(float dt, int mapSize, Random random)
    {
        Progress += dt * Speed;
        if (Progress >= 1)
        {
            Progress -= 1;
            FromX = ToX;
            FromY = ToY;
            PickNextStep(mapSize, random);
        }

        float x = FromX + (ToX - FromX) * Progress, y = FromY + (ToY - FromY) * Progress;
        Object.Position = new Vector3(x + 0.35f, y + 0.35f, 0);
    }

    void PickNextStep(int mapSize, Random random)
    {
        var options = new List<(int, int)>();
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            int x = FromX + dx, y = FromY + dy;
            if (x < 0 || y < 0 || x >= mapSize || y >= mapSize || !IsoStress.IsStreet(x, y)) continue;
            if (dx == -DirX && dy == -DirY && (DirX != 0 || DirY != 0)) continue; // bez zawracania
            options.Add((dx, dy));
        }

        (int, int) choice;
        if (options.Count == 0) choice = (-DirX, -DirY); // ślepa uliczka
        else if (options.Contains((DirX, DirY)) && random.Next(4) != 0) choice = (DirX, DirY);
        else choice = options[random.Next(options.Count)];

        (DirX, DirY) = choice;
        ToX = FromX + DirX;
        ToY = FromY + DirY;
        if (ToX < 0 || ToY < 0 || ToX >= mapSize || ToY >= mapSize)
        {
            ToX = FromX;
            ToY = FromY;
        }
    }
}
