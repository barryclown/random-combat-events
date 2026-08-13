[CmdletBinding()]
param(
    [ValidateSet('private', 'public', 'unlisted', 'friends_only')]
    [string]$Visibility,

    [string]$ChangeNote
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$manifestPath = Join-Path $PSScriptRoot 'workshop.json'
$descriptionPath = Join-Path $PSScriptRoot 'description.bbcode'
$previewPath = Join-Path $PSScriptRoot 'image.png'
$screenshotsPath = Join-Path $PSScriptRoot 'previews'

$description = [System.IO.File]::ReadAllText($descriptionPath, [System.Text.Encoding]::UTF8).TrimEnd()
$descriptionBytes = [System.Text.Encoding]::UTF8.GetByteCount($description)

if ($descriptionBytes -gt 8000) {
    throw "Workshop description is $descriptionBytes UTF-8 bytes; Steam allows at most 8000."
}

if ($description -match 'PLACEHOLDER_') {
    throw 'Workshop description still contains a placeholder.'
}

$manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestPath | ConvertFrom-Json
$manifest.description = $description

if ($PSBoundParameters.ContainsKey('Visibility')) {
    $manifest.visibility = $Visibility
}

if ($PSBoundParameters.ContainsKey('ChangeNote')) {
    $manifest.changeNote = $ChangeNote
}

if (-not (Test-Path -LiteralPath $previewPath -PathType Leaf)) {
    throw 'workshop/image.png is missing.'
}

$uploadImages = @((Get-Item -LiteralPath $previewPath))
if (Test-Path -LiteralPath $screenshotsPath -PathType Container) {
    $uploadImages += @(Get-ChildItem -LiteralPath $screenshotsPath -File)
}

foreach ($image in $uploadImages) {
    if ($image.Length -ge 1MB) {
        throw "$($image.FullName) is $($image.Length) bytes; Workshop images must be smaller than 1 MiB."
    }
}

$json = $manifest | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($manifestPath, $json + [Environment]::NewLine, $utf8NoBom)

Write-Host "Synchronized workshop.json ($descriptionBytes/8000 description bytes, $($uploadImages.Count) images checked)."
