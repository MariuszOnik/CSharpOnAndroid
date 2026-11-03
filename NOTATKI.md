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

## Przydatne polecenia
```powershell
.\start-emulator.ps1   # włącza emulator
.\run-android.ps1      # buduje, wgrywa i uruchamia grę (telefon lub emulator)
adb devices            # lista podłączonych urządzeń
```

## Następnym razem
1. Ekran edytora: WebView + CodeMirror 6 z kolorowaniem C#.
2. Roslyn na telefonie: kompilacja tekstu z edytora do assembly w pamięci, lista błędów.
3. Przycisk „Graj”: załadowanie skompilowanego assembly i uruchomienie gry MonoGame.
4. Telefon: włączyć opcje programisty + debugowanie USB, kabel do transmisji danych.
