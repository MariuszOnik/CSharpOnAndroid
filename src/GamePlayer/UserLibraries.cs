using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace GamePlayer;

/// <summary>
/// Dodatkowe biblioteki .dll gracza – bez przebudowy aplikacji:
///   CsharpGame/libs/                   – wspólne dla wszystkich gier
///   CsharpGame/projects/NazwaGry/libs/ – tylko dla tej gry (przy tej samej nazwie wygrywa ta)
/// Kompilator i podpowiedzi biorą je jako referencje, a gra ładuje je, gdy pierwszy raz ich potrzebuje.
/// Biblioteka musi być czystym .NET (netstandard2.0 / net6+), bez natywnych .so i bez Windows/WPF.
/// Jej zależności (inne .dll z paczki NuGet) też trzeba wrzucić do libs.
/// </summary>
public static class UserLibraries
{
    record Entry(DateTime Time, long Size, MetadataReference Reference);

    static readonly object Gate = new();
    static readonly EnumerationOptions DllFiles = new() { MatchCasing = MatchCasing.CaseInsensitive };
    static readonly Dictionary<string, Entry> References = new(StringComparer.Ordinal);
    static readonly Dictionary<string, Assembly> Loaded = new(StringComparer.OrdinalIgnoreCase);
    static string[] _runtimeDirs = [];
    static bool _hooked;

    /// <summary>Pliki .dll z folderów. Przy tej samej nazwie wygrywa wcześniejszy folder.</summary>
    public static List<string> Find(IEnumerable<string> dirs)
    {
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in dirs.Where(Directory.Exists))
            foreach (var path in Directory.GetFiles(dir, "*.dll", DllFiles))
                byName.TryAdd(Path.GetFileNameWithoutExtension(path), path);
        return byName.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase).Select(p => p.Value).ToList();
    }

    /// <summary>
    /// Referencje dla Roslyna. Plik czytamy ponownie tylko, gdy się zmienił, więc te same pliki
    /// dają te same obiekty – podpowiedzi poznają po tym, że nic się nie zmieniło.
    /// </summary>
    /// <param name="skipNames">Assembly wbudowane w aplikację (np. MonoGame.Framework) – ich kopii z libs nie bierzemy.</param>
    public static List<MetadataReference> GetReferences(IEnumerable<string> dirs, ISet<string> skipNames)
    {
        lock (Gate)
        {
            var result = new List<MetadataReference>();
            foreach (var path in Find(dirs))
            {
                if (skipNames.Contains(Path.GetFileNameWithoutExtension(path)))
                    continue;
                var info = new FileInfo(path);
                if (!References.TryGetValue(path, out var entry) || entry.Time != info.LastWriteTimeUtc || entry.Size != info.Length)
                {
                    var reference = MetadataReference.CreateFromImage(File.ReadAllBytes(path), filePath: path);
                    References[path] = entry = new Entry(info.LastWriteTimeUtc, info.Length, reference);
                }
                result.Add(entry.Reference);
            }
            return result;
        }
    }

    /// <summary>Skąd ładować biblioteki w czasie gry. Wywołaj przed załadowaniem skompilowanego kodu gracza.</summary>
    public static void UseForRuntime(string[] dirs)
    {
        lock (Gate)
        {
            _runtimeDirs = dirs;
            if (_hooked)
                return;
            _hooked = true;
            // AssemblyResolve, a nie Default.Resolving: kod gracza siedzi we własnym kontekście (Assembly.Load z bajtów),
            // a to zdarzenie dostaje prośby ze wszystkich kontekstów.
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        }
    }

    // .NET pyta tu o assembly, którego nie ma w aplikacji – np. gdy kod gracza pierwszy raz użyje typu z biblioteki.
    static Assembly? Resolve(object? sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name).Name;
        if (name == null)
            return null;

        lock (Gate)
        {
            if (Loaded.TryGetValue(name, out var loaded))
                return loaded;

            var path = Find(_runtimeDirs).FirstOrDefault(p =>
                string.Equals(Path.GetFileNameWithoutExtension(p), name, StringComparison.OrdinalIgnoreCase));
            if (path == null)
                return null;

            // Z pamięci, a nie z pliku: plik nie jest blokowany i można go podmienić przez FTP.
            // Załadowanej biblioteki nie da się wyładować – nowa wersja zadziała po ponownym uruchomieniu aplikacji.
            using var stream = new MemoryStream(File.ReadAllBytes(path));
            var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
            Loaded[name] = assembly;
            Android.Util.Log.Info("GamePlayer", $"Biblioteka gracza: {path}");
            return assembly;
        }
    }
}
