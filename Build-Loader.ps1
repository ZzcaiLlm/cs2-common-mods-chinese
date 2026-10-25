param(
    [Parameter(Mandatory=$true)][string]$GamePath,
    [Parameter(Mandatory=$true)][string]$TranslationProject,
    [ValidatePattern('^\d{1,4}\.\d{1,4}\.\d{1,4}$')][string]$Version = '1.1.3'
)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$managed = Join-Path $GamePath 'Cities2_Data\Managed'
$work = Join-Path $PSScriptRoot 'obj'
$dist = Join-Path $PSScriptRoot 'dist'
$null = New-Item -ItemType Directory -Path $work,$dist -Force
$utf8 = [Text.UTF8Encoding]::new($false)
$names = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
$manifest = Get-Content -LiteralPath (Join-Path $TranslationProject 'Assets\civicbeaver-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($package in $manifest.Packages) {
    foreach ($asset in $package.Assets) { $null = $names.Add([string]$asset.Name) }
}
$entries = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::Ordinal)
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $TranslationProject 'Lang') -Filter 'zh-HANS*.json' -File | Sort-Object Name) {
    $json = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($entry in $json.PSObject.Properties) {
        if ($entry.Value -isnot [string] -or [string]::IsNullOrWhiteSpace($entry.Value)) {
            throw "Invalid translation: $($entry.Name)"
        }
        $entries.Add($entry.Name, $entry.Value)
    }
}
$extra = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
$overridesPath = Join-Path $TranslationProject 'Editor\zh-HANS.overrides.json'
if (Test-Path -LiteralPath $overridesPath) {
    $json = Get-Content -LiteralPath $overridesPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($entry in $json.PSObject.Properties) {
        if ($entry.Name.Length -gt 512 -or
            $entry.Name -cnotmatch '^(Assets\.(NAME|DESCRIPTION)\[[^\r\n\[\]]+\]|[A-Za-z][A-Za-z0-9_.-]*(\[[^\r\n\[\]]+\])?)$' -or
            $entry.Value -isnot [string] -or [string]::IsNullOrWhiteSpace($entry.Value)) {
            throw "Invalid custom translation: $($entry.Name)"
        }
        if (!$entries.ContainsKey($entry.Name)) { $null = $extra.Add($entry.Name) }
        $entries[$entry.Name] = $entry.Value
    }
}
if ($entries.Count -lt 1 -or $entries.Count -gt 100000) { throw 'Invalid entry count.' }
$writer = [IO.BinaryWriter]::new([IO.File]::Create((Join-Path $work 'zh-HANS.bin')))
try {
    $writer.Write([string]'PlacemakerChinese/1')
    $writer.Write([int]$entries.Count)
    foreach ($key in $entries.Keys | Sort-Object -CaseSensitive) {
        $writer.Write([string]$key)
        $writer.Write([string]$entries[$key])
    }
} finally { $writer.Dispose() }
[IO.File]::WriteAllLines((Join-Path $work 'civicbeaver-assets.txt'), [string[]]@($names | Sort-Object -CaseSensitive), $utf8)
[IO.File]::WriteAllLines((Join-Path $work 'extra-keys.txt'), [string[]]@($extra | Sort-Object -CaseSensitive), $utf8)
[IO.File]::WriteAllText((Join-Path $work 'Version.cs'),
    ('[assembly: System.Reflection.AssemblyVersion("' + $Version + '.0")]' + "`r`n" +
    '[assembly: System.Reflection.AssemblyFileVersion("' + $Version + '.0")]'), $utf8)
$arguments = @('/nologo','/target:library','/optimize+','/warnaserror+','/utf8output',
    "/out:$dist\PlacemakerChinese.dll",
    "/resource:$work\zh-HANS.bin,PlacemakerChinese.zh-HANS.bin",
    "/resource:$work\civicbeaver-assets.txt,PlacemakerChinese.CivicbeaverAssets.txt",
    "/resource:$work\extra-keys.txt,PlacemakerChinese.ExtraKeys.txt")
foreach ($name in @('Game.dll','Colossal.Core.dll','Colossal.Localization.dll','Colossal.Logging.dll',
    'UnityEngine.CoreModule.dll','Unity.Entities.dll','netstandard.dll')) {
    $arguments += '/reference:' + (Join-Path $managed $name)
}
$arguments += Join-Path $PSScriptRoot 'Mod.cs'
$arguments += Join-Path $work 'Version.cs'
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Loader compilation failed.' }
Write-Output "Built $dist\PlacemakerChinese.dll with $($entries.Count) entries."
