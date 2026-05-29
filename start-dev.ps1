param(
    [int]$ApiPort = 5000,
    [int]$WebPort = 8081,
    [string]$ApiProject = "AuthTest.Api",
    [string]$WebPath = "AuthTest.Web",
    [switch]$KillPortOwners,
    [switch]$SkipWebBuild
)

$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    if ($PSScriptRoot -and (Test-Path $PSScriptRoot)) {
        return $PSScriptRoot
    }

    return (Get-Location).Path
}

function Test-PortInUse {
    param([int]$Port)

    $conn = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    return $null -ne $conn
}

function Stop-PortOwner {
    param([int]$Port)

    $conn = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $conn) { return }

    $pid = $conn.OwningProcess
    if ($pid -and $pid -ne $PID) {
        try {
            Stop-Process -Id $pid -Force -ErrorAction Stop
            Write-Host "Stopped process $pid on port $Port"
        }
        catch {
            Write-Warning "Could not stop process $pid on port ${Port}: $($_.Exception.Message)"
        }
    }
}

function Resolve-MSBuildPath {
    $preferred = "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    if (Test-Path $preferred) {
        return $preferred
    }

    $fromPath = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $installPath = & $vswhere -latest -requires Microsoft.Component.MSBuild -property installationPath 2>$null
        if ($installPath) {
            $candidate = Join-Path $installPath "MSBuild\Current\Bin\MSBuild.exe"
            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }

    $known = @(
        "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    )

    foreach ($candidate in $known) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

$repoRoot = Get-RepoRoot
$apiProjectPath = Join-Path $repoRoot $ApiProject
$webPhysicalPath = Join-Path $repoRoot $WebPath
$webProjectPath = Join-Path $webPhysicalPath "AuthTest.Web.csproj"
$iisExpressExe = Join-Path $env:ProgramFiles "IIS Express\iisexpress.exe"

if (-not (Test-Path $apiProjectPath)) {
    throw "API project path not found: $apiProjectPath"
}

if (-not (Test-Path $webPhysicalPath)) {
    throw "WebForms path not found: $webPhysicalPath"
}

if (-not (Test-Path $webProjectPath)) {
    throw "WebForms project file not found: $webProjectPath"
}

if (-not (Test-Path $iisExpressExe)) {
    throw "IIS Express not found at: $iisExpressExe"
}

if ($KillPortOwners) {
    Stop-PortOwner -Port $ApiPort
    Stop-PortOwner -Port $WebPort
}

if (Test-PortInUse -Port $ApiPort) {
    throw "Port $ApiPort is already in use. Re-run with -KillPortOwners or change -ApiPort."
}

if (Test-PortInUse -Port $WebPort) {
    throw "Port $WebPort is already in use. Re-run with -KillPortOwners or change -WebPort."
}

if (-not $SkipWebBuild) {
    $msbuild = Resolve-MSBuildPath
    if (-not $msbuild) {
        throw "MSBuild not found. Install Visual Studio Build Tools or run with -SkipWebBuild."
    }

    Write-Host "Building WebForms project with MSBuild..."
    $buildArgs = @($webProjectPath, "/t:Build", "/p:Configuration=Debug", "/nologo")
    & $msbuild @buildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "WebForms build failed with exit code $LASTEXITCODE"
    }
}

$apiArgs = @("run", "--project", $apiProjectPath, "--urls", "http://localhost:$ApiPort")
$apiProcess = Start-Process -FilePath "dotnet" -ArgumentList $apiArgs -WorkingDirectory $repoRoot -PassThru

$webArgs = @("/path:$webPhysicalPath", "/port:$WebPort", "/clr:v4.0")
$webProcess = Start-Process -FilePath $iisExpressExe -ArgumentList $webArgs -WorkingDirectory $repoRoot -PassThru

Write-Host ""
Write-Host "Started API     PID: $($apiProcess.Id)  URL: http://localhost:$ApiPort"
Write-Host "Started Web     PID: $($webProcess.Id)  URL: http://localhost:$WebPort/Login.aspx"
Write-Host ""
Write-Host "To stop them later:"
Write-Host "  Stop-Process -Id $($apiProcess.Id),$($webProcess.Id)"
