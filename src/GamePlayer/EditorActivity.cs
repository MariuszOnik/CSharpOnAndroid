using System.Text.Json;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using Java.Interop;

namespace GamePlayer;

[Activity(
    Label = "@string/app_name",
    MainLauncher = true,
    Icon = "@drawable/icon",
    Theme = "@android:style/Theme.Material.NoActionBar",
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize
)]
public class EditorActivity : Activity
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    WebView _webView = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _webView = new WebView(this);
        _webView.Settings.JavaScriptEnabled = true;
        _webView.AddJavascriptInterface(new EditorBridge(this), "Android");
        _webView.LoadUrl("file:///android_asset/editor/index.html");

        // Od Androida 15 aplikacja rysuje się pod paskami systemowymi i klawiaturą,
        // więc sami odsuwamy edytor o ich wysokość.
        var root = new FrameLayout(this);
        root.SetBackgroundColor(Android.Graphics.Color.ParseColor("#21252b"));
        root.AddView(_webView);
        root.SetOnApplyWindowInsetsListener(new SystemBarsPadding());
        SetContentView(root);

        GameFiles.RequestPublicAccess(this);

        var assets = Assets!;
        Task.Run(async () =>
        {
            ScriptCompiler.Warmup(assets);
            await CodeCompletion.WarmupAsync(assets);
        });
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (GameHost.TakeRuntimeError() is { } error)
            SendResult(new CompileResult(false, 0, "Błąd w trakcie gry: " + error, []));
    }

    internal void RunJs(string script) => RunOnUiThread(() => _webView.EvaluateJavascript(script, null));

    internal void SendResult(CompileResult result) =>
        RunJs($"window.onCompileResult({JsonSerializer.Serialize(result, JsonOptions)})");

    internal void StartGame(Type scriptType)
    {
        GameHost.PendingScriptType = scriptType;
        RunOnUiThread(() => StartActivity(new Intent(this, typeof(GameActivity))));
    }
}

class SystemBarsPadding : Java.Lang.Object, View.IOnApplyWindowInsetsListener
{
    public WindowInsets OnApplyWindowInsets(View view, WindowInsets insets)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            var bars = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.Ime());
            view.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
        }
        return insets;
    }
}

/// <summary>Obiekt widoczny w JavaScripcie jako window.Android.</summary>
class EditorBridge(EditorActivity activity) : Java.Lang.Object
{
    [JavascriptInterface]
    [Export("loadCode")]
    public string LoadCode()
    {
        var path = GameFiles.CodePath(activity);
        if (File.Exists(path))
            return File.ReadAllText(path);

        using var reader = new StreamReader(activity.Assets!.Open("sample/Game.cs"));
        return reader.ReadToEnd();
    }

    /// <summary>Zwraca ścieżkę zapisanego pliku albo komunikat błędu.</summary>
    [JavascriptInterface]
    [Export("save")]
    public string Save(string code)
    {
        try
        {
            var path = GameFiles.CodePath(activity);
            File.WriteAllText(path, code);
            return "Zapisano: " + path.Replace(Android.OS.Environment.ExternalStorageDirectory!.AbsolutePath, "/sdcard");
        }
        catch (Exception e)
        {
            return "Błąd zapisu: " + e.Message;
        }
    }

    // Wywoływane na wątku mostka JavaScript, więc kompilacja nie blokuje UI.
    [JavascriptInterface]
    [Export("compile")]
    public void Compile(string code)
    {
        Save(code);
        CompileResult result;
        try
        {
            result = ScriptCompiler.Compile(code, activity.Assets!);
        }
        catch (Exception e)
        {
            result = new CompileResult(false, 0, e.ToString(), []);
        }

        activity.SendResult(result);
        if (result.ScriptType != null)
            activity.StartGame(result.ScriptType);
    }

    /// <summary>Podpowiedzi liczone w tle; wynik wraca przez window.onCompletions(id, wynik).</summary>
    [JavascriptInterface]
    [Export("complete")]
    public void Complete(int id, string code, int position)
    {
        var assets = activity.Assets!;
        Task.Run(async () =>
        {
            string json;
            try
            {
                json = JsonSerializer.Serialize(await CodeCompletion.GetAsync(code, position, assets), EditorActivity.JsonOptions);
            }
            catch (Exception e)
            {
                Android.Util.Log.Error("GamePlayer", "Completion: " + e);
                json = "null";
            }
            activity.RunJs($"window.onCompletions({id},{json})");
        });
    }
}
