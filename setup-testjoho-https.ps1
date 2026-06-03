#requires -RunAsAdministrator

param(
    [int]$Port = 8081,
    [string]$PrimaryHost = "localhost",
    [string]$SecondaryHost = "test.joho"
)

$ErrorActionPreference = "Stop"

function Ensure-HostsEntry {
    param(
        [string]$HostName
    )

    $hostsPath = Join-Path $env:SystemRoot "System32\drivers\etc\hosts"
    $existing = Get-Content -Path $hostsPath -ErrorAction Stop
    $pattern = "(^|\s)$([regex]::Escape($HostName))(\s|$)"
    $hasEntry = $false

    foreach ($line in $existing) {
        if ($line -match "^\s*#") { continue }
        if ($line -match $pattern) {
            $hasEntry = $true
            break
        }
    }

    if (-not $hasEntry) {
        Add-Content -Path $hostsPath -Value "127.0.0.1 $HostName"
        Write-Host "Added hosts entry: 127.0.0.1 $HostName"
    }
    else {
        Write-Host "Hosts entry already present for $HostName"
    }
}

function Ensure-Certificate {
    param(
        [string]$HostName
    )

    $cert = Get-ChildItem Cert:\LocalMachine\My |
        Where-Object { $_.Subject -eq "CN=$HostName" } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

    if ($null -eq $cert) {
        $cert = New-SelfSignedCertificate `
            -DnsName $HostName `
            -CertStoreLocation "Cert:\LocalMachine\My" `
            -FriendlyName "AuthTest Dev $HostName" `
            -NotAfter (Get-Date).AddYears(5)
        Write-Host "Created self-signed certificate for $HostName ($($cert.Thumbprint))"
    }
    else {
        Write-Host "Using existing certificate for $HostName ($($cert.Thumbprint))"
    }

    $trusted = Get-ChildItem Cert:\LocalMachine\Root |
        Where-Object { $_.Thumbprint -eq $cert.Thumbprint } |
        Select-Object -First 1

    if ($null -eq $trusted) {
        $exportPath = Join-Path $env:TEMP "$HostName.cer"
        Export-Certificate -Cert $cert -FilePath $exportPath -Type CERT | Out-Null
        Import-Certificate -FilePath $exportPath -CertStoreLocation "Cert:\LocalMachine\Root" | Out-Null
        Remove-Item -Path $exportPath -Force -ErrorAction SilentlyContinue
        Write-Host "Trusted certificate in LocalMachine\\Root"
    }
    else {
        Write-Host "Certificate already trusted in LocalMachine\\Root"
    }

    return $cert
}

function Ensure-UrlAcl {
    param(
        [string]$Url
    )

    $currentUser = "$env:USERDOMAIN\$env:USERNAME"
    & netsh http delete urlacl url=$Url | Out-Null
    & netsh http add urlacl url=$Url user=$currentUser | Out-Null
    Write-Host "Configured URL ACL for $Url ($currentUser)"
}

function Ensure-SslBinding {
    param(
        [string]$HostName,
        [int]$Port,
        [string]$Thumbprint
    )

    $hostPort = "$HostName`:$Port"
    $appId = "{9c9387f5-8b42-4e1e-bf35-1554af6abf8c}"

    & netsh http delete sslcert hostnameport=$hostPort | Out-Null
    & netsh http add sslcert hostnameport=$hostPort certhash=$Thumbprint certstorename=MY appid=$appId | Out-Null
    Write-Host "Configured SSL binding for $hostPort"
}

$hosts = @($PrimaryHost, $SecondaryHost) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique

foreach ($hostName in $hosts) {
    if ($hostName -ne "localhost") {
        Ensure-HostsEntry -HostName $hostName
    }

    $cert = Ensure-Certificate -HostName $hostName
    Ensure-UrlAcl -Url "https://${hostName}:$Port/"
    Ensure-SslBinding -HostName $hostName -Port $Port -Thumbprint $cert.Thumbprint
}

Write-Host ""
Write-Host "Bootstrap complete. You can now run: .\\start-dev.ps1 -SkipWebBuild -KillPortOwners"