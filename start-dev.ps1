param(
    [int]$ApiPort = 5000,
    [int]$WebPort = 8081,
    [string]$WebHost = "localhost",
    [string]$WebAltHost = "test.joho",
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

    $conn = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Where-Object { $_.OwningProcess -ne 4 } |
        Select-Object -First 1
    return $null -ne $conn
}

function Stop-PortOwner {
    param([int]$Port)

    $conn = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $conn) { return }

    $owningProcessId = $conn.OwningProcess
    if ($owningProcessId -eq 4) {
        return
    }

    if ($owningProcessId -and $owningProcessId -ne $PID) {
        try {
            Stop-Process -Id $owningProcessId -Force -ErrorAction Stop
            Write-Host "Stopped process $owningProcessId on port $Port"
        }
        catch {
            Write-Warning "Could not stop process $owningProcessId on port ${Port}: $($_.Exception.Message)"
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

function Resolve-IisTemplateConfigPath {
    $candidate = Join-Path $env:ProgramFiles "IIS Express\AppServer\applicationhost.config"
    if (Test-Path $candidate) {
        return $candidate
    }

    throw "IIS Express template applicationhost.config not found at: $candidate"
}

function Ensure-IisExpressConfig {
    param(
        [string]$RepoRoot,
        [string]$WebPhysicalPath,
        [int]$WebPort,
        [string]$PrimaryHost,
        [string]$SecondaryHost
    )

    $iisFolder = Join-Path $RepoRoot ".iisexpress"
    if (-not (Test-Path $iisFolder)) {
        New-Item -ItemType Directory -Path $iisFolder | Out-Null
    }

    $configPath = Join-Path $iisFolder "applicationhost.config"
    if (-not (Test-Path $configPath)) {
        Copy-Item -Path (Resolve-IisTemplateConfigPath) -Destination $configPath -Force
    }

    [xml]$config = Get-Content -Path $configPath -Raw
    $sites = $config.SelectSingleNode("/configuration/system.applicationHost/sites")
    if ($null -eq $sites) {
        throw "Invalid IIS Express config: system.applicationHost/sites section missing in $configPath"
    }

    $siteName = "AuthTest.Web"
    $site = $sites.SelectSingleNode("site[@name='$siteName']")
    if ($null -eq $site) {
        $maxId = 1
        foreach ($existing in $sites.SelectNodes("site")) {
            $existingId = 0
            if ([int]::TryParse([string]$existing.Attributes["id"].Value, [ref]$existingId) -and $existingId -ge $maxId) {
                $maxId = $existingId + 1
            }
        }

        $site = $config.CreateElement("site")
        $site.SetAttribute("name", $siteName)
        $site.SetAttribute("id", [string]$maxId)
        $site.SetAttribute("serverAutoStart", "true")

        $application = $config.CreateElement("application")
        $application.SetAttribute("path", "/")

        $virtualDirectory = $config.CreateElement("virtualDirectory")
        $virtualDirectory.SetAttribute("path", "/")
        $virtualDirectory.SetAttribute("physicalPath", $WebPhysicalPath)
        $null = $application.AppendChild($virtualDirectory)
        $null = $site.AppendChild($application)

        $bindings = $config.CreateElement("bindings")
        $null = $site.AppendChild($bindings)

        $null = $sites.AppendChild($site)
    }

    $applicationNode = $site.SelectSingleNode("application[@path='/']")
    if ($null -eq $applicationNode) {
        $applicationNode = $config.CreateElement("application")
        $applicationNode.SetAttribute("path", "/")
        $null = $site.AppendChild($applicationNode)
    }

    $virtualDirectoryNode = $applicationNode.SelectSingleNode("virtualDirectory[@path='/']")
    if ($null -eq $virtualDirectoryNode) {
        $virtualDirectoryNode = $config.CreateElement("virtualDirectory")
        $virtualDirectoryNode.SetAttribute("path", "/")
        $null = $applicationNode.AppendChild($virtualDirectoryNode)
    }
    $virtualDirectoryNode.SetAttribute("physicalPath", $WebPhysicalPath)

    $bindingsNode = $site.SelectSingleNode("bindings")
    if ($null -eq $bindingsNode) {
        $bindingsNode = $config.CreateElement("bindings")
        $null = $site.AppendChild($bindingsNode)
    }

    while ($bindingsNode.HasChildNodes) {
        $null = $bindingsNode.RemoveChild($bindingsNode.FirstChild)
    }

    $hostCandidates = @($PrimaryHost, $SecondaryHost) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique
    foreach ($bindingHost in $hostCandidates) {
        $httpsBinding = $config.CreateElement("binding")
        $httpsBinding.SetAttribute("protocol", "https")
        $httpsBinding.SetAttribute("bindingInformation", "*:${WebPort}:$bindingHost")
        $null = $bindingsNode.AppendChild($httpsBinding)
    }

    $config.Save($configPath)
    return $configPath
}

function Test-UrlAclExists {
    param([string]$Url)

    $output = (& netsh http show urlacl url=$Url 2>$null | Out-String)
    return -not [string]::IsNullOrWhiteSpace($output) -and $output -match "https://"
}

function Test-SslHostBindingExists {
    param(
        [string]$HostName,
        [int]$Port
    )

    $hostPort = "$HostName`:$Port"
    $output = (& netsh http show sslcert hostnameport=$hostPort 2>$null | Out-String)
    return -not [string]::IsNullOrWhiteSpace($output) -and $output -match "[0-9A-Fa-f]{40}"
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

foreach ($requiredHost in @($WebHost, $WebAltHost) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique) {
    $requiredUrl = "https://${requiredHost}:$WebPort/"
    $hasAcl = Test-UrlAclExists -Url $requiredUrl
    $hasSsl = Test-SslHostBindingExists -HostName $requiredHost -Port $WebPort
    if (-not $hasAcl -or -not $hasSsl) {
        throw "Missing HTTP.SYS HTTPS bootstrap for $requiredUrl. Run an elevated PowerShell once: .\\setup-testjoho-https.ps1"
    }
}

$apiArgs = @("run", "--project", $apiProjectPath, "--urls", "http://localhost:$ApiPort")
$apiProcess = Start-Process -FilePath "dotnet" -ArgumentList $apiArgs -WorkingDirectory $repoRoot -PassThru

$iisConfigPath = Ensure-IisExpressConfig -RepoRoot $repoRoot -WebPhysicalPath $webPhysicalPath -WebPort $WebPort -PrimaryHost $WebHost -SecondaryHost $WebAltHost

$webArgs = @("/config:$iisConfigPath", "/site:AuthTest.Web", "/systray:false")
$webProcess = Start-Process -FilePath $iisExpressExe -ArgumentList $webArgs -WorkingDirectory $repoRoot -PassThru

Write-Host ""
Write-Host "Started API     PID: $($apiProcess.Id)  URL: http://localhost:$ApiPort"
Write-Host "Started Web     PID: $($webProcess.Id)  URL: https://${WebHost}:$WebPort/Login.aspx"
Write-Host "Started Web Alt URL: https://${WebAltHost}:$WebPort/Login.aspx"
Write-Host ""
Write-Host "To stop them later:"
Write-Host "  Stop-Process -Id $($apiProcess.Id),$($webProcess.Id)"
