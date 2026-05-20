# One-off: regenerate db.txt from SQL Server DefaultConnection in appsettings.json
$ErrorActionPreference = 'Stop'
# Script lives in repo /tools; repo root is one level up
$root = Split-Path $PSScriptRoot -Parent
$appsettings = Join-Path $root 'src/HRMBT.Web/appsettings.json'
$cfg = Get-Content $appsettings -Raw | ConvertFrom-Json
$cs = $cfg.ConnectionStrings.DefaultConnection
if (-not $cs) { throw "DefaultConnection missing in appsettings.json" }

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
$outPath = Join-Path $root 'db.txt'
$lines = New-Object System.Collections.Generic.List[string]
[void]$lines.Add("Database: $db")
[void]$lines.Add('Connection: DefaultConnection (src/HRMBT.Web/appsettings.json)')
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
