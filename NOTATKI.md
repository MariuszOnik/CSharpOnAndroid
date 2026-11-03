# CsharpOnAndroid – notatki

## Cel
Aplikacja na Androida, w której piszę kod gry w C# na telefonie, kompiluję go Roslynem na urządzeniu i od razu uruchamiam.

Plan techniczny:
- **Gra/silnik:** MonoGame (Android), system komponentów.
- **Edytor:** CodeMirror 6 w WebView (offline, lokalny HTML), mostek JS ↔ C#.
- **Kompilacja:** Roslyn (`Microsoft.CodeAnalysis.CSharp`) w aplikacji, przycisk „Graj”.
- **IntelliSense:** `CompletionService` z `Microsoft.CodeAnalysis.CSharp.Features`. Do podglądania rozwiązań: RoslynPad.
- Kolejność: kompilacja + błędy → podpowiadanie kodu → reszta (hover, parametry, rename).

## Stan na 2026-11-03 – środowisko gotowe ✅
- .NET SDK 10.0.401 (+ 8.0), workload `android`, szablony MonoGame 3.8.6
- Android SDK: `%LOCALAPPDATA%\Android\Sdk`
- JDK 17 dla Androida: `%LOCALAPPDATA%\Android\openjdk`
  (systemowy Microsoft OpenJDK 17.0.21 z `Program Files` **nie działa** z narzędziami Androida – nie rozpoznają jego numeru wersji)
- Zmienne użytkownika: `JAVA_HOME`, `ANDROID_HOME`, `platform-tools` w PATH
- Emulator: AVD `Pixel_API35` (Android 15, x86_64)
- Projekt `src/GamePlayer` (MonoGame Android, przestawiony na `net10.0-android`, targetSdk 36) – buduje się, instaluje i działa w emulatorze (niebieski ekran)

## Stan na 2026-11-03 wieczór – pierwsza działająca wersja ✅
- Edytor CodeMirror 6 w WebView (`editor-web/` → `npm run build` → `Assets/editor/editor.js`)
- Roslyn kompiluje kod **na telefonie**, błędy podkreślane w edytorze
- „▶ Graj” uruchamia grę w MonoGame, Wstecz wraca do edytora, kod zapisuje się w `files/Game.cs`
- `GameScript` (src/GameApi): `Start/Update/Draw`, `FillRect`, dotyk, `LoadImage` + `DrawImage(Centered)`
- Obrazki: PNG bez mgcb (`Texture2D.FromStream`) – z `Assets/images/` albo `files/images/` gracza
- APK Release (bez AOT i trimmingu, ~58 MB) wysłane na telefon przez FTP do `Download/CsharpGamePlayer.apk`

Pułapki, na które już trafiliśmy:
- FastDev psuł GameApi.dll → `EmbedAssembliesIntoApk=true`
- mgcb się wywalał → usunięty `MonoGame.Content.Builder.Task` (obrazki ładujemy z PNG)
- Release: AOT wymaga trimmingu, a trimming psuje kod gracza → `RunAOTCompilation=false`
- Android 15+: edge-to-edge → ręczne odsunięcie od pasków systemowych (`SystemBarsPadding`)

## Przydatne polecenia
```powershell
.\start-emulator.ps1   # włącza emulator
.\run-android.ps1      # buduje, wgrywa i uruchamia grę (telefon lub emulator)
adb devices            # lista podłączonych urządzeń
```

## Następnym razem
1. Test na prawdziwym telefonie (czas kompilacji Roslyna na ARM).
2. IntelliSense: podpowiedzi po kropce (`CompletionService`).
3. Wybór własnych obrazków z galerii telefonu → `files/images/`.
4. Tekst na ekranie (czcionka bez mgcb, np. FontStashSharp).
5. Kilka plików / projektów gry zamiast jednego `Game.cs`.
6. Mniejsze APK: tylko `android-arm64`.
