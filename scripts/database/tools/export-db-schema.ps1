# Dumps the live schema (tables/columns) from DefaultConnection (see lookup order below) to docs/db-schema-export.txt.
# db.md at the repo root is hand-maintained: diff it against this export and update db.md (version, date, notes).
# Connection string lookup order: -ConnectionString, env ConnectionStrings__DefaultConnection,
# src/HRMBT.Web/appsettings.<Environment>.json (Environment = -Environment, $env:ASPNETCORE_ENVIRONMENT or Production).
param(
    [string]$ConnectionString,
    [string]$Environment
)
$ErrorActionPreference = 'Stop'
# Script lives in scripts/database/tools; repo root is three levels up
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\..\.."))
if (-not $Environment) { $Environment = $env:ASPNETCORE_ENVIRONMENT }
if (-not $Environment) { $Environment = 'Production' }
$cs = $ConnectionString
if (-not $cs) { $cs = $env:ConnectionStrings__DefaultConnection }
if (-not $cs) {
    $envFile = Join-Path $root "src/HRMBT.Web/appsettings.$Environment.json"
    if (Test-Path -LiteralPath $envFile) {
        $cs = (Get-Content -LiteralPath $envFile -Raw | ConvertFrom-Json).ConnectionStrings.DefaultConnection
    }
}
if (-not $cs) { throw "No connection string. Pass -ConnectionString, set ConnectionStrings__DefaultConnection, or create src/HRMBT.Web/appsettings.$Environment.json from its .example.json file." }

Add-Type -AssemblyName System.Data

function Format-SqlTypeName([string]$typename, [int]$maxlen, [byte]$precision, [byte]$scale) {
    $t = $typename.ToLowerInvariant()
    if ($t -in @('varchar', 'nvarchar', 'char', 'nchar')) {
        if ($maxlen -eq -1) { return "$typename(MAX)" }
        $len = if ($t.StartsWith('n')) { $maxlen / 2 } else { $maxlen }
        return "$typename($([int]$len))"
    }
    if ($t -eq 'decimal' -or $t -eq 'numeric') { return "$typename($precision,$scale)" }
    if ($t -eq 'float' -and $precision -ne 53) { return "$typename($precision)" }
    if ($t -eq 'datetimeoffset' -or $t -eq 'datetime2' -or $t -eq 'time') {
        if ($scale -gt 0) { return "$typename($scale)" }
        return $typename
    }
    return $typename
}

$conn = New-Object System.Data.SqlClient.SqlConnection($cs)
$query = @'
SELECT DB_NAME() AS DbName,
    SCHEMA_NAME(t.schema_id) AS SchemaName,
    t.name AS TableName,
    c.name AS ColumnName,
    TYPE_NAME(c.user_type_id) AS TypeName,
    c.max_length,
    c.precision,
    c.scale,
    c.is_nullable,
    c.column_id
FROM sys.columns c
INNER JOIN sys.tables t ON c.object_id = t.object_id
WHERE t.is_ms_shipped = 0
ORDER BY SchemaName, TableName, c.column_id
'@

$conn.Open()
try {
    $cmd = New-Object System.Data.SqlClient.SqlCommand($query, $conn)
    $reader = $cmd.ExecuteReader()
    $rows = New-Object System.Collections.Generic.List[object]
    while ($reader.Read()) {
        $rows.Add([ordered]@{
                DbName      = [string]$reader['DbName']
                SchemaName  = [string]$reader['SchemaName']
                TableName   = [string]$reader['TableName']
                ColumnName  = [string]$reader['ColumnName']
                TypeName    = [string]$reader['TypeName']
                max_length  = [int]$reader['max_length']
                precision   = [byte]$reader['precision']
                scale       = [byte]$reader['scale']
                is_nullable = [bool]$reader['is_nullable']
            })
    }
    $reader.Dispose()
}
finally {
    $conn.Dispose()
}

if ($rows.Count -eq 0) { throw 'No tables/columns returned (empty database?).' }

$stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
$db = $rows[0].DbName
$outPath = Join-Path $root 'docs/db-schema-export.txt'
$lines = New-Object System.Collections.Generic.List[string]
[void]$lines.Add("Database: $db")
[void]$lines.Add("Connection: DefaultConnection (environment: $Environment)")
[void]$lines.Add("Generated: $stamp")

$currentKey = $null
foreach ($r in $rows) {
    $key = "$($r.SchemaName)|$($r.TableName)"
    if ($key -ne $currentKey) {
        [void]$lines.Add('')
        [void]$lines.Add("[$($r.SchemaName)].[$($r.TableName)]")
        $currentKey = $key
    }
    $ftype = Format-SqlTypeName $r.TypeName $r.max_length $r.precision $r.scale
    $nullStr = if ($r.is_nullable) { 'YES' } else { 'NO' }
    [void]$lines.Add("  - $($r.ColumnName) ($ftype, Nullable: $nullStr)")
}

$content = ($lines | ForEach-Object { $_ }) -join "`r`n"
[System.IO.File]::WriteAllText($outPath, $content, [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $($rows.Count) columns to $outPath"
