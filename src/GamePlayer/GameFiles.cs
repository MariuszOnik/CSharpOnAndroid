using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Android.Provider;

namespace GamePlayer;

public record SourceFile(string Name, string Text);

/// <summary>
/// Pliki gier gracza. Docelowo w publicznym folderze /sdcard/CsharpGame (dostępnym przez FTP i menedżer plików):
///   CsharpGame/projects/NazwaGry/*.cs      – kod gry
///   CsharpGame/projects/NazwaGry/images/   – obrazki tej gry
///   CsharpGame/projects/NazwaGry/models/   – modele 3D tej gry (OBJ + MTL + tekstury)
///   CsharpGame/images/                     – obrazki wspólne dla wszystkich gier
///   CsharpGame/models/                     – modele wspólne dla wszystkich gier
///   CsharpGame/projects/NazwaGry/libs/     – biblioteki .dll tej gry
///   CsharpGame/libs/                       – biblioteki .dll wspólne dla wszystkich gier
/// Bez zgody na dostęp do plików używamy prywatnego folderu aplikacji.
/// </summary>
public static class GameFiles
{
    const string Preferences = "game-files";
    const string CurrentProjectKey = "project";
    const string SourcePattern = "*.cs";

    static readonly object Gate = new();

    static readonly string NewProjectCode = """
        class MyGame : GameScript
        {
            public override void Start()
            {
                Background = new Color(20, 24, 40);
            }

            public override void Update(float dt)
            {
            }

            public override void Draw()
            {
                DrawTextCentered("Witaj!", Width / 2, Height / 2, 64, Color.White);
            }
        }

        """;

    public static string PublicRoot => Path.Combine(Android.OS.Environment.ExternalStorageDirectory!.AbsolutePath, "CsharpGame");

    public static bool HasPublicAccess(Context context) =>
        Build.VERSION.SdkInt >= BuildVersionCodes.R
            ? Android.OS.Environment.IsExternalStorageManager
            : context.CheckSelfPermission(Android.Manifest.Permission.WriteExternalStorage) == Permission.Granted;

    public static string Root(Context context)
    {
        var root = HasPublicAccess(context) ? PublicRoot : context.FilesDir!.AbsolutePath;
        Prepare(context, root);
        return root;
    }

    // ---------- Projekty ----------

    public static IReadOnlyList<string> ListProjects(Context context) =>
        Directory.GetDirectories(ProjectsDir(context))
            .Select(path => Path.GetFileName(path)!)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Ostatnio otwarty projekt. Gdy zniknął, bierze pierwszy, a gdy nie ma żadnego – tworzy nowy.</summary>
    public static string CurrentProject(Context context)
    {
        var projects = ListProjects(context);
        var saved = context.GetSharedPreferences(Preferences, FileCreationMode.Private)!.GetString(CurrentProjectKey, null);
        if (saved != null && projects.Contains(saved))
            return saved;
        if (projects.Count > 0)
            return projects[0];
        return CreateProject(context, "MyGame");
    }

    public static void SetCurrentProject(Context context, string project)
    {
        ProjectDir(context, project);
        using var editor = context.GetSharedPreferences(Preferences, FileCreationMode.Private)!.Edit()!;
        editor.PutString(CurrentProjectKey, project)!.Apply();
    }

    public static string CreateProject(Context context, string name)
    {
        name = name.Trim();
        var dir = ProjectDir(context, name);
        if (Directory.Exists(dir))
            throw new IOException($"Projekt '{name}' już istnieje.");

        Directory.CreateDirectory(Path.Combine(dir, "images"));
        Directory.CreateDirectory(Path.Combine(dir, "models"));
        Directory.CreateDirectory(Path.Combine(dir, "libs"));
        File.WriteAllText(Path.Combine(dir, "Game.cs"), NewProjectCode);
        return name;
    }

    public static void DeleteProject(Context context, string name) =>
        Directory.Delete(ProjectDir(context, name), recursive: true);

    /// <summary>Foldery z obrazkami gry, w kolejności szukania.</summary>
    public static string[] ImageDirs(Context context, string project) =>
        [Path.Combine(ProjectDir(context, project), "images"), Path.Combine(Root(context), "images")];

    /// <summary>Foldery z modelami 3D gry (OBJ, MTL, tekstury), w kolejności szukania.</summary>
    public static string[] ModelDirs(Context context, string project) =>
        [Path.Combine(ProjectDir(context, project), "models"), Path.Combine(Root(context), "models")];

    /// <summary>Foldery z bibliotekami .dll gry. Przy tej samej nazwie wygrywa biblioteka projektu.</summary>
    public static string[] LibDirs(Context context, string project) =>
        [Path.Combine(ProjectDir(context, project), "libs"), Path.Combine(Root(context), "libs")];

    // ---------- Pliki projektu ----------

    /// <summary>Wszystkie pliki .cs projektu. Końce linii jak w edytorze, żeby pozycje błędów się zgadzały.</summary>
    public static List<SourceFile> ReadSources(Context context, string project)
    {
        var dir = ProjectDir(context, project);
        if (Directory.GetFiles(dir, SourcePattern).Length == 0)
            File.WriteAllText(Path.Combine(dir, "Game.cs"), NewProjectCode);

        return Directory.GetFiles(dir, SourcePattern)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(path => new SourceFile(Path.GetFileName(path), File.ReadAllText(path).ReplaceLineEndings("\n")))
            .ToList();
    }

    public static string SaveFile(Context context, string project, string name, string text)
    {
        var path = FilePath(context, project, name);
        File.WriteAllText(path, text);
        return path;
    }

    public static string CreateFile(Context context, string project, string name)
    {
        name = SourceName(name);
        var path = FilePath(context, project, name);
        if (File.Exists(path))
            throw new IOException($"Plik '{name}' już istnieje.");

        var className = Path.GetFileNameWithoutExtension(name);
        var isIdentifier = className.All(c => char.IsLetterOrDigit(c) || c == '_') && !char.IsDigit(className[0]);
        File.WriteAllText(path, isIdentifier ? $"class {className}\n{{\n}}\n" : "");
        return name;
    }

    public static string RenameFile(Context context, string project, string oldName, string newName)
    {
        newName = SourceName(newName);
        var target = FilePath(context, project, newName);
        if (File.Exists(target))
            throw new IOException($"Plik '{newName}' już istnieje.");

        File.Move(FilePath(context, project, oldName), target);
        return newName;
    }

    public static void DeleteFile(Context context, string project, string name)
    {
        if (Directory.GetFiles(ProjectDir(context, project), SourcePattern).Length <= 1)
            throw new IOException("Projekt musi mieć przynajmniej jeden plik.");

        File.Delete(FilePath(context, project, name));
    }

    static string ProjectsDir(Context context) => Path.Combine(Root(context), "projects");

    static string ProjectDir(Context context, string project) => Path.Combine(ProjectsDir(context), CheckName(project));

    static string FilePath(Context context, string project, string name) =>
        Path.Combine(ProjectDir(context, project), CheckName(name));

    static string SourceName(string name)
    {
        name = name.Trim();
        return name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? name : name + ".cs";
    }

    // Nazwy przychodzą z edytora i stają się ścieżkami: nie mogą wyjść poza folder.
    static string CheckName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('.') || name.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0)
            throw new ArgumentException($"Niedozwolona nazwa: '{name}'.");
        return name;
    }

    // ---------- Pierwsze uruchomienie w danym folderze ----------

    static void Prepare(Context context, string root)
    {
        lock (Gate)
        {
            var projects = Path.Combine(root, "projects");
            Directory.CreateDirectory(projects);
            Directory.CreateDirectory(Path.Combine(root, "images"));
            Directory.CreateDirectory(Path.Combine(root, "models"));
            Directory.CreateDirectory(Path.Combine(root, "libs"));

            var preferences = context.GetSharedPreferences(Preferences, FileCreationMode.Private)!;
            // Przykłady sprawdzamy przy każdym starcie, żeby nowe wersje aplikacji dokładały nowe przykłady.
            InstallSamples(context.Assets!, projects, preferences, root);

            var preparedKey = "prepared:" + root;
            if (preferences.GetBoolean(preparedKey, false))
                return;

            var privateRoot = context.FilesDir!.AbsolutePath;
            ImportOldLayout(root, privateRoot, projects);

            // Projekty pisane przed udzieleniem zgody przechodzą do folderu publicznego.
            var privateProjects = Path.Combine(privateRoot, "projects");
            if (root != privateRoot && Directory.Exists(privateProjects))
                foreach (var dir in Directory.GetDirectories(privateProjects))
                    CopyDirectory(dir, Path.Combine(projects, Path.GetFileName(dir)!));

            using var editor = preferences.Edit()!;
            editor.PutBoolean(preparedKey, true)!.Apply();
        }
    }

    // Stare układy kopiujemy, a nie przenosimy: oryginały zostają na miejscu.
    static void ImportOldLayout(string root, string privateRoot, string projects)
    {
        var oldGame = new[] { root, privateRoot }.Select(dir => Path.Combine(dir, "Game.cs")).FirstOrDefault(File.Exists);
        var imported = Path.Combine(projects, "Imported");
        if (oldGame != null && !Directory.Exists(imported))
        {
            Directory.CreateDirectory(Path.Combine(imported, "images"));
            File.Copy(oldGame, Path.Combine(imported, "Game.cs"));
        }

        // Folder z edytora uruchamianego wewnątrz gry.
        var oldProject = Path.Combine(root, "projekt");
        if (Directory.Exists(oldProject))
            CopyDirectory(oldProject, Path.Combine(projects, "projekt"));
    }

    static void CopyDirectory(string source, string target)
    {
        if (Directory.Exists(target))
            return;

        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)!));
    }

    // Każdy przykład kopiujemy raz: usunięty przez gracza nie wraca, istniejący nie jest nadpisywany.
    static void InstallSamples(AssetManager assets, string projects, ISharedPreferences preferences, string root)
    {
        using var editor = preferences.Edit()!;
        foreach (var sample in assets.List("samples") ?? [])
        {
            var key = "sample:" + root + ":" + sample;
            if (preferences.GetBoolean(key, false))
                continue;

            var target = Path.Combine(projects, sample);
            if (!Directory.Exists(target))
                CopyAssets(assets, "samples/" + sample, target);
            editor.PutBoolean(key, true);
        }
        editor.Apply();
    }

    /// <summary>Folder projektu (kod, obrazki, pliki zapisywane przez grę).</summary>
    public static string ProjectFolder(Context context, string project) => ProjectDir(context, project);

    static void CopyAssets(AssetManager assets, string path, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var name in assets.List(path) ?? [])
        {
            var child = path + "/" + name;
            // Foldery w Assets to te, które mają zawartość.
            if (assets.List(child) is { Length: > 0 })
            {
                CopyAssets(assets, child, Path.Combine(target, name));
                continue;
            }

            using var input = assets.Open(child);
            using var output = File.Create(Path.Combine(target, name));
            input.CopyTo(output);
        }
    }

    // ---------- Zgoda na dostęp ----------

    public static void RequestPublicAccess(Activity activity)
    {
        if (HasPublicAccess(activity))
            return;

        new AlertDialog.Builder(activity)
            .SetTitle("Dostęp do plików")
            .SetMessage("Kod i obrazki gry będą w folderze CsharpGame w pamięci telefonu, żebyś mógł je wgrywać np. przez FTP.\n\nWłącz „Zezwalaj na dostęp do wszystkich plików”.")
            .SetPositiveButton("Ustawienia", (_, _) =>
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
                    activity.StartActivity(new Intent(Settings.ActionManageAppAllFilesAccessPermission, Android.Net.Uri.Parse("package:" + activity.PackageName)));
                else
                    activity.RequestPermissions([Android.Manifest.Permission.WriteExternalStorage, Android.Manifest.Permission.ReadExternalStorage], 1);
            })
            .SetNegativeButton("Nie teraz", (_, _) => { })
            .Show();
    }
}
