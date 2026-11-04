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

        // Pliki mogły się zmienić: FTP, menedżer plików albo udzielona właśnie zgoda na dostęp.
        RunJs("window.onResumed && window.onResumed()");
    }

    internal void RunJs(string script) => RunOnUiThread(() => _webView.EvaluateJavascript(script, null));

    internal void SendResult(CompileResult result) =>
        RunJs($"window.onCompileResult({JsonSerializer.Serialize(result, JsonOptions)})");

    internal void StartGame(Type scriptType, string project)
    {
        GameHost.PendingScriptType = scriptType;
        GameHost.PendingImageDirs = GameFiles.ImageDirs(this, project);
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

