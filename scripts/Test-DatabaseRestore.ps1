param([string]$BackupDirectory = (Join-Path ([IO.Path]::GetTempPath()) 'waterly-restore-check'))
$ErrorActionPreference = 'Stop'
$composeFile = Join-Path (Split-Path $PSScriptRoot -Parent) 'compose.yaml'
$runId = [Guid]::NewGuid().ToString('N')
$target = "waterly_restore_$runId"
$backup = Join-Path ([IO.Path]::GetFullPath($BackupDirectory)) "$runId.dump"
$query = @'
SELECT json_build_object(
 'migrations', (SELECT count(*) FROM "__EFMigrationsHistory"),
 'accounts', (SELECT count(*) FROM "AspNetUsers"),
 'drinks', (SELECT count(*) FROM "DrinkEntries"),
 'dropsEntries', (SELECT count(*) FROM "DropsLedgerEntries"),
 'dropsAmount', (SELECT coalesce(sum("Amount"),0) FROM "DropsLedgerEntries"),
 'prestigeEntries', (SELECT count(*) FROM "PrestigeLedgerEntries"),
 'prestigeAmount', (SELECT coalesce(sum("Amount"),0) FROM "PrestigeLedgerEntries"),
 'contestResults', (SELECT count(*) FROM "ContestResults"));
'@
& "$PSScriptRoot/Backup-Database.ps1" -OutputPath $backup | Out-Null
# Restore first. Comparing a live database with a snapshot requires quiescent writes.
$created = $false
try {
    & "$PSScriptRoot/Restore-Database.ps1" -BackupPath $backup -TargetDatabase $target | Out-Null
    $created = $true
    $sourceSummary = & docker compose -f $composeFile exec -T postgres psql -U waterly -d waterly -At -v ON_ERROR_STOP=1 -c $query
    if ($LASTEXITCODE -ne 0) { throw 'Could not read source summary.' }
    $restoredSummary = & docker compose -f $composeFile exec -T postgres psql -U waterly -d $target -At -v ON_ERROR_STOP=1 -c $query
    if ($LASTEXITCODE -ne 0) { throw 'Could not read restored summary.' }
    if (($sourceSummary -join '') -ne ($restoredSummary -join '')) { throw 'Source and restore differ. Retry with application writes paused.' }
    [PSCustomObject]@{ Verified = $true; Backup = $backup; Summary = ($restoredSummary -join '') }
} finally {
    if ($created -and $target -eq "waterly_restore_$runId" -and $target -match '^waterly_restore_[a-f0-9]{32}$') {
        & docker compose -f $composeFile exec -T postgres dropdb -U waterly --maintenance-db=postgres $target
        if ($LASTEXITCODE -ne 0) { Write-Warning "Could not remove disposable database $target." }
    }
}
