using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;

namespace GamePlayer;

/// <summary>
/// Pliki gry gracza. Docelowo w publicznym folderze /sdcard/CsharpGame (dostępnym przez FTP i menedżer plików):
///   CsharpGame/Game.cs      – kod gry
///   CsharpGame/images/*.png – obrazki dla LoadImage
/// Bez zgody na dostęp do plików używamy prywatnego folderu aplikacji.
/// </summary>
public static class GameFiles
{
    public static string PublicRoot => Path.Combine(Android.OS.Environment.ExternalStorageDirectory!.AbsolutePath, "CsharpGame");

    public static bool HasPublicAccess(Context context) =>
        Build.VERSION.SdkInt >= BuildVersionCodes.R
            ? Android.OS.Environment.IsExternalStorageManager
            : context.CheckSelfPermission(Android.Manifest.Permission.WriteExternalStorage) == Permission.Granted;

    public static string Root(Context context)
    {
        if (!HasPublicAccess(context))
            return context.FilesDir!.AbsolutePath;

        var root = PublicRoot;
        Directory.CreateDirectory(Path.Combine(root, "images"));

        // Kod pisany przed udzieleniem zgody przenosimy do folderu publicznego.
        var privateCode = Path.Combine(context.FilesDir!.AbsolutePath, "Game.cs");
        var publicCode = Path.Combine(root, "Game.cs");
        if (!File.Exists(publicCode) && File.Exists(privateCode))
            File.Copy(privateCode, publicCode);

        return root;
    }

    public static string CodePath(Context context) => Path.Combine(Root(context), "Game.cs");

    public static string ImagesDir(Context context) => Path.Combine(Root(context), "images");

    public static void RequestPublicAccess(Activity activity)
    {
        if (HasPublicAccess(activity))
            return;

        new AlertDialog.Builder(activity)
            .SetTitle("Dostęp do plików")
            .SetMessage("Kod i obrazki gry będą w folderze CsharpGame w pamięci telefonu, żebyś mógł je wgrywać np. przez FTP.\n\nWłącz „Zezwalaj na dostęp do wszystkich plików”.")
            .SetPositiveButton("Ustawienia", (_, _) =>
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
                    activity.StartActivity(new Intent(Settings.ActionManageAppAllFilesAccessPermission, Android.Net.Uri.Parse("package:" + activity.PackageName)));
                else
                    activity.RequestPermissions([Android.Manifest.Permission.WriteExternalStorage, Android.Manifest.Permission.ReadExternalStorage], 1);
            })
            .SetNegativeButton("Nie teraz", (_, _) => { })
            .Show();
    }
}
