using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Window;
using Microsoft.Xna.Framework;

namespace GamePlayer;

[Activity(
    Label = "@string/app_name",
    Theme = "@android:style/Theme.Material.NoActionBar.Fullscreen",
    ScreenOrientation = ScreenOrientation.FullUser,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize
)]
public class GameActivity : AndroidGameActivity
{
    GameHost? _game;

    protected override void OnCreate(Bundle? bundle)
    {
        base.OnCreate(bundle);
        HideSystemBars();

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            OnBackInvokedDispatcher.RegisterOnBackInvokedCallback(0, new BackCallback(Finish));

        _game = new GameHost(this);
        SetContentView((View)_game.Services.GetService(typeof(View))!);
        _game.Run();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus)
            HideSystemBars();
    }

    // Bufor gry ma rozmiar całego ekranu. Przy widocznych paskach MonoGame wpasowałby go w mniejszy obszar
    // i po bokach zostałyby pasy (widać to na telefonach z przyciskami nawigacji).
    void HideSystemBars()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
        {
            var attributes = Window!.Attributes!;
            attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
            Window.Attributes = attributes;
        }

        Window!.DecorView.SystemUiVisibility = (StatusBarVisibility)(
            SystemUiFlags.ImmersiveSticky | SystemUiFlags.HideNavigation | SystemUiFlags.Fullscreen
            | SystemUiFlags.LayoutStable | SystemUiFlags.LayoutHideNavigation | SystemUiFlags.LayoutFullscreen);
    }

    protected override void OnDestroy()
    {
        _game?.Dispose();
        _game = null;
        base.OnDestroy();
    }

    class BackCallback(Action onBack) : Java.Lang.Object, IOnBackInvokedCallback
    {
        public void OnBackInvoked() => onBack();
    }
}
