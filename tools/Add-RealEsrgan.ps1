[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $AppDirectory
)

$ErrorActionPreference = 'Stop'
$app = (Resolve-Path -LiteralPath $AppDirectory).Path
if (!(Test-Path -LiteralPath (Join-Path $app 'CatgirlStudio.exe') -PathType Leaf)) {
    throw 'The app directory must contain a published CatgirlStudio.exe.'
}

$archiveName = 'realesrgan-ncnn-vulkan-20220424-windows.zip'
$archivePath = Join-Path (Split-Path -Parent $app) $archiveName
$expectedSha256 = 'ABC02804E17982A3BE33675E4D471E91EA374E65B70167ABC09E31ACB412802D'
if (!(Test-Path -LiteralPath $archivePath -PathType Leaf)) {
    $url = "https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.5.0/$archiveName"
    Invoke-WebRequest -Uri $url -OutFile $archivePath
}
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $expectedSha256) {
    throw "Unexpected SHA-256 for $archivePath."
}

$upscaler = Join-Path $app 'realesrgan'
New-Item -ItemType Directory -Path (Join-Path $upscaler 'models') -Force | Out-Null
$files = @('realesrgan-ncnn-vulkan.exe', 'vcomp140.dll')
foreach ($scale in 2..4) {
    $files += "models/realesr-animevideov3-x$scale.bin", "models/realesr-animevideov3-x$scale.param"
}

$archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    foreach ($name in $files) {
        $entry = $archive.GetEntry($name)
        if ($null -eq $entry -or $entry.Length -eq 0) { throw "Missing archive entry: $name." }
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $upscaler $name), $true)
    }
} finally {
    $archive.Dispose()
}

$licenses = Join-Path $app 'licenses/realesrgan'
New-Item -ItemType Directory -Path $licenses -Force | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot '../third-party/realesrgan/*') -Destination $licenses
