$ErrorActionPreference = 'Stop'

Write-Host "Building SimpleBackup..." -ForegroundColor Cyan
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    dotnet build
}

Write-Host "Preparing Release Folder..." -ForegroundColor Cyan
if (!(Test-Path "releases")) {
    New-Item -ItemType Directory -Force -Path "releases" | Out-Null
}

$TempDir = "releases\temp"
if (Test-Path $TempDir) {
    Remove-Item -Recurse -Force $TempDir
}
New-Item -ItemType Directory -Force -Path $TempDir | Out-Null

Write-Host "Gathering Artifacts..." -ForegroundColor Cyan
$ReleaseDll = "src\bin\Release\net462\SimpleBackup.dll"
$DebugDll = "src\bin\Debug\net462\SimpleBackup.dll"

if (Test-Path $ReleaseDll) {
    Write-Host "   -> Found Release DLL"
    Copy-Item $ReleaseDll -Destination $TempDir
    
    $BinDir = Split-Path $ReleaseDll
    $MonoModDlls = Get-ChildItem "$BinDir\MonoMod.*.dll"
    foreach ($dll in $MonoModDlls) {
        Write-Host "   -> Copying dependency: $($dll.Name)"
        Copy-Item $dll.FullName -Destination $TempDir
    }
} elseif (Test-Path $DebugDll) {
    Write-Host "   -> Found Debug DLL"
    Copy-Item $DebugDll -Destination $TempDir
    
    # Copy MonoMod dependencies that are required by the modern Harmony link
    $BinDir = Split-Path $DebugDll
    $MonoModDlls = Get-ChildItem "$BinDir\MonoMod.*.dll"
    foreach ($dll in $MonoModDlls) {
        Write-Host "   -> Copying dependency: $($dll.Name)"
        Copy-Item $dll.FullName -Destination $TempDir
    }
} else {
    Write-Host "Error: Could not find compiled DLL! Make sure the build succeeds." -ForegroundColor Red
    exit 1
}

Write-Host "   -> Copying Thunderstore Metadata..."
Copy-Item "Thunderstore\manifest.json" -Destination $TempDir
Copy-Item "Thunderstore\icon.png" -Destination $TempDir
Copy-Item "Thunderstore\description.md" -Destination $TempDir
Copy-Item "README.md" -Destination $TempDir

$ManifestContent = Get-Content "Thunderstore\manifest.json" | ConvertFrom-Json
$Version = $ManifestContent.version_number

$ZipName = "releases\$($ManifestContent.name)-$Version.zip"

if (Test-Path $ZipName) {
    Remove-Item -Force $ZipName
}

Write-Host "Compressing to $ZipName..." -ForegroundColor Cyan
Compress-Archive -Path "$TempDir\*" -DestinationPath $ZipName -Force

Write-Host "Cleaning up temp files..." -ForegroundColor Cyan
Remove-Item -Recurse -Force $TempDir

Write-Host "Release created successfully: $ZipName" -ForegroundColor Green
