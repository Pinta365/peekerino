param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained = $true,
    [switch]$SingleFile = $true,
    [string]$Tag,
    [switch]$PushTag,
    [string]$Remote = "origin"
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

if ([string]::IsNullOrWhiteSpace($Tag)) {
    return
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Warning "Git command not found. Skipping tag creation."
    return
}

$existingTag = git tag --list $Tag
if ($existingTag) {
    Write-Warning "Tag '$Tag' already exists. Skipping."
    return
}

$status = git status --porcelain
if ($status) {
    Write-Warning "Working tree has uncommitted changes. Tag skipped. Commit or stash changes first."
    return
}

git tag $Tag
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Failed to create tag '$Tag'."
    return
}

Write-Host "Created tag '$Tag'." -ForegroundColor Cyan

if (-not $PushTag) {
    return
}

git push $Remote $Tag
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Failed to push tag '$Tag' to '$Remote'."
    return
}

Write-Host "Pushed tag '$Tag' to '$Remote'." -ForegroundColor Cyan

