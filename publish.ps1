param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained = $true,
    [switch]$SingleFile = $true
)

Write-Host "Publishing Peekerino..." -ForegroundColor Cyan

$publishArgs = @(
    "publish",
    "-c", $Configuration,
    "-r", $Runtime
)

if ($SelfContained) {
    $publishArgs += "--self-contained"
    $publishArgs += "true"
} else {
    $publishArgs += "--self-contained"
    $publishArgs += "false"
}

if ($SingleFile) {
    $publishArgs += "/p:PublishSingleFile=true"
    $publishArgs += "/p:IncludeAllContentForSelfExtract=true"
} else {
    $publishArgs += "/p:PublishSingleFile=false"
}

if ($SingleFile) {
    $publishArgs += "/p:EnableCompressionInSingleFile=true"
}

$publishArgs += "/p:DebugType=None"
$publishArgs += "/p:DebugSymbols=false"

dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed."
    exit $LASTEXITCODE
}

$output = Join-Path -Path "bin" -ChildPath "$Configuration\net9.0-windows\$Runtime\publish"
Write-Host "Publish succeeded. Output in $output" -ForegroundColor Green

