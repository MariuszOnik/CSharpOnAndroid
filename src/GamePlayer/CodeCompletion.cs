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
/// Workspace trzyma projekt z global usings i jednym dokumentem Game.cs, którego tekst podmieniamy przy każdym zapytaniu.
/// </summary>
public static class CodeCompletion
{
    static readonly SemaphoreSlim Gate = new(1, 1);
    static AdhocWorkspace? _workspace;
    static DocumentId? _documentId;

    static DocumentId Init(AssetManager assets)
    {
        if (_documentId != null)
            return _documentId;

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
        return _documentId = _workspace.AddDocument(project.Id, "Game.cs", SourceText.From("")).Id;
    }

    public static async Task<CompletionResult> GetAsync(string code, int position, AssetManager assets)
    {
        await Gate.WaitAsync();
        try
        {
            var documentId = Init(assets);
            var document = _workspace!.CurrentSolution.GetDocument(documentId)!.WithText(SourceText.From(code));
            var service = CompletionService.GetService(document)!;
            var list = await service.GetCompletionsAsync(document, Math.Clamp(position, 0, code.Length));

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
        GetAsync("class W : GameScript { void F() { Batch. } }", 39, assets);

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
