using Android.Content.Res;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

namespace GamePlayer;

public record CompletionOption(string Label, string Type, string? Detail);

public record CompletionResult(int From, IReadOnlyList<CompletionOption> Options);

/// <summary>
/// IntelliSense: podpowiedzi z Roslyna (ten sam CompletionService, którego używa Visual Studio).
/// Solution trzyma global usings i pliki projektu. Przy zapytaniu podmieniamy tylko te, które się zmieniły,
/// więc Roslyn nie analizuje całego projektu od nowa.
/// </summary>
public static class CodeCompletion
{
    static readonly SemaphoreSlim Gate = new(1, 1);
    static readonly Dictionary<string, DocumentId> Documents = [];
    static AdhocWorkspace? _workspace;
    static ProjectId? _projectId;
    static Solution? _solution;

    static void Init(AssetManager assets)
    {
        if (_solution != null)
            return;

        _workspace = new AdhocWorkspace(MefHostServices.DefaultHost);
        var project = _workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            "UserGame",
            "UserGame",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            parseOptions: new CSharpParseOptions(LanguageVersion.Latest),
            metadataReferences: ScriptCompiler.GetReferences(assets)));

        _workspace.AddDocument(project.Id, "GlobalUsings.cs", SourceText.From(ScriptCompiler.GlobalUsings));
        _projectId = project.Id;
        _solution = _workspace.CurrentSolution;
    }

    static async Task SyncAsync(IReadOnlyList<SourceFile> sources)
    {
        foreach (var name in Documents.Keys.Except(sources.Select(s => s.Name)).ToList())
        {
            _solution = _solution!.RemoveDocument(Documents[name]);
            Documents.Remove(name);
        }

        foreach (var source in sources)
        {
            var text = SourceText.From(source.Text);
            if (!Documents.TryGetValue(source.Name, out var id))
            {
                id = Documents[source.Name] = DocumentId.CreateNewId(_projectId!);
                _solution = _solution!.AddDocument(id, source.Name, text);
            }
            else if (!(await _solution!.GetDocument(id)!.GetTextAsync()).ContentEquals(text))
            {
                _solution = _solution.WithDocumentText(id, text);
            }
        }
    }

    /// <param name="sources">Pliki projektu, w tym otwarty plik z aktualnym tekstem z edytora.</param>
    /// <param name="file">Nazwa pliku, w którym stoi kursor.</param>
    public static async Task<CompletionResult> GetAsync(IReadOnlyList<SourceFile> sources, string file, int position, AssetManager assets)
    {
        await Gate.WaitAsync();
        try
        {
            Init(assets);
            await SyncAsync(sources);

            var document = _solution!.GetDocument(Documents[file])!;
            var length = (await document.GetTextAsync()).Length;
            var service = CompletionService.GetService(document)!;
            var list = await service.GetCompletionsAsync(document, Math.Clamp(position, 0, length));

            // Pomijamy operatory i rzutowania (np. "-x", "!= y", "(byte)"), na telefonie tylko zaśmiecają listę.
            var options = list.ItemsList
                .Where(item => item.DisplayTextPrefix.Length == 0
                    && item.DisplayText is [var first, ..] && (char.IsLetter(first) || first is '_' or '@'))
                .Select(item => new CompletionOption(item.DisplayText + item.DisplayTextSuffix, KindOf(item.Tags), item.InlineDescription is { Length: > 0 } d ? d : null))
                .ToList();
            return new CompletionResult(list.Span.Start, options);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Pierwsze zapytanie ładuje MEF i analizę semantyczną, więc robimy je w tle zawczasu.</summary>
    public static Task WarmupAsync(AssetManager assets) =>
        GetAsync([new SourceFile("Warmup.cs", "class W : GameScript { void F() { Batch. } }")], "Warmup.cs", 39, assets);

    // Tagi Roslyna -> typy ikon w CodeMirror.
    static string KindOf(IReadOnlyList<string> tags) => tags.FirstOrDefault() switch
    {
        "Method" or "ExtensionMethod" => "method",
        "Property" or "Event" => "property",
        "Field" or "Local" or "Parameter" or "RangeVariable" => "variable",
        "Constant" or "EnumMember" => "constant",
        "Class" or "Structure" or "Record" or "RecordStruct" or "Interface" or "Enum" or "Delegate" or "TypeParameter" => "class",
        "Namespace" => "namespace",
        "Keyword" => "keyword",
        _ => "text",
    };
}
