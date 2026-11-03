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
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    WebView _webView = null!;

    internal string CodePath => Path.Combine(FilesDir!.AbsolutePath, "Game.cs");

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

        var assets = Assets!;
        Task.Run(() => ScriptCompiler.Warmup(assets));
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (GameHost.TakeRuntimeError() is { } error)
            SendResult(new CompileResult(false, 0, "Błąd w trakcie gry: " + error, []));
    }

    internal void SendResult(CompileResult result)
    {
        var json = JsonSerializer.Serialize(result, JsonOptions);
        RunOnUiThread(() => _webView.EvaluateJavascript($"window.onCompileResult({json})", null));
    }

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
        if (File.Exists(activity.CodePath))
            return File.ReadAllText(activity.CodePath);

        using var reader = new StreamReader(activity.Assets!.Open("sample/Game.cs"));
        return reader.ReadToEnd();
    }

    // Wywoływane na wątku mostka JavaScript, więc kompilacja nie blokuje UI.
    [JavascriptInterface]
    [Export("compile")]
    public void Compile(string code)
    {
        File.WriteAllText(activity.CodePath, code);
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
}
