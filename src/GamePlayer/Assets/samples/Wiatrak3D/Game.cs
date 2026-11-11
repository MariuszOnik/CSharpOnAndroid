// Scena 3D: wiatrak z obracającymi się śmigłami, drzewa i kamera krążąca wokół.
// Jeden palec – obrót kamery, dwa palce – przybliżanie.
// Modele leżą w folderze models/ projektu (OBJ + MTL + tekstury PNG).

class Wiatrak3D : GameScript
{
    Model3D wiatrak, drzewo, ziemia;
    List<Vector4> drzewa = new();   // x, z, obrót, skala

    float czas;
    float obrot = 0.7f, uniesienie = 0.3f, odleglosc = 16;
    Vector2 poprzedniPalec;
    float poprzedniRozstaw;
    int poprzedniaLiczbaPalcow;

    public override void Start()
    {
        Background = new Color(130, 180, 235);
        wiatrak = LoadModel("wiatrak.obj");
        drzewo = LoadModel("drzewo.obj");
        ziemia = LoadModel("ziemia.obj");

        var los = new Random(3);
        while (drzewa.Count < 40)
        {
            float x = los.Next(-26, 27), z = los.Next(-26, 27);
            if (new Vector2(x, z).Length() > 6)   // nie sadzimy drzew pod wiatrakiem
                drzewa.Add(new Vector4(x, z, (float)los.NextDouble() * 6, 0.7f + (float)los.NextDouble() * 0.9f));
        }
    }

    public override void Update(float dt)
    {
        czas += dt;
        Sterowanie();
        Camera.Orbit(new Vector3(0, 3, 0), odleglosc, obrot, uniesienie);

        // Animacja części: śmigła to osobny obiekt „o smigla” w pliku OBJ.
        wiatrak.Part("smigla").SetRotation(Vector3.UnitZ, czas * 1.5f);
    }

    void Sterowanie()
    {
        int palce = Touches.Count;
        if (palce == 1)
        {
            var palec = Touches[0].Position;
            if (poprzedniaLiczbaPalcow == 1)
            {
                obrot -= (palec.X - poprzedniPalec.X) * 0.006f;
                uniesienie = MathHelper.Clamp(uniesienie + (palec.Y - poprzedniPalec.Y) * 0.006f, 0.05f, 1.4f);
            }
            poprzedniPalec = palec;
        }
        else if (palce >= 2)
        {
            float rozstaw = Vector2.Distance(Touches[0].Position, Touches[1].Position);
            if (poprzedniaLiczbaPalcow >= 2 && rozstaw > 1)
                odleglosc = MathHelper.Clamp(odleglosc * poprzedniRozstaw / rozstaw, 6, 45);
            poprzedniRozstaw = rozstaw;
        }
        else
        {
            obrot += 0.1f * (1f / 60);   // bez dotyku kamera powoli krąży sama
        }
        poprzedniaLiczbaPalcow = palce;
    }

    public override void Draw()
    {
        DrawModel(ziemia, Vector3.Zero);
        DrawModel(wiatrak, Vector3.Zero);
        foreach (var d in drzewa)
            DrawModel(drzewo, new Vector3(d.X, 0, d.Y), d.Z, d.W);

        // 2D rysowane po modelach ląduje na wierzchu.
        var szczyt = WorldToScreen(new Vector3(0, 7.2f, 0));
        if (szczyt.Z > 0 && szczyt.Z < 1)
            DrawTextCentered("Wiatrak", szczyt.X, szczyt.Y, 40, Color.White);
        DrawText($"FPS: {Fps:0}   trójkąty wiatraka: {wiatrak.TriangleCount}", 30, 60, 36, Color.White);
    }
}
