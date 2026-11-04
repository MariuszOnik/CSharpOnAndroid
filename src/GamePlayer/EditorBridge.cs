using System.Text.Json;
using Android.Webkit;
using Java.Interop;

namespace GamePlayer;

/// <summary>Obiekt widoczny w JavaScripcie jako window.Android.</summary>
class EditorBridge(EditorActivity activity) : Java.Lang.Object
{
    record ProjectState(string Project, IReadOnlyList<string> Projects, IReadOnlyList<SourceFile> Files);

    record ActionResult(string? Name, string? Error);

    /// <summary>Bieżący projekt z treścią wszystkich plików.</summary>
    [JavascriptInterface]
    [Export("state")]
    public string State()
    {
        var project = GameFiles.CurrentProject(activity);
        var state = new ProjectState(project, GameFiles.ListProjects(activity), GameFiles.ReadSources(activity, project));
        return JsonSerializer.Serialize(state, EditorActivity.JsonOptions);
    }

    [JavascriptInterface]
    [Export("openProject")]
    public string OpenProject(string name)
    {
        GameFiles.SetCurrentProject(activity, name);
        return State();
    }

    [JavascriptInterface]
    [Export("createProject")]
    public string CreateProject(string name) => Run(() => GameFiles.CreateProject(activity, name));

    [JavascriptInterface]
    [Export("deleteProject")]
    public string DeleteProject(string name) => Run(() =>
    {
        GameFiles.DeleteProject(activity, name);
        return name;
    });

    [JavascriptInterface]
    [Export("createFile")]
    public string CreateFile(string project, string name) => Run(() => GameFiles.CreateFile(activity, project, name));

    [JavascriptInterface]
    [Export("renameFile")]
    public string RenameFile(string project, string oldName, string newName) =>
        Run(() => GameFiles.RenameFile(activity, project, oldName, newName));

    [JavascriptInterface]
    [Export("deleteFile")]
    public string DeleteFile(string project, string name) => Run(() =>
    {
        GameFiles.DeleteFile(activity, project, name);
        return name;
    });

    /// <summary>Zwraca ścieżkę zapisanego pliku albo komunikat błędu.</summary>
    [JavascriptInterface]
    [Export("save")]
    public string Save(string project, string file, string code)
    {
        try
        {
            var path = GameFiles.SaveFile(activity, project, file, code);
            return "Zapisano: " + path.Replace(Android.OS.Environment.ExternalStorageDirectory!.AbsolutePath, "/sdcard");
        }
        catch (Exception e)
        {
            return "Błąd zapisu: " + e.Message;
        }
    }

    // Wywoływane na wątku mostka JavaScript, więc kompilacja nie blokuje UI.
    // Edytor zapisuje pliki przed wywołaniem, a kompilator czyta cały projekt z dysku.
    [JavascriptInterface]
    [Export("compile")]
    public void Compile(string project)
    {
        CompileResult result;
        try
        {
            result = ScriptCompiler.Compile(GameFiles.ReadSources(activity, project), activity.Assets!);
        }
        catch (Exception e)
        {
            result = new CompileResult(false, 0, e.ToString(), []);
        }

        activity.SendResult(result);
        if (result.ScriptType != null)
            activity.StartGame(result.ScriptType, project);
    }

    /// <summary>Podpowiedzi liczone w tle; wynik wraca przez window.onCompletions(id, wynik).</summary>
    [JavascriptInterface]
    [Export("complete")]
    public void Complete(int id, string project, string file, string code, int position)
    {
        var assets = activity.Assets!;
        Task.Run(async () =>
        {
            string json;
            try
            {
                var sources = GameFiles.ReadSources(activity, project).Where(s => s.Name != file).Append(new SourceFile(file, code)).ToList();
                json = JsonSerializer.Serialize(await CodeCompletion.GetAsync(sources, file, position, assets), EditorActivity.JsonOptions);
            }
            catch (Exception e)
            {
                Android.Util.Log.Error("GamePlayer", "Completion: " + e);
                json = "null";
            }
            activity.RunJs($"window.onCompletions({id},{json})");
        });
    }

    static string Run(Func<string> action)
    {
        ActionResult result;
        try
        {
            result = new ActionResult(action(), null);
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
        {
            result = new ActionResult(null, e.Message);
        }
        return JsonSerializer.Serialize(result, EditorActivity.JsonOptions);
    }
}
