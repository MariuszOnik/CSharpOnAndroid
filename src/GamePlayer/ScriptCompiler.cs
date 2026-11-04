using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Serialization;
using Android.Content.Res;
using GameApi;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace GamePlayer;

public record ScriptDiagnostic(string File, int From, int To, int Line, string Severity, string Message);

public record CompileResult(bool Ok, long Ms, string? Message, IReadOnlyList<ScriptDiagnostic> Diagnostics)
{
    [JsonIgnore]
    public Type? ScriptType { get; init; }
}

/// <summary>
/// Kompiluje kod gry Roslynem na telefonie. Referencje (assembly .NET, MonoGame, GameApi)
/// leżą w Assets/refs, bo na Androidzie assembly aplikacji nie są zwykłymi plikami na dysku.
/// </summary>
public static class ScriptCompiler
{
    public const string GlobalUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.Linq;
        global using Microsoft.Xna.Framework;
        global using Microsoft.Xna.Framework.Graphics;
        global using Microsoft.Xna.Framework.Input.Touch;
        global using GameApi;
        """;

    static readonly object Gate = new();
    static List<MetadataReference>? _references;
    static int _counter;

    public static List<MetadataReference> GetReferences(AssetManager assets)
    {
        lock (Gate)
            return _references ??= LoadReferences(assets);
    }

    static List<MetadataReference> LoadReferences(AssetManager assets)
    {

        var references = new List<MetadataReference>();
        foreach (var name in assets.List("refs") ?? [])
        {
            using var stream = assets.Open("refs/" + name);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            references.Add(MetadataReference.CreateFromImage(memory.ToArray(), filePath: name));
        }
        return references;
    }

    /// <summary>Pierwsza kompilacja jest wolna (JIT Roslyna), więc robimy ją w tle zawczasu.</summary>
    public static void Warmup(AssetManager assets) =>
        Compile([new SourceFile("Warmup.cs", "class Warmup : GameScript { public override void Update(float dt) { } }")], assets, load: false);

    public static CompileResult Compile(IReadOnlyList<SourceFile> sources, AssetManager assets, bool load = true)
    {
        lock (Gate)
        {
            var stopwatch = Stopwatch.StartNew();
            var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
            var userTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s.Text, parseOptions, path: s.Name)).ToList();
            var usingsTree = CSharpSyntaxTree.ParseText(GlobalUsings, parseOptions, path: "GlobalUsings.cs");

            var compilation = CSharpCompilation.Create(
                $"UserGame{++_counter}",
                [usingsTree, .. userTrees],
                GetReferences(assets),
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    nullableContextOptions: NullableContextOptions.Disable));

            using var output = new MemoryStream();
            var emit = compilation.Emit(output);

            var diagnostics = emit.Diagnostics
                .Where(d => d.Severity >= DiagnosticSeverity.Warning && d.Location.SourceTree is { } tree && tree != usingsTree)
                .OrderByDescending(d => d.Severity)
                .Select(d =>
                {
                    var span = d.Location.SourceSpan;
                    return new ScriptDiagnostic(
                        d.Location.SourceTree!.FilePath,
                        span.Start,
                        span.End,
                        d.Location.GetLineSpan().StartLinePosition.Line + 1,
                        d.Severity == DiagnosticSeverity.Error ? "error" : "warning",
                        $"{d.Id}: {d.GetMessage()}");
                })
                .ToList();

            if (!emit.Success)
            {
                // Błędy spoza kodu użytkownika (np. brak referencji) też muszą być widoczne.
                var other = emit.Diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error && (d.Location.SourceTree == null || d.Location.SourceTree == usingsTree));
                return new CompileResult(false, stopwatch.ElapsedMilliseconds, other?.ToString(), diagnostics);
            }

            if (!load)
                return new CompileResult(true, stopwatch.ElapsedMilliseconds, null, diagnostics);

            var assembly = Assembly.Load(output.ToArray());
            var scriptTypes = assembly.GetTypes().Where(t => typeof(GameScript).IsAssignableFrom(t) && !t.IsAbstract).ToList();
            var message = scriptTypes.Count switch
            {
                0 => "Brak klasy dziedziczącej po GameScript.",
                > 1 => "Więcej niż jedna klasa dziedziczy po GameScript: " + string.Join(", ", scriptTypes.Select(t => t.Name)),
                _ => null,
            };

            return new CompileResult(message == null, stopwatch.ElapsedMilliseconds, message, diagnostics)
            {
                ScriptType = message == null ? scriptTypes[0] : null,
            };
        }
    }
}
