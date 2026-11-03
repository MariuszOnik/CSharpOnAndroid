# Buduje GamePlayer, wgrywa na podłączony telefon/emulator i uruchamia.
$sdk = "$env:LOCALAPPDATA\Android\Sdk"
$jdk = "$env:LOCALAPPDATA\Android\openjdk"
dotnet build "$PSScriptRoot\src\GamePlayer\GamePlayer.csproj" -t:Run "-p:AndroidSdkDirectory=$sdk" "-p:JavaSdkDirectory=$jdk"
