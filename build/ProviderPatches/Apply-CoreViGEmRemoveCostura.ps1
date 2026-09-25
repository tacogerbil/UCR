param(
    [Parameter(Mandatory = $true)]
    [string]$IOWrapperRoot
)

$ErrorActionPreference = 'Stop'
$csprojPath = Join-Path $IOWrapperRoot 'Source\Core Providers\Core_ViGEm\Core_ViGEm.csproj'

if (-not (Test-Path -LiteralPath $csprojPath -PathType Leaf)) {
    throw "Core_ViGEm project file not found: $csprojPath"
}

$source = Get-Content -LiteralPath $csprojPath -Raw

$costuraImportPattern = '(?ms)^  <Import Project="\.\.\\\.\.\\\.\.\\packages\\Costura\.Fody\.2\.0\.1\\build\\Costura\.Fody\.targets" Condition="Exists\(.*?\)" />\r?\n'
if (-not $source.Contains('Costura.Fody.2.0.1\build\Costura.Fody.targets')) {
    Write-Host 'Core_ViGEm Costura removal is already applied.'
    exit 0
}

# Nefarius.ViGEmClient (the modern managed rewrite this provider now references) ships a single
# pure-managed DLL with no native payload at all -- Costura.Fody has nothing left to embed/extract.
# Its generated module initializer (Costura.AssemblyLoader.Attach()) calls the .NET Framework-era
# Mutex.SetAccessControl(MutexSecurity) overload, which does not resolve under .NET 8's BCL
# (MissingMethodException), crashing Core_ViGEm.dll's <Module> cctor before InitLibrary() ever runs.
# Verified against upstream Nefarius.ViGEmClient 1.15.16 (lib\net452, single managed DLL, no natives).

$referencePattern = '(?ms)^    <Reference Include="Costura, Version=2\.0\.1\.0, Culture=neutral, PublicKeyToken=9919ef960d84173d, processorArchitecture=MSIL">\r?\n      <HintPath>\.\.\\\.\.\\\.\.\\packages\\Costura\.Fody\.2\.0\.1\\lib\\net452\\Costura\.dll</HintPath>\r?\n    </Reference>\r?\n'
if (([regex]::Matches($source, $referencePattern)).Count -ne 1) {
    throw 'Expected exactly one Costura Reference block. Refusing a partial patch.'
}
$source = [regex]::Replace($source, $referencePattern, '', 1)

$errorCheckPattern = '(?ms)^    <Error Condition="!Exists\(''\.\.\\\.\.\\\.\.\\packages\\Costura\.Fody\.2\.0\.1\\build\\Costura\.Fody\.targets''\)" Text="\$\(\[System\.String\]::Format\(''\$\(ErrorText\)'', ''\.\.\\\.\.\\\.\.\\packages\\Costura\.Fody\.2\.0\.1\\build\\Costura\.Fody\.targets''\)\)" />\r?\n'
if (([regex]::Matches($source, $errorCheckPattern)).Count -ne 1) {
    throw 'Expected exactly one Costura EnsureNuGetPackageBuildImports Error check. Refusing a partial patch.'
}
$source = [regex]::Replace($source, $errorCheckPattern, '', 1)

if (([regex]::Matches($source, [regex]::Escape('Costura.Fody.2.0.1\build\Costura.Fody.targets'))).Count -ne 2) {
    throw 'Expected exactly one remaining Costura.Fody.targets Import line (matching Project= and Condition=Exists() twice) after removing the Reference and Error check.'
}
$source = [regex]::Replace($source, $costuraImportPattern, '', 1)

Set-Content -LiteralPath $csprojPath -Value $source -Encoding UTF8 -NoNewline

$verified = Get-Content -LiteralPath $csprojPath -Raw
if ($verified.Contains('Costura')) {
    throw 'Costura removal verification failed: a Costura reference still remains.'
}

Write-Host 'Applied Core_ViGEm Costura removal: no native payload to embed, and its .NET 8-incompatible module initializer no longer runs.'
