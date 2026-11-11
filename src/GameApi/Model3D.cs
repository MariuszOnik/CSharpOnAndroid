using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameApi;

/// <summary>Trójkąty jednej części modelu w jednym materiale (kolor albo tekstura).</summary>
public class Surface3D
{
    public VertexBuffer Buffer { get; }
    public int TriangleCount { get; }

    /// <summary>Kolor materiału (Kd z pliku .mtl). Mnożony przez teksturę, jeśli jest.</summary>
    public Color Color { get; set; }

    /// <summary>Tekstura (map_Kd z pliku .mtl) albo null.</summary>
    public Texture2D? Texture { get; set; }

    internal Surface3D(VertexBuffer buffer, int triangleCount, Color color, Texture2D? texture)
    {
        Buffer = buffer;
        TriangleCount = triangleCount;
        Color = color;
        Texture = texture;
    }
}

/// <summary>
/// Nazwana część modelu (obiekt „o” z pliku OBJ, np. śmigło, koło, wieżyczka).
/// Można ją ruszać niezależnie od reszty modelu – tak robi się proste animacje.
/// </summary>
public class Part3D
{
    public string Name { get; }
    public IReadOnlyList<Surface3D> Surfaces { get; }

    /// <summary>Pudełko otaczające część (we współrzędnych modelu).</summary>
    public BoundingBox Bounds { get; }

    /// <summary>Środek części.</summary>
    public Vector3 Center => (Bounds.Min + Bounds.Max) / 2;

    /// <summary>Punkt, wokół którego SetRotation obraca część. Domyślnie jej środek.</summary>
    public Vector3 Pivot { get; set; }

    /// <summary>Własne przekształcenie części, stosowane przed przekształceniem całego modelu.</summary>
    public Matrix Transform { get; set; } = Matrix.Identity;

    public bool Visible { get; set; } = true;

    internal Part3D(string name, IReadOnlyList<Surface3D> surfaces, BoundingBox bounds)
    {
        Name = name;
        Surfaces = surfaces;
        Bounds = bounds;
        Pivot = Center;
    }

    /// <summary>
    /// Obraca część wokół osi przechodzącej przez Pivot. Kąt w radianach.
    /// Przykład: wiatrak.Part("smigla").SetRotation(Vector3.UnitZ, czas * 2);
    /// </summary>
    public void SetRotation(Vector3 axis, float angle) =>
        Transform = Matrix.CreateTranslation(-Pivot) * Matrix.CreateFromAxisAngle(Vector3.Normalize(axis), angle) * Matrix.CreateTranslation(Pivot);

    /// <summary>Przesuwa część względem jej miejsca w modelu.</summary>
    public void SetOffset(Vector3 offset) => Transform = Matrix.CreateTranslation(offset);
}

/// <summary>Model 3D wczytany z pliku OBJ przez LoadModel. Rysuje go DrawModel.</summary>
public class Model3D
{
    public IReadOnlyList<Part3D> Parts { get; }

    /// <summary>Pudełko otaczające cały model.</summary>
    public BoundingBox Bounds { get; }

    public Vector3 Center => (Bounds.Min + Bounds.Max) / 2;
    public Vector3 Size => Bounds.Max - Bounds.Min;

    public int TriangleCount { get; }

    Model3D(IReadOnlyList<Part3D> parts, BoundingBox bounds, int triangleCount)
    {
        Parts = parts;
        Bounds = bounds;
        TriangleCount = triangleCount;
    }

    /// <summary>Część o podanej nazwie (obiekt „o” z pliku OBJ). Wielkość liter nie ma znaczenia.</summary>
    public Part3D Part(string name)
    {
        foreach (var part in Parts)
            if (string.Equals(part.Name, name, StringComparison.OrdinalIgnoreCase))
                return part;

        var names = new List<string>();
        foreach (var part in Parts)
            names.Add(part.Name);
        throw new ArgumentException($"Model nie ma części '{name}'. Dostępne: {string.Join(", ", names)}.");
    }

    public bool HasPart(string name)
    {
        foreach (var part in Parts)
            if (string.Equals(part.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    internal static Model3D Load(GraphicsDevice device, string name, Func<string, Stream> openModel, Func<string, Stream> openImage)
    {
        var libraries = new List<string>();
        List<ObjGroup> groups;
        using (var reader = new StreamReader(openModel(name)))
            groups = ObjParser.Parse(reader, libraries);
        if (groups.Count == 0)
            throw new InvalidDataException($"Model '{name}' nie ma żadnych ścian.");

        // Plik .mtl i tekstury leżą obok modelu. Brak .mtl to nie błąd: model będzie biały.
        string folder = Path.GetDirectoryName(name)?.Replace('\\', '/') ?? "";
        string Beside(string file) => folder.Length > 0 ? folder + "/" + file : file;

        var materials = new Dictionary<string, ObjMaterial>();
        foreach (var library in libraries)
        {
            try
            {
                using var reader = new StreamReader(openModel(Beside(library)));
                foreach (var pair in ObjParser.ParseMaterials(reader))
                    materials[pair.Key] = pair.Value;
            }
            catch (FileNotFoundException)
            {
            }
        }

        var textures = new Dictionary<string, Texture2D>();
        Texture2D LoadTexture(string file)
        {
            if (textures.TryGetValue(file, out var texture))
                return texture;

            Stream stream;
            try
            {
                stream = openModel(Beside(file));
            }
            catch (FileNotFoundException)
            {
                // Tekstura może też leżeć w images/, razem z resztą obrazków gry.
                try
                {
                    stream = openImage(Path.GetFileName(file));
                }
                catch (FileNotFoundException)
                {
                    throw new FileNotFoundException($"Model '{name}': nie ma tekstury '{file}' (szukam obok modelu i w images/).");
                }
            }

            using (stream)
                texture = Texture2D.FromStream(device, stream, DefaultColorProcessors.PremultiplyAlpha);
            textures[file] = texture;
            return texture;
        }

        var partNames = new List<string>();
        var surfacesByPart = new Dictionary<string, List<Surface3D>>();
        var pointsByPart = new Dictionary<string, List<Vector3>>();
        var allPoints = new List<Vector3>();
        int triangles = 0;

        foreach (var group in groups)
        {
            if (!surfacesByPart.TryGetValue(group.Object, out var surfaces))
            {
                surfaces = new List<Surface3D>();
                surfacesByPart[group.Object] = surfaces;
                pointsByPart[group.Object] = new List<Vector3>();
                partNames.Add(group.Object);
            }

            var vertices = group.Vertices.ToArray();
            var buffer = new VertexBuffer(device, VertexPositionNormalTexture.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            buffer.SetData(vertices);

            materials.TryGetValue(group.Material, out var material);
            material ??= new ObjMaterial();
            var color = new Color(new Vector4(material.Diffuse, MathHelper.Clamp(material.Alpha, 0f, 1f)));
            var texture = material.Texture != null ? LoadTexture(material.Texture) : null;
            surfaces.Add(new Surface3D(buffer, vertices.Length / 3, color, texture));
            triangles += vertices.Length / 3;

            foreach (var vertex in vertices)
            {
                pointsByPart[group.Object].Add(vertex.Position);
                allPoints.Add(vertex.Position);
            }
        }

        var parts = new List<Part3D>();
        foreach (var partName in partNames)
            parts.Add(new Part3D(partName, surfacesByPart[partName], BoundingBox.CreateFromPoints(pointsByPart[partName])));
        return new Model3D(parts, BoundingBox.CreateFromPoints(allPoints), triangles);
    }
}
