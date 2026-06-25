$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "src\CuteSpace\CuteSpace.csproj"
$buildOutput = Join-Path $root "src\CuteSpace\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64"
$publishDir = Join-Path $root "release\windows\CuteSpace"
$zipPath = Join-Path $root "release\CuteSpace-v0.1.0-windows-x64.zip"

function Find-MSBuild {
    $fromPath = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    $candidates = @(
        "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "MSBuild.exe was not found. Install Visual Studio 2022 with Windows App SDK / WinUI support, then run this script from Developer PowerShell."
}

New-Item -ItemType Directory -Path (Split-Path $zipPath) -Force | Out-Null
if (Test-Path $publishDir) {
    try {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }
    catch {
        $publishDir = Join-Path $root "release\windows\CuteSpace-fixed"
        $zipPath = Join-Path $root "release\CuteSpace-v0.1.0-windows-x64-fixed.zip"
        if (Test-Path $publishDir) {
            Remove-Item -LiteralPath $publishDir -Recurse -Force
        }
    }
}
if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

$msbuild = Find-MSBuild
& $msbuild $project `
    /t:Restore,Rebuild `
    /p:Configuration=Release `
    /p:Platform=x64 `
    /p:RuntimeIdentifier=win-x64 `
    /p:SelfContained=true `
    /m

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
Copy-Item -Path (Join-Path $buildOutput "*") -Destination $publishDir -Recurse -Force
Get-ChildItem -Path $publishDir -Recurse -Filter "*.pdb" | Remove-Item -Force

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

Write-Host "Windows build created:"
Write-Host "  $publishDir"
Write-Host "  $zipPath"
