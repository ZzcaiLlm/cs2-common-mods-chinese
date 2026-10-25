$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$tests = Join-Path $PSScriptRoot 'Tests'
$work = Join-Path $PSScriptRoot 'obj'
$output = Join-Path $work 'tests'
if (!(Test-Path -LiteralPath (Join-Path $work 'zh-HANS.bin'))) { throw 'Run Build-Loader.ps1 first.' }
$null = New-Item -ItemType Directory -Path $output -Force
& $compiler /nologo /target:library /warnaserror+ "/out:$output\TestApi.dll" "$tests\TestApi.cs"
if ($LASTEXITCODE -ne 0) { throw 'API fixture compilation failed.' }
& $compiler /nologo /target:library /warnaserror+ "/reference:$output\TestApi.dll" "/out:$output\Placemaker.dll" "$tests\PlacemakerStub.cs"
if ($LASTEXITCODE -ne 0) { throw 'Original-mod fixture compilation failed.' }
& $compiler /nologo /target:exe /warnaserror+ /utf8output "/reference:$output\TestApi.dll" "/out:$output\TestMain.exe" "/resource:$work\zh-HANS.bin,PlacemakerChinese.zh-HANS.bin" "/resource:$work\civicbeaver-assets.txt,PlacemakerChinese.CivicbeaverAssets.txt" "/resource:$work\extra-keys.txt,PlacemakerChinese.ExtraKeys.txt" "/resource:$tests\RequiredUiKeys.txt,PlacemakerChinese.Tests.RequiredUiKeys.txt" "/resource:$tests\RequiredSettingsKeys.txt,PlacemakerChinese.Tests.RequiredSettingsKeys.txt" "$PSScriptRoot\Mod.cs" "$tests\TestMain.cs" "$work\Version.cs"
if ($LASTEXITCODE -ne 0) { throw 'Behavior test compilation failed.' }
& "$output\TestMain.exe"
if ($LASTEXITCODE -ne 0) { throw 'Behavior tests failed.' }
