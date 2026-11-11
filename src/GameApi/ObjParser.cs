using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameApi;

/// <summary>Materiał z pliku .mtl: kolor (Kd), przezroczystość (d) i nazwa pliku tekstury (map_Kd).</summary>
internal sealed class ObjMaterial
{
    public Vector3 Diffuse = Vector3.One;
    public float Alpha = 1f;
    public string? Texture;
}

/// <summary>Trójkąty jednego obiektu (o/g) w jednym materiale – gotowe do wrzucenia do bufora wierzchołków.</summary>
internal sealed class ObjGroup
{
    public string Object = "";
    public string Material = "";
    public List<VertexPositionNormalTexture> Vertices = new();
}

/// <summary>
/// Czyta pliki Wavefront OBJ i MTL. Sam parser, bez karty graficznej.
/// Obsługuje v/vt/vn, wielokąty (cięte w wachlarz), indeksy ujemne, obiekty (o, g) i materiały (usemtl).
/// </summary>
internal static class ObjParser
{
    static readonly char[] Space = { ' ', '\t' };

    public static List<ObjGroup> Parse(TextReader reader, List<string> materialLibraries)
    {
        var positions = new List<Vector3>();
        var texCoords = new List<Vector2>();
        var normals = new List<Vector3>();
        var groups = new List<ObjGroup>();
        var byKey = new Dictionary<(string, string), ObjGroup>();
        string currentObject = "", currentMaterial = "";
        ObjGroup? current = null;
        var corners = new List<VertexPositionNormalTexture>();
        var hasNormal = new List<bool>();

        int lineNumber = 0;
        for (string? line = reader.ReadLine(); line != null; line = reader.ReadLine())
        {
            lineNumber++;
            int comment = line.IndexOf('#');
            if (comment >= 0)
                line = line.Substring(0, comment);
            var parts = line.Split(Space, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;

            try
            {
                switch (parts[0])
                {
                    case "v":
                        positions.Add(new Vector3(Number(parts[1]), Number(parts[2]), Number(parts[3])));
                        break;
                    case "vt":
                        // OBJ ma v = 0 na dole obrazka, MonoGame – na górze.
                        texCoords.Add(new Vector2(Number(parts[1]), parts.Length > 2 ? 1f - Number(parts[2]) : 0f));
                        break;
                    case "vn":
                        normals.Add(new Vector3(Number(parts[1]), Number(parts[2]), Number(parts[3])));
                        break;
                    case "o":
                    case "g":
                        currentObject = parts.Length > 1 ? parts[1] : "";
                        current = null;
                        break;
                    case "usemtl":
                        currentMaterial = parts.Length > 1 ? parts[1] : "";
                        current = null;
                        break;
                    case "mtllib":
                        materialLibraries.Add(line.Trim().Substring("mtllib".Length).Trim());
                        break;
                    case "f":
                        if (parts.Length < 4)
                            break;
                        if (current == null && !byKey.TryGetValue((currentObject, currentMaterial), out current))
                        {
                            current = new ObjGroup { Object = currentObject, Material = currentMaterial };
                            byKey[(currentObject, currentMaterial)] = current;
                            groups.Add(current);
                        }

                        corners.Clear();
                        hasNormal.Clear();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            var refs = parts[i].Split('/');
                            var corner = new VertexPositionNormalTexture
                            {
                                Position = positions[Index(refs[0], positions.Count)],
                            };
                            if (refs.Length > 1 && refs[1].Length > 0)
                                corner.TextureCoordinate = texCoords[Index(refs[1], texCoords.Count)];
                            bool normal = refs.Length > 2 && refs[2].Length > 0;
                            if (normal)
                                corner.Normal = normals[Index(refs[2], normals.Count)];
                            corners.Add(corner);
                            hasNormal.Add(normal);
                        }

                        for (int i = 1; i + 1 < corners.Count; i++)
                            AddTriangle(current.Vertices, corners, hasNormal, 0, i, i + 1);
                        break;
                }
            }
            catch (Exception e) when (e is FormatException || e is IndexOutOfRangeException || e is ArgumentOutOfRangeException)
            {
                throw new InvalidDataException($"Błąd w pliku OBJ, linia {lineNumber}: {line.Trim()}");
            }
        }

        groups.RemoveAll(group => group.Vertices.Count == 0);
        return groups;
    }

    // OBJ podaje ściany przeciwnie do ruchu wskazówek zegara, a MonoGame domyślnie rysuje te zgodne z ruchem –
    // dlatego dwa ostatnie wierzchołki zamieniamy miejscami. Bez normalnych w pliku liczymy płaską normalną ściany.
    static void AddTriangle(List<VertexPositionNormalTexture> target, List<VertexPositionNormalTexture> corners, List<bool> hasNormal, int a, int b, int c)
    {
        var va = corners[a];
        var vb = corners[b];
        var vc = corners[c];
        if (!hasNormal[a] || !hasNormal[b] || !hasNormal[c])
        {
            var face = Vector3.Cross(vb.Position - va.Position, vc.Position - va.Position);
            if (face.LengthSquared() > 0)
                face.Normalize();
            if (!hasNormal[a]) va.Normal = face;
            if (!hasNormal[b]) vb.Normal = face;
            if (!hasNormal[c]) vc.Normal = face;
        }

        target.Add(va);
        target.Add(vc);
        target.Add(vb);
    }

    public static Dictionary<string, ObjMaterial> ParseMaterials(TextReader reader)
    {
        var materials = new Dictionary<string, ObjMaterial>();
        ObjMaterial? current = null;
        for (string? line = reader.ReadLine(); line != null; line = reader.ReadLine())
        {
            int comment = line.IndexOf('#');
            if (comment >= 0)
                line = line.Substring(0, comment);
            var parts = line.Split(Space, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                continue;

            if (parts[0] == "newmtl")
            {
                current = new ObjMaterial();
                materials[parts[1]] = current;
            }
            else if (current == null)
            {
                continue;
            }
            else if (parts[0] == "Kd" && parts.Length >= 4)
            {
                current.Diffuse = new Vector3(Number(parts[1]), Number(parts[2]), Number(parts[3]));
            }
            else if (parts[0] == "d")
            {
                current.Alpha = Number(parts[1]);
            }
            else if (parts[0] == "map_Kd")
            {
                // Ostatni człon: przed nazwą pliku mogą stać opcje (-s 1 1 1 itp.).
                current.Texture = parts[parts.Length - 1];
            }
        }
        return materials;
    }

    static float Number(string text) => float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

    // Indeksy w OBJ liczą się od 1, a ujemne – od końca dotychczasowej listy.
    static int Index(string text, int count)
    {
        int index = int.Parse(text, CultureInfo.InvariantCulture);
        return index > 0 ? index - 1 : count + index;
    }
}
