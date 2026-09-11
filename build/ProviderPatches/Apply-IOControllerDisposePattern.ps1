param(
    [Parameter(Mandatory = $true)]
    [string]$IOWrapperRoot
)

$ErrorActionPreference = 'Stop'
$path = Join-Path $IOWrapperRoot 'Source\Core\IOController.cs'
if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
    throw "IOWrapper IOController source not found: $path"
}

$source = Get-Content -LiteralPath $path -Raw

# 1. Finalizer Dispose(true) -> Dispose(false)
$source = [regex]::Replace($source, '~\s*IOController\s*\(\)\s*\{\s*Dispose\(true\);\s*\}', "~IOController()`n        {`n            Dispose(false);`n        }")

# 2. Add GC.SuppressFinalize(this) to Dispose()
$source = [regex]::Replace($source, 'public\s+void\s+Dispose\s*\(\)\s*\{\s*Dispose\(true\);\s*\}', "public void Dispose()`n        {`n            Dispose(true);`n            GC.SuppressFinalize(this);`n        }")

# 3. Add null check to _providers loop in Dispose(bool disposing)
$disposingPattern = 'if\s*\(disposing\)\s*\{\s*foreach\s*\(var\s+provider\s+in\s+_providers\.Values\)\s*\{\s*provider\.Dispose\(\);\s*\}\s*_providers\s*=\s*null;\s*\}'
$disposingPatched = @"
if (disposing)
            {
                if (_providers != null)
                {
                    foreach (var provider in _providers.Values)
                    {
                        provider.Dispose();
                    }
                    _providers = null;
                }
            }
"@
$source = [regex]::Replace($source, $disposingPattern, $disposingPatched)

Set-Content -LiteralPath $path -Value $source -Encoding UTF8

$verified = Get-Content -LiteralPath $path -Raw
if (-not $verified.Contains('GC.SuppressFinalize(this);') -or -not $verified.Contains('Dispose(false);') -or -not $verified.Contains('if (_providers != null)')) {
    throw 'IOWrapper IOController dispose pattern patch verification failed.'
}

Write-Host 'Applied IOWrapper IOController dispose pattern fix.'
