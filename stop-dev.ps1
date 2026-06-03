param(
    [int]$ApiPort = 5000,
    [int]$WebPort = 8081
)

$ErrorActionPreference = "Stop"

function Stop-ListenerOnPort {
    param([int]$Port)

    $listeners = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique

    if (-not $listeners) {
        Write-Host "No listener found on port $Port"
        return
    }

    foreach ($procId in $listeners) {
        if ($procId -eq $PID) { continue }

        try {
            $proc = Get-Process -Id $procId -ErrorAction Stop
            Stop-Process -Id $procId -Force -ErrorAction Stop
            Write-Host "Stopped $($proc.ProcessName) (PID: $procId) on port $Port"
        }
        catch {
            Write-Warning "Could not stop PID $procId on port ${Port}: $($_.Exception.Message)"
        }
    }
}

Stop-ListenerOnPort -Port $ApiPort
Stop-ListenerOnPort -Port $WebPort

Write-Host "Stop script finished."
