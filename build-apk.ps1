# Buduje osobne APK Release na kazda architekture i kopiuje je do folderu dist.
#   .\build-apk.ps1                     # 64-bit i 32-bit
#   .\build-apk.ps1 -Abi android-arm64  # tylko 64-bit
param([string[]]$Abi = @("android-arm64", "android-arm"))

$ErrorActionPreference = "Stop"
$sdk = "$env:LOCALAPPDATA\Android\Sdk"
$jdk = "$env:LOCALAPPDATA\Android\openjdk"
$project = "$PSScriptRoot\src\GamePlayer"
$dist = "$PSScriptRoot\dist"
New-Item -ItemType Directory -Force $dist | Out-Null

foreach ($rid in $Abi) {
    Write-Host "=== $rid ===" -ForegroundColor Cyan
    # Kazdy wariant od czysta: przyrostowy Release juz gubil metody, a tu zmienia sie tez lista architektur.
    foreach ($dir in "$project\bin\Release", "$project\obj\Release") {
        if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }
    }

    dotnet build "$project\GamePlayer.csproj" -c Release "-p:ApkAbi=$rid" "-p:AndroidSdkDirectory=$sdk" "-p:JavaSdkDirectory=$jdk" -v:minimal -clp:ErrorsOnly
    if ($LASTEXITCODE -ne 0) { throw "Build $rid nie powiodl sie" }

    $name = "CsharpGamePlayer-" + $rid.Replace("android-", "") + ".apk"
    Copy-Item "$project\bin\Release\net10.0-android\GamePlayer.GamePlayer-Signed.apk" "$dist\$name" -Force
}

Get-ChildItem $dist -Filter *.apk | Select-Object Name, @{ n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table
