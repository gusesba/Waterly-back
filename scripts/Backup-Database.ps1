param([Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
$composeFile = Join-Path (Split-Path $PSScriptRoot -Parent) 'compose.yaml'
$destination = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $destination) { throw "Backup destination already exists: $destination" }
$parent = Split-Path $destination -Parent
New-Item -ItemType Directory -Force -Path $parent | Out-Null
$containerFile = "/tmp/waterly-backup-$([Guid]::NewGuid().ToString('N')).dump"
try {
    & docker compose -f $composeFile exec -T postgres pg_dump -U waterly -d waterly --format=custom --no-owner "--file=$containerFile"
    if ($LASTEXITCODE -ne 0) { throw 'pg_dump failed.' }
    & docker compose -f $composeFile cp "postgres:$containerFile" $destination
    if ($LASTEXITCODE -ne 0) { throw 'Copying the backup failed.' }
    Get-Item -LiteralPath $destination
} finally {
    & docker compose -f $composeFile exec -T postgres rm -f $containerFile
}
