// ContentManager gry: pliki .xnb z folderów content/ (projekt, potem wspólny CsharpGame/content, potem wbudowane w aplikację).
using System;
using System.IO;
using Microsoft.Xna.Framework.Content;

namespace GameApi;

sealed class ScriptContent : ContentManager
{
    readonly Func<string, Stream> _open;

    public ScriptContent(IServiceProvider services, Func<string, Stream> open) : base(services, "") => _open = open;

    protected override Stream OpenStream(string assetName) => _open(assetName.Replace('\\', '/') + ".xnb");
}
