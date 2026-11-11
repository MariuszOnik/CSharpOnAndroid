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

    // Pomoc w edytorze: Wstecz ma ją zamknąć. Od Androida 13 (a przy targetSdk 36 obowiązkowo od 16)
    // system nie woła OnBackPressed, tylko zarejestrowany OnBackInvokedCallback.
    bool _helpOpen;
    BackCallback? _helpBack;

    internal void SetHelpOpen(bool open) => RunOnUiThread(() =>
    {
        _helpOpen = open;
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
            return;
        if (open && _helpBack == null)
        {
            _helpBack = new BackCallback(CloseHelp);
            OnBackInvokedDispatcher.RegisterOnBackInvokedCallback(0, _helpBack);
        }
        else if (!open && _helpBack != null)
        {
            OnBackInvokedDispatcher.UnregisterOnBackInvokedCallback(_helpBack);
            _helpBack = null;
        }
    });

    void CloseHelp() => RunJs("window.closeHelp && window.closeHelp()");

    [Obsolete("Starsze Androidy; od 13 działa OnBackInvokedCallback")]
    public override void OnBackPressed()
    {
        if (_helpOpen)
            CloseHelp();
        else
            base.OnBackPressed();
    }

    class BackCallback(Action onBack) : Java.Lang.Object, Android.Window.IOnBackInvokedCallback
    {
        public void OnBackInvoked() => onBack();
    }

    internal void StartGame(Type scriptType, string project)
    {
        GameHost.PendingScriptType = scriptType;
        GameHost.PendingImageDirs = GameFiles.ImageDirs(this, project);
        GameHost.PendingModelDirs = GameFiles.ModelDirs(this, project);
        GameHost.PendingContentDirs = GameFiles.ContentDirs(this, project);
        GameHost.PendingProjectFolder = GameFiles.ProjectFolder(this, project);
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

