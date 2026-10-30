[CmdletBinding()]
param(
    [string]$ConfigurationPath = '',
    [string]$OutputDirectory = '',
    [switch]$CleanupProbe
)

# Không nhận BaseUrl hay lời xác nhận IsolatedTestDatabase từ caller.
# Harness sở hữu DB ngẫu nhiên, API loopback riêng, secret tổng hợp và cleanup.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
if (!$ConfigurationPath) { $ConfigurationPath = Join-Path $root 'backend/src/Dental.Api/appsettings.Development.json' }
$ConfigurationPath = (Resolve-Path -LiteralPath $ConfigurationPath).Path
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root 'docs/testing' }
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$runId = [Guid]::NewGuid().ToString('N')
$started = [DateTimeOffset]::UtcNow
$reportPath = Join-Path $OutputDirectory ('FIVE_ROLE_' + $started.ToString('yyyyMMdd-HHmmss') + '_' + $runId.Substring(0,8) + '.json')
$checks = [Collections.Generic.List[object]]::new()
$ownedDatabaseRemoved = $false
$exitCode = 1
$previousDirectory = Get-Location
try {
    Set-Location -LiteralPath $root
    $mode = if ($CleanupProbe) { '--five-role-cleanup-probe' } else { '--five-role-smoke-only' }
    # Nội dung stderr/API không đưa vào báo cáo: có thể chứa credential hoặc dữ liệu khi test thất bại.
    & dotnet run --project backend/tests/CashPayment.Postgres --no-restore -p:UseAppHost=false -- --isolated-config $ConfigurationPath $mode 2>&1 | ForEach-Object {
        $line = "$_"
        if ($line -match '^(PASS|FAIL): (.+)$') {
            $checks.Add([pscustomobject]@{check=$Matches[2];passed=($Matches[1] -eq 'PASS')})
            Write-Host $line
        } elseif ($line -eq 'Owned isolated test database removed') {
            $ownedDatabaseRemoved = $true
            Write-Host $line
        }
    }
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 0 -and (!$ownedDatabaseRemoved -or $checks.Count -eq 0 -or @($checks | Where-Object { !$_.passed }).Count -gt 0)) { $exitCode = 1 }
} finally {
    Set-Location -LiteralPath $previousDirectory
    $pass = @($checks | Where-Object passed).Count
    [pscustomobject]@{
        runId=$runId; startedAtUtc=$started.ToString('o'); endedAtUtc=[DateTimeOffset]::UtcNow.ToString('o')
        mode= $(if ($CleanupProbe) {'cleanup-probe'} else {'five-role'})
        exitCode=$exitCode; total=$checks.Count; passed=$pass; failed=($checks.Count-$pass)
        databaseRemoved=$ownedDatabaseRemoved; completed=($exitCode -eq 0); checks=$checks
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding utf8
    Write-Host "Checks: total=$($checks.Count), pass=$pass, fail=$($checks.Count-$pass); exit=$exitCode; databaseRemoved=$ownedDatabaseRemoved"
    Write-Host "Report: $reportPath"
}
if ($exitCode -ne 0) { throw 'Five-role smoke did not complete. Read the sanitized report; no merge readiness is implied.' }
