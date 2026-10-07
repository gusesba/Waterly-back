param(
    [Parameter(Mandatory)][string]$BackupPath,
    [string]$TargetDatabase = "waterly_restore_$([Guid]::NewGuid().ToString('N'))"
)
$ErrorActionPreference = 'Stop'
$composeFile = Join-Path (Split-Path $PSScriptRoot -Parent) 'compose.yaml'
$backup = (Resolve-Path -LiteralPath $BackupPath).Path
if ($TargetDatabase -notmatch '^waterly_restore_[a-z0-9_]{1,40}$') {
    throw 'Restore target must be a new disposable database named waterly_restore_<suffix>.'
}
$containerFile = "/tmp/waterly-restore-$([Guid]::NewGuid().ToString('N')).dump"
try {
    & docker compose -f $composeFile exec -T postgres createdb -U waterly --maintenance-db=postgres $TargetDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Could not create a new restore database. Existing databases are never overwritten.' }
    & docker compose -f $composeFile cp $backup "postgres:$containerFile"
    if ($LASTEXITCODE -ne 0) { throw 'Copying the backup failed.' }
    & docker compose -f $composeFile exec -T postgres pg_restore -U waterly "--dbname=$TargetDatabase" --no-owner --exit-on-error $containerFile
    if ($LASTEXITCODE -ne 0) { throw "Restore failed; inspect disposable database $TargetDatabase." }
    [PSCustomObject]@{ Database = $TargetDatabase; Backup = $backup; Restored = $true }
} finally {
    & docker compose -f $composeFile exec -T postgres rm -f $containerFile
}
