<#
.SYNOPSIS
  Builds database-production.sql (schema + data) from the HRMS SQL Server database.

.DESCRIPTION
  Reads DefaultConnection from src/HRMBT.Web/appsettings.json unless -ConnectionString is set.
  Schema: database-schema.sql + database-schema-patches.sql from repo root.
  Data: INSERT statements for all dbo tables (FK-safe order, IDENTITY_INSERT where needed).

.EXAMPLE
  .\tools\export-database-production.ps1
  .\tools\export-database-production.ps1 -ConnectionString "Server=.;Database=HRMS;Trusted_Connection=True;TrustServerCertificate=True"
  .\tools\export-database-production.ps1 -SchemaOnly
  .\tools\export-database-production.ps1 -DataOnly -OutputPath .\database-data.sql
#>
[CmdletBinding()]
param(
    [string]$ConnectionString,
    [string]$OutputPath,
    [string]$DatabaseName,
    [switch]$SchemaOnly,
    [switch]$DataOnly,
    [string[]]$ExcludeTables = @()
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$appsettings = Join-Path $repoRoot "src\HRMBT.Web\appsettings.json"

if (-not $ConnectionString) {
    if (-not (Test-Path $appsettings)) { throw "appsettings.json not found: $appsettings" }
    $json = Get-Content -Raw -LiteralPath $appsettings | ConvertFrom-Json
    $ConnectionString = $json.ConnectionStrings.DefaultConnection
}
if (-not $ConnectionString) { throw "No connection string. Pass -ConnectionString or set DefaultConnection in appsettings.json." }

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot "database-production.sql"
}

function Parse-ConnectionStringValue([string]$cs, [string]$key) {
    foreach ($part in ($cs -split ';')) {
        $p = $part.Trim()
        if ($p -match '^(?i)' + [regex]::Escape($key) + '\s*=\s*(.+)$') { return $Matches[1].Trim() }
    }
    return $null
}

if (-not $DatabaseName) {
    $DatabaseName = Parse-ConnectionStringValue $ConnectionString 'Initial Catalog'
    if (-not $DatabaseName) { $DatabaseName = Parse-ConnectionStringValue $ConnectionString 'Database' }
    if (-not $DatabaseName) { $DatabaseName = 'HRMS' }
}

function Escape-SqlString([string]$s, [bool]$unicode) {
    if ($null -eq $s) { return "NULL" }
    $escaped = $s.Replace("'", "''")
    if ($unicode) { return "N'$escaped'" }
    return "'$escaped'"
}

function Format-SqlValue($value, [string]$dataType) {
    if ($null -eq $value -or [DBNull]::Value.Equals($value)) { return "NULL" }
    $dt = $dataType.ToLowerInvariant()
    switch -Regex ($dt) {
        '^(bit)$' {
            if ($value -is [bool]) { return $(if ($value) { '1' } else { '0' }) }
            return $(if ([int]$value -ne 0) { '1' } else { '0' })
        }
        '^(tinyint|smallint|int|bigint|decimal|numeric|float|real|money|smallmoney)$' { return $value.ToString([System.Globalization.CultureInfo]::InvariantCulture) }
        '^(date|datetime|datetime2|smalldatetime|datetimeoffset|time)$' {
            if ($value -is [TimeSpan]) { return "'" + $value.ToString("c").Substring(1) + "'" }
            $d = [datetime]$value
            if ($dt -eq 'date') { return "'" + $d.ToString("yyyy-MM-dd") + "'" }
            return "'" + $d.ToString("yyyy-MM-dd HH:mm:ss.fff") + "'"
        }
        '^(uniqueidentifier)$' { return "'" + $value.ToString() + "'" }
        '^(varbinary|binary|image|timestamp|rowversion)$' {
            $bytes = [byte[]]$value
            if ($bytes.Length -eq 0) { return "0x" }
            return "0x" + ([BitConverter]::ToString($bytes) -replace '-', '')
        }
        default {
            $isUnicode = $dt.StartsWith('n')
            return Escape-SqlString ([string]$value) $isUnicode
        }
    }
}

function Get-TablesInDependencyOrder([System.Data.SqlClient.SqlConnection]$conn) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = @"
SELECT t.name AS TableName
FROM sys.tables t
WHERE SCHEMA_NAME(t.schema_id) = N'dbo'
  AND t.is_ms_shipped = 0
ORDER BY t.name
"@
    $all = New-Object System.Collections.Generic.List[string]
    $da = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
    $dt = New-Object System.Data.DataTable
    [void]$da.Fill($dt)
    foreach ($row in $dt.Rows) { [void]$all.Add([string]$row.TableName) }

    $cmd.CommandText = @"
SELECT OBJECT_NAME(fk.parent_object_id) AS ChildTable,
       OBJECT_NAME(fk.referenced_object_id) AS ParentTable
FROM sys.foreign_keys fk
WHERE SCHEMA_NAME(fk.schema_id) = N'dbo'
"@
    $fkDt = New-Object System.Data.DataTable
    [void](New-Object System.Data.SqlClient.SqlDataAdapter $cmd).Fill($fkDt)

    $deps = @{}
    foreach ($t in $all) { $deps[$t] = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase) }
    foreach ($row in $fkDt.Rows) {
        $child = [string]$row.ChildTable
        $parent = [string]$row.ParentTable
        if ($deps.ContainsKey($child) -and $deps.ContainsKey($parent) -and $child -ne $parent) {
            [void]$deps[$child].Add($parent)
        }
    }

    $sorted = New-Object System.Collections.Generic.List[string]
    $visited = @{}
    $visiting = @{}

    function Visit([string]$name) {
        if ($visiting.ContainsKey($name)) { return }
        if ($visited.ContainsKey($name)) { return }
        $visiting[$name] = $true
        foreach ($p in $deps[$name]) { Visit $p }
        $visiting.Remove($name) | Out-Null
        $visited[$name] = $true
        [void]$sorted.Add($name)
    }

    foreach ($t in ($all | Sort-Object)) { Visit $t }
    return $sorted
}

function Get-TableColumns([System.Data.SqlClient.SqlConnection]$conn, [string]$table) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = @"
SELECT c.name AS ColumnName,
       TYPE_NAME(c.user_type_id) AS DataType,
       c.is_identity AS IsIdentity,
       c.column_id
FROM sys.columns c
WHERE c.object_id = OBJECT_ID(N'dbo.' + QUOTENAME(@t, ']').Value)
ORDER BY c.column_id
"@
    # Fix QUOTENAME usage - use parameter
    $cmd.Parameters.AddWithValue("@t", $table) | Out-Null
    $cmd.CommandText = @"
SELECT c.name AS ColumnName,
       TYPE_NAME(c.user_type_id) AS DataType,
       c.is_identity AS IsIdentity
FROM sys.columns c
INNER JOIN sys.tables t ON c.object_id = t.object_id
WHERE SCHEMA_NAME(t.schema_id) = N'dbo'
  AND t.name = @t
  AND c.name IS NOT NULL
  AND LEN(LTRIM(RTRIM(c.name))) > 0
ORDER BY c.column_id
"@
    $dt = New-Object System.Data.DataTable
    [void](New-Object System.Data.SqlClient.SqlDataAdapter $cmd).Fill($dt)
    return $dt
}

function Export-TableData {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [System.Text.StringBuilder]$Sb,
        [string]$Table
    )
    $meta = Get-TableColumns $Connection $Table
    if ($meta.Rows.Count -eq 0) { return 0 }

    $hasIdentity = ($meta.Rows | Where-Object { $_.IsIdentity -eq $true }).Count -gt 0

    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = "SELECT * FROM [dbo].[$Table]"
    $reader = $cmd.ExecuteReader([System.Data.CommandBehavior]::SchemaOnly)
    $fieldCount = $reader.FieldCount
    $colNames = New-Object string[] $fieldCount
    $colTypes = New-Object string[] $fieldCount
    for ($i = 0; $i -lt $fieldCount; $i++) {
        $name = $reader.GetName($i)
        if ([string]::IsNullOrWhiteSpace($name)) {
            $reader.Close()
            throw "Table [$Table] has an unnamed column at index $i."
        }
        $colNames[$i] = $name
        $colTypes[$i] = $reader.GetDataTypeName($i)
    }
    $reader.Close()

    $quotedCols = ($colNames | ForEach-Object { "[" + ($_ -replace '\]', ']]') + "]" }) -join ", "
    $cmd.CommandText = "SELECT * FROM [dbo].[$Table]"
    $reader = $cmd.ExecuteReader()

    $count = 0
    if ($hasIdentity) {
        [void]$Sb.AppendLine("SET IDENTITY_INSERT [dbo].[$Table] ON;")
        [void]$Sb.AppendLine("GO")
    }

    while ($reader.Read()) {
        $vals = New-Object System.Collections.Generic.List[string]
        for ($i = 0; $i -lt $reader.FieldCount; $i++) {
            [void]$vals.Add((Format-SqlValue $reader.GetValue($i) $colTypes[$i]))
        }
        [void]$Sb.AppendLine("INSERT INTO [dbo].[$Table] ($quotedCols) VALUES ($($vals -join ', '));")
        $count++
    }
    $reader.Close()

    if ($hasIdentity) {
        [void]$Sb.AppendLine("SET IDENTITY_INSERT [dbo].[$Table] OFF;")
        [void]$Sb.AppendLine("GO")
    }
    return $count
}

$schemaFile = Join-Path $repoRoot "database-schema.sql"
$patchFile = Join-Path $repoRoot "database-schema-patches.sql"
if (-not $DataOnly) {
    if (-not (Test-Path $schemaFile)) { throw "Missing $schemaFile" }
}

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("/*")
[void]$sb.AppendLine(" HRMS production database script")
[void]$sb.AppendLine(" Database: $DatabaseName")
[void]$sb.AppendLine(" Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
[void]$sb.AppendLine(" Source: tools/export-database-production.ps1")
[void]$sb.AppendLine("")
[void]$sb.AppendLine(" Run on SQL Server (sqlcmd or SSMS):")
[void]$sb.AppendLine("   sqlcmd -S YourServer -d master -i database-production.sql")
[void]$sb.AppendLine(" Or open in SSMS and execute against the target instance.")
[void]$sb.AppendLine("*/")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("SET NOCOUNT ON;")
[void]$sb.AppendLine("SET ANSI_NULLS ON;")
[void]$sb.AppendLine("SET QUOTED_IDENTIFIER ON;")
[void]$sb.AppendLine("SET XACT_ABORT ON;")
[void]$sb.AppendLine("GO")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("IF DB_ID(N'$DatabaseName') IS NULL")
[void]$sb.AppendLine("BEGIN")
[void]$sb.AppendLine("    CREATE DATABASE [$DatabaseName];")
[void]$sb.AppendLine("END")
[void]$sb.AppendLine("GO")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("USE [$DatabaseName];")
[void]$sb.AppendLine("GO")
[void]$sb.AppendLine("")

if (-not $DataOnly) {
    [void]$sb.AppendLine("/* ========== SCHEMA (database-schema.sql) ========== */")
    [void]$sb.AppendLine("GO")
    [void]$sb.Append((Get-Content -Raw -LiteralPath $schemaFile))
    [void]$sb.AppendLine("")
    if (Test-Path $patchFile) {
        [void]$sb.AppendLine("/* ========== SCHEMA PATCHES ========== */")
        [void]$sb.AppendLine("GO")
        [void]$sb.Append((Get-Content -Raw -LiteralPath $patchFile))
    }
    [void]$sb.AppendLine("")
}

if (-not $SchemaOnly) {
  $conn = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
  $conn.Open()
  try {
    $tables = Get-TablesInDependencyOrder $conn
    $excludeSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($e in $ExcludeTables) { [void]$excludeSet.Add($e) }

    [void]$sb.AppendLine("/* ========== DATA ========== */")
    [void]$sb.AppendLine("GO")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("/* Disable FK checks while loading */")
    [void]$sb.AppendLine("EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';")
    [void]$sb.AppendLine("GO")
    [void]$sb.AppendLine("")

    $totalRows = 0
    foreach ($table in $tables) {
        if ($excludeSet.Contains($table)) {
            [void]$sb.AppendLine("-- Skipped table: $table")
            continue
        }
        [void]$sb.AppendLine("-- Table: [dbo].[$table]")
        try {
            $rows = Export-TableData -Connection $conn -Sb $sb -Table $table
        }
        catch {
            throw "Failed exporting table [$table]: $($_.Exception.Message)"
        }
        $totalRows += $rows
        [void]$sb.AppendLine("-- Rows: $rows")
        [void]$sb.AppendLine("GO")
        [void]$sb.AppendLine("")
        Write-Host "  $table : $rows rows"
    }

    [void]$sb.AppendLine("/* Re-enable FK checks */")
    [void]$sb.AppendLine("EXEC sp_msforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';")
    [void]$sb.AppendLine("GO")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("/* Total data rows scripted: $totalRows */")

    Write-Host "Data export complete: $totalRows rows across $($tables.Count) tables."
  }
  finally {
    $conn.Close()
  }
}

$text = $sb.ToString()
# Normalize line endings for SQL tools
$text = $text -replace "`r`n", "`n" -replace "`n", "`r`n"
[System.IO.File]::WriteAllText($OutputPath, $text, [System.Text.UTF8Encoding]::new($true))
Write-Host "OK: wrote $OutputPath ($([math]::Round((Get-Item $OutputPath).Length / 1MB, 2)) MB)"
