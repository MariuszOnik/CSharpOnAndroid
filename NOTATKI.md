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

## Stan na 2026-11-04 – projekty, tekst i silnik izo natywnie ✅
- Obejścia z refleksją z telefonu (`Tekst`, edytor w grze, Roslyn przez refleksję) zastąpione kodem w aplikacji
- Projekty: `CsharpGame/projects/<nazwa>/*.cs` + `images/`, wspólne obrazki w `CsharpGame/images/`
  (stare `Game.cs` i `projekt/` są kopiowane do projektów `Imported` i `projekt`, oryginały zostają)
- Edytor: wybór projektu, zakładki plików, nowy/zmiana nazwy/usuwanie, lista błędów ze skokiem do pliku i linii,
  podpowiedzi z całego projektu, wczytywanie zmian z dysku po powrocie do aplikacji
- `GameApi`: `DrawText`, `DrawTextCentered`, `MeasureText` (FontStashSharp, DejaVu Sans), `JustTouched`, `JustReleased`, `Fps`, `ListImages`
- `GameApi/Iso`: `IsoWorld`, `IsoObject`, `IsoSorter`, `IsoSprite` – zgodne ze starym kodem z telefonu (test porównawczy)
- Kod gracza kompilowany z optymalizacją Release
- Przykłady w `Assets/samples/`: `BallDemo`, `IsoStress` (klocki Kenneya, CC0)

## 2026-11-09 – pomoc i sprite'y
- Przycisk „?” w edytorze → `Assets/editor/help.html` (poradnik API) w nakładce, Wstecz ją zamyka
- `GameApi/Sprite.cs`: wycinek atlasu + pivot 0..1, `Sprite.Slice`, `Sprite.Animate`;
  w `GameScript`: `LoadSprites`, `DrawSprite`, `DrawImage(img, source, pos, pivot, ...)` (flip odbija też pivot)
- Telefon: FTP `192.168.1.28:5041`, APK do `device/Download/`
- Przykład `IsoEditor` (edytor mapy izo, tylko dotyk) + `help-iso.html` (Tutorial 2).
  `farma.png` = `crops_tileset.png` bez białego tła, przepakowany do siatki 36×48 z pivotem (18, 35)
- `GameScript.ProjectFolder` / `ProjectFile(nazwa)` – zapis plików gry w folderze projektu
- Przykłady instalują się przy każdym starcie, jeśli jeszcze nie były (flaga per przykład) – wcześniej tylko przy pierwszym

Pułapki, na które już trafiliśmy:
- FastDev psuł GameApi.dll → `EmbedAssembliesIntoApk=true`
- mgcb się wywalał → usunięty `MonoGame.Content.Builder.Task` (obrazki ładujemy z PNG)
- Release: AOT wymaga trimmingu, a trimming psuje kod gracza → `RunAOTCompilation=false`
- Android 15+: edge-to-edge → ręczne odsunięcie od pasków systemowych (`SystemBarsPadding`)
- Telefon testowy Redmi 9A (Android 10, MIUI, 2 GB RAM) ma tylko 32-bitowego Androida → `RuntimeIdentifiers` zawiera `android-arm`
- MIUI: `adb install` wymaga opcji „Instaluj przez USB”, a stukanie z ADB – „Debugowanie USB (ustawienia zabezpieczeń)”, które chce logowania na konto Mi.
  Bez tego: `adb push` APK do `/sdcard/Download` i ręczna instalacja na telefonie
- Build przyrostowy Release z marshal methods gubił metody → `UnsatisfiedLinkError` przy starcie → `AndroidEnableMarshalMethods=false`
- Przy widocznych paskach systemowych MonoGame zwężał obraz → `GameActivity.HideSystemBars`

## Przydatne polecenia
```powershell
.\start-emulator.ps1   # włącza emulator
.\run-android.ps1      # buduje, wgrywa i uruchamia grę (telefon lub emulator)
adb devices            # lista podłączonych urządzeń
```

## Następnym razem
0. Redmi 9A: zainstalować `Download/CsharpGamePlayer-naprawiony.apk` i sprawdzić start, pełny ekran (bez niebieskich pasów)
   oraz „Sprawdź” w IsoStress. Poprzedni pomiar 48×48 przed poprawką: sort 1,7 ms, rysowanie 0,8 ms, 42 FPS, ok. 310 MB.
1. Zmierzyć na telefonie czas kompilacji oraz IsoStress w trybach PROSTY i SZYBKI.
2. Silnik izo: mapa z JSON-a, potem edytor mapy.
3. Animowane sprite'y, A*, warstwy (podłoga, obiekty, efekty, UI), dźwięk.
4. Wybór własnych obrazków z galerii telefonu → `images/` projektu.
5. Mniejsze APK: tylko `android-arm64`.
6. Port rdzenia izo do C na DS/3DS, gdy API się ustabilizuje.
