param(
    [Parameter(Mandatory = $true)]
    [string]$ApkPath,
    [string]$PackageName = "",
    [string]$LaunchActivity = "",
    [switch]$LaunchAfterInstall,
    [switch]$Wireless,
    [string]$DeviceId = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-AdbPath {
    if ($env:ANDROID_SDK_ROOT) {
        $candidate = Join-Path $env:ANDROID_SDK_ROOT "platform-tools\adb.exe"
        if (Test-Path $candidate) { return $candidate }
    }
    if ($env:ANDROID_HOME) {
        $candidate = Join-Path $env:ANDROID_HOME "platform-tools\adb.exe"
        if (Test-Path $candidate) { return $candidate }
    }

    $userSdk = Join-Path $env:LOCALAPPDATA "Android\Sdk\platform-tools\adb.exe"
    if (Test-Path $userSdk) { return $userSdk }

    $cmd = Get-Command adb -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    throw "adb not found. Install Android platform-tools or set ANDROID_SDK_ROOT."
}

function Parse-PackageAndActivityFromApk {
    param(
        [string]$BuildToolsBin,
        [string]$Apk
    )

    if (-not (Test-Path $BuildToolsBin)) {
        return @{ Package = ""; Activity = "" }
    }

    $output = & $BuildToolsBin dump badging $Apk 2>$null
    $package = ""
    $activity = ""

    foreach ($line in $output) {
        if ($line -like "package:*") {
            if ($line -match "name='([^']+)'") { $package = $matches[1] }
        }
        if ($line -like "launchable-activity:*") {
            if ($line -match "name='([^']+)'") { $activity = $matches[1] }
        }
    }

    return @{ Package = $package; Activity = $activity }
}

function Resolve-AaptPath {
    if (-not $env:ANDROID_SDK_ROOT -and -not $env:ANDROID_HOME) { return "" }
    $sdkRoot = if ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT } else { $env:ANDROID_HOME }
    $buildToolsRoot = Join-Path $sdkRoot "build-tools"
    if (-not (Test-Path $buildToolsRoot)) { return "" }

    $dirs = Get-ChildItem $buildToolsRoot -Directory | Sort-Object Name -Descending
    foreach ($dir in $dirs) {
        $aapt = Join-Path $dir.FullName "aapt.exe"
        if (Test-Path $aapt) { return $aapt }
    }
    return ""
}

if (-not (Test-Path $ApkPath)) {
    throw "APK not found: $ApkPath"
}

$adb = Resolve-AdbPath
Write-Host "Using adb: $adb" -ForegroundColor Cyan

if ($Wireless) {
    & $adb start-server | Out-Null
}

$devicesOutput = & $adb devices
$deviceLines = $devicesOutput | Where-Object { $_ -match "device$" -and $_ -notmatch "List of devices" }
if (-not $deviceLines) {
    throw "No connected VR device found. Connect headset (USB or wireless adb) and enable USB debugging."
}

$adbArgs = @()
if ($DeviceId) {
    $adbArgs += @("-s", $DeviceId)
}

Write-Host "Installing APK..." -ForegroundColor Green
& $adb @adbArgs install -r $ApkPath

if ($LASTEXITCODE -ne 0) {
    throw "APK install failed."
}

Write-Host "Install complete." -ForegroundColor Green

if ($LaunchAfterInstall) {
    if (-not $PackageName -or -not $LaunchActivity) {
        $aapt = Resolve-AaptPath
        if ($aapt) {
            $parsed = Parse-PackageAndActivityFromApk -BuildToolsBin $aapt -Apk $ApkPath
            if (-not $PackageName) { $PackageName = $parsed.Package }
            if (-not $LaunchActivity) { $LaunchActivity = $parsed.Activity }
        }
    }

    if (-not $PackageName) {
        throw "Launch requested but PackageName is missing. Provide -PackageName or install Android build-tools for auto-detection."
    }

    if ($LaunchActivity) {
        $component = "$PackageName/$LaunchActivity"
        Write-Host "Launching: $component" -ForegroundColor Yellow
        & $adb @adbArgs shell am start -n $component
    }
    else {
        Write-Host "Launching package: $PackageName" -ForegroundColor Yellow
        & $adb @adbArgs shell monkey -p $PackageName -c android.intent.category.LAUNCHER 1
    }
}

Write-Host "VR deploy script finished successfully." -ForegroundColor Green
