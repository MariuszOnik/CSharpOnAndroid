using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

[assembly: InternalsVisibleTo("GamePlayer")]

namespace GameApi;

/// <summary>
/// Klasa bazowa dla gry pisanej na telefonie. Nadpisz Start, Update i Draw.
/// </summary>
public abstract class GameScript
{
    public GraphicsDevice GraphicsDevice { get; private set; } = null!;
    public SpriteBatch Batch { get; private set; } = null!;

    /// <summary>Biały piksel 1x1 – do rysowania prostokątów w dowolnym kolorze.</summary>
    public Texture2D Pixel { get; private set; } = null!;

    public int Width => GraphicsDevice.Viewport.Width;
    public int Height => GraphicsDevice.Viewport.Height;

    /// <summary>Kolor tła czyszczony przed każdą klatką.</summary>
    public Color Background { get; set; } = Color.Black;

    public Random Random { get; } = new();

    /// <summary>Narysowane klatki na sekundę. Przy przeciążeniu MonoGame pomija Draw, więc liczenie w Update zawyżałoby wynik.</summary>
    public float Fps { get; private set; }

    // ---------- Dotyk ----------

    public TouchCollection Touches { get; private set; }

    /// <summary>Palec jest na ekranie (przez cały czas dotyku).</summary>
    public bool IsTouching => Touches.Count > 0;

    /// <summary>Palec właśnie dotknął ekranu – prawda tylko w jednej klatce.</summary>
    public bool JustTouched => HasTouch(TouchLocationState.Pressed);

    /// <summary>Palec właśnie został podniesiony – prawda tylko w jednej klatce.</summary>
    public bool JustReleased => HasTouch(TouchLocationState.Released);

    public Vector2 TouchPosition => Touches.Count > 0 ? Touches[0].Position : Vector2.Zero;

    bool HasTouch(TouchLocationState state)
    {
        foreach (var touch in Touches)
            if (touch.State == state)
                return true;
        return false;
    }

    // ---------- Cykl życia ----------

    /// <summary>Wywoływane raz, po przygotowaniu grafiki.</summary>
    public virtual void Start() { }

    /// <summary>Logika gry. dt to czas od poprzedniej klatki w sekundach.</summary>
    public virtual void Update(float dt) { }

    /// <summary>Rysowanie. Batch jest już otwarty (Begin/End robi host).</summary>
    public virtual void Draw() { }

    // ---------- Prostokąty ----------

    public void FillRect(float x, float y, float width, float height, Color color) =>
        FillRect(new Rectangle((int)x, (int)y, (int)width, (int)height), color);

    public void FillRect(Rectangle rect, Color color)
    {
        End3D();
        Batch.Draw(Pixel, rect, color);
    }

    // ---------- Tekst ----------

    /// <summary>
    /// Rysuje tekst. (x, y) to lewy górny róg, size to wysokość czcionki w pikselach. Obsługuje polskie znaki.
    /// Przykład: DrawText($"Wynik: {score}", 30, 80, 48, Color.White);
    /// </summary>
    public void DrawText(string text, float x, float y, float size, Color color)
    {
        if (string.IsNullOrEmpty(text))
            return;
        End3D();
        _text.Draw(Batch, text, new Vector2(x, y), size, color);
    }

    /// <summary>Rysuje tekst wyśrodkowany w punkcie (x, y).</summary>
    public void DrawTextCentered(string text, float x, float y, float size, Color color)
    {
        if (string.IsNullOrEmpty(text))
            return;
        End3D();
        var measured = _text.Measure(text, size);
        _text.Draw(Batch, text, new Vector2(x - measured.X / 2, y - measured.Y / 2), size, color);
    }

    /// <summary>Zwraca szerokość i wysokość tekstu w pikselach.</summary>
    public Vector2 MeasureText(string text, float size) =>
        string.IsNullOrEmpty(text) ? Vector2.Zero : _text.Measure(text, size);

    // ---------- Obrazki ----------

    /// <summary>
    /// Wczytuje obrazek PNG/JPG po nazwie, np. LoadImage("pilka.png").
    /// Szuka w images/ projektu, potem we wspólnym CsharpGame/images, potem we wbudowanych.
    /// Można też podać pełną ścieżkę, np. "/sdcard/Pictures/kot.png".
    /// </summary>
    public Texture2D LoadImage(string name)
    {
        using var stream = _openImage(name);
        return Texture2D.FromStream(GraphicsDevice, stream, DefaultColorProcessors.PremultiplyAlpha);
    }

    /// <summary>
    /// Nazwy obrazków pasujących do wzorca (także z podfolderów), gotowe dla LoadImage.
    /// Przeszukuje images/ projektu i wspólne CsharpGame/images. Przykład: ListImages("tile_*.png").
    /// </summary>
    public IReadOnlyList<string> ListImages(string pattern = "*.png") => _listImages(pattern);

    /// <summary>Rysuje obrazek w punkcie (x, y) – lewy górny róg.</summary>
    public void DrawImage(Texture2D image, float x, float y, float scale = 1f)
    {
        End3D();
        Batch.Draw(image, new Vector2(x, y), null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    /// <summary>Rysuje obrazek wyśrodkowany w punkcie, z obrotem (radiany) i skalą.</summary>
    public void DrawImageCentered(Texture2D image, Vector2 center, float rotation = 0f, float scale = 1f)
    {
        End3D();
        Batch.Draw(image, center, null, Color.White, rotation, new Vector2(image.Width / 2f, image.Height / 2f), scale, SpriteEffects.None, 0f);
    }

    /// <summary>Rysuje obrazek rozciągnięty do prostokąta.</summary>
    public void DrawImage(Texture2D image, Rectangle destination)
    {
        End3D();
        Batch.Draw(image, destination, Color.White);
    }

    /// <summary>
    /// Rysuje wycinek obrazka (np. klatkę z atlasu) tak, że punkt pivot (0..1 względem wycinka) trafia w position.
    /// Przykład: DrawImage(atlas, new Rectangle(32, 0, 32, 32), poz, new Vector2(0.5f, 1f), scale: 3);
    /// </summary>
    public void DrawImage(Texture2D image, Rectangle source, Vector2 position, Vector2 pivot,
        float rotation = 0f, float scale = 1f, Color? color = null, bool flipX = false, bool flipY = false) =>
        DrawRegion(image, source, position, pivot, rotation, new Vector2(scale), color ?? Color.White, flipX, flipY);

    // ---------- Sprite'y (atlas + pivot) ----------

    /// <summary>
    /// Wczytuje obrazek i tnie go na klatki frameWidth×frameHeight (wierszami).
    /// Przykład: var bieg = LoadSprites("bohater.png", 32, 32, new Vector2(0.5f, 1f));
    /// </summary>
    public Sprite[] LoadSprites(string name, int frameWidth, int frameHeight, Vector2? pivot = null, int margin = 0, int spacing = 0) =>
        Sprite.Slice(LoadImage(name), frameWidth, frameHeight, pivot, margin, spacing);

    /// <summary>Rysuje sprite tak, że jego pivot trafia w position. Obrót (radiany) i skala są wokół pivota.</summary>
    public void DrawSprite(Sprite sprite, Vector2 position, float rotation = 0f, float scale = 1f,
        Color? color = null, bool flipX = false, bool flipY = false) =>
        DrawRegion(sprite.Texture, sprite.Source, position, sprite.Pivot, rotation, new Vector2(scale), color ?? Color.White, flipX, flipY);

    /// <summary>Rysuje sprite z pivotem w (x, y).</summary>
    public void DrawSprite(Sprite sprite, float x, float y, float scale = 1f) =>
        DrawSprite(sprite, new Vector2(x, y), 0f, scale);

    /// <summary>Rysuje sprite z osobną skalą w poziomie i pionie (np. rozciąganie, „squash”).</summary>
    public void DrawSprite(Sprite sprite, Vector2 position, Vector2 scale, float rotation = 0f, Color? color = null) =>
        DrawRegion(sprite.Texture, sprite.Source, position, sprite.Pivot, rotation, scale, color ?? Color.White, false, false);

    // Przy odbiciu lustrzanym pivot też się odbija, żeby został w tym samym miejscu rysunku (np. stopy postaci).
    void DrawRegion(Texture2D image, Rectangle source, Vector2 position, Vector2 pivot, float rotation, Vector2 scale, Color color, bool flipX, bool flipY)
    {
        var effects = (flipX ? SpriteEffects.FlipHorizontally : 0) | (flipY ? SpriteEffects.FlipVertically : 0);
        var origin = new Vector2(
            (flipX ? 1 - pivot.X : pivot.X) * source.Width,
            (flipY ? 1 - pivot.Y : pivot.Y) * source.Height);
        End3D();
        Batch.Draw(image, position, source, color, rotation, origin, scale, effects, 0f);
    }

    // ---------- 3D ----------

    /// <summary>Kamera sceny 3D. Przykład: Camera.Orbit(Vector3.Zero, 12, czas, 0.4f);</summary>
    public Camera3D Camera { get; } = new();

    /// <summary>Kierunek, w którym świeci światło (od słońca w stronę sceny).</summary>
    public Vector3 LightDirection { get; set; } = new(-0.4f, -1f, -0.6f);

    /// <summary>Kolor światła kierunkowego.</summary>
    public Color LightColor { get; set; } = new(215, 215, 205);

    /// <summary>Światło rozproszone – jasność ścian odwróconych od światła.</summary>
    public Color AmbientLight { get; set; } = new(95, 100, 115);

    /// <summary>
    /// Wczytuje model 3D z pliku OBJ, np. LoadModel("domek.obj"). Plik .mtl i tekstury leżą obok modelu.
    /// Szuka w models/ projektu, potem we wspólnym CsharpGame/models, potem we wbudowanych.
    /// </summary>
    public Model3D LoadModel(string name) => Model3D.Load(GraphicsDevice, name, _openModel, _openImage);

    /// <summary>
    /// Rysuje model w punkcie position. rotationY – obrót wokół osi pionowej (radiany), color – barwienie.
    /// Modele zasłaniają się nawzajem według odległości; to, co 2D narysujesz później, będzie na wierzchu.
    /// </summary>
    public void DrawModel(Model3D model, Vector3 position, float rotationY = 0f, float scale = 1f, Color? color = null) =>
        DrawModel(model, Matrix.CreateScale(scale) * Matrix.CreateRotationY(rotationY) * Matrix.CreateTranslation(position), color);

    /// <summary>Rysuje model z dowolnym przekształceniem (skala, obroty, przesunięcie złożone w macierz).</summary>
    public void DrawModel(Model3D model, Matrix world, Color? color = null)
    {
        Begin3D();
        var effect = _effect3D!;
        var tint = (color ?? Color.White).ToVector4();
        foreach (var part in model.Parts)
        {
            if (!part.Visible)
                continue;

            effect.World = part.Transform * world;
            foreach (var surface in part.Surfaces)
            {
                var diffuse = surface.Color.ToVector4() * tint;
                effect.DiffuseColor = new Vector3(diffuse.X, diffuse.Y, diffuse.Z);
                effect.Alpha = diffuse.W;
                effect.TextureEnabled = surface.Texture != null;
                effect.Texture = surface.Texture;
                if (surface.Texture != null)
                    GraphicsDevice.SamplerStates[0] = Repeats(surface.Texture) ? SamplerState.LinearWrap : SamplerState.LinearClamp;
                GraphicsDevice.BlendState = diffuse.W < 1f ? BlendState.AlphaBlend : BlendState.Opaque;

                GraphicsDevice.SetVertexBuffer(surface.Buffer);
                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, surface.TriangleCount);
                }
            }
        }
    }

    /// <summary>
    /// Punkt sceny 3D przeliczony na piksele ekranu (np. do napisu nad postacią).
    /// Z &lt; 0 albo Z &gt; 1 oznacza punkt za kamerą lub poza zasięgiem.
    /// </summary>
    public Vector3 WorldToScreen(Vector3 point) =>
        GraphicsDevice.Viewport.Project(point, Camera.Projection(GraphicsDevice.Viewport.AspectRatio), Camera.View, Matrix.Identity);

    /// <summary>
    /// Kończy rysowanie 3D i wraca do 2D. DrawText, DrawImage, DrawSprite i FillRect robią to same –
    /// wywołaj ręcznie tylko przed własnym Batch.Draw po DrawModel.
    /// </summary>
    public void End3D()
    {
        if (!_in3D)
            return;
        _in3D = false;
        GraphicsDevice.SetVertexBuffer(null);
        Batch.Begin();
    }

    BasicEffect? _effect3D;
    bool _in3D;

    // Sprite'y narysowane dotąd trzeba wypchnąć na ekran, zanim pojawi się 3D – inaczej wylądowałyby na wierzchu.
    void Begin3D()
    {
        if (!_in3D)
        {
            _in3D = true;
            Batch.End();
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        _effect3D ??= new BasicEffect(GraphicsDevice);
        var effect = _effect3D;
        effect.View = Camera.View;
        effect.Projection = Camera.Projection(GraphicsDevice.Viewport.AspectRatio);
        effect.LightingEnabled = true;
        effect.AmbientLightColor = AmbientLight.ToVector3();
        effect.DirectionalLight0.Enabled = true;
        effect.DirectionalLight0.Direction = LightDirection.LengthSquared() > 0 ? Vector3.Normalize(LightDirection) : Vector3.Down;
        effect.DirectionalLight0.DiffuseColor = LightColor.ToVector3();
        effect.DirectionalLight0.SpecularColor = Vector3.Zero;
        effect.DirectionalLight1.Enabled = false;
        effect.DirectionalLight2.Enabled = false;
    }

    // Starsze układy graficzne (OpenGL ES 2) powtarzają tylko tekstury o bokach będących potęgą dwójki.
    static bool Repeats(Texture2D texture) =>
        (texture.Width & (texture.Width - 1)) == 0 && (texture.Height & (texture.Height - 1)) == 0;

    // ---------- Pliki ----------

    /// <summary>Folder bieżącego projektu, np. /sdcard/CsharpGame/projects/MojaGra.</summary>
    public string ProjectFolder { get; private set; } = "";

    /// <summary>
    /// Pełna ścieżka pliku w folderze projektu – do zapisu map, rekordów itp.
    /// Przykład: File.WriteAllText(ProjectFile("mapa.txt"), tekst);  (wymaga using System.IO;)
    /// </summary>
    public string ProjectFile(string name) => Path.Combine(ProjectFolder, name);

    // ---------- Podłączenie do hosta ----------

    Func<string, Stream> _openImage = null!;
    Func<string, IReadOnlyList<string>> _listImages = null!;
    Func<string, Stream> _openModel = null!;
    TextRenderer _text = null!;

    internal void Attach(GraphicsDevice device, SpriteBatch batch, Texture2D pixel,
        Func<string, Stream> openImage, Func<string, IReadOnlyList<string>> listImages, Func<string, Stream> openModel, byte[] font)
    {
        GraphicsDevice = device;
        Batch = batch;
        Pixel = pixel;
        _openImage = openImage;
        _listImages = listImages;
        _openModel = openModel;
        _text = new TextRenderer(font);
    }

    internal void SetTouches(TouchCollection touches) => Touches = touches;

    internal void SetFps(float fps) => Fps = fps;

    internal void SetProjectFolder(string folder) => ProjectFolder = folder;
}
