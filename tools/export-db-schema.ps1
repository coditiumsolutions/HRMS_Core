$ErrorActionPreference = "Stop"
$appsettings = Join-Path $PSScriptRoot "..\src\HRMBT.Web\appsettings.json" | Resolve-Path
$json = Get-Content -Raw -LiteralPath $appsettings | ConvertFrom-Json
$cs = $json.ConnectionStrings.DefaultConnection
if (-not $cs) { throw "DefaultConnection not found in appsettings.json" }

$outPath = Join-Path $PSScriptRoot "..\db.txt" | Resolve-Path

function Get-SqlDisplayType {
    param($r)
    $t = [string]$r.DataType
    if ($t -in @("nvarchar", "nchar", "varchar", "char", "varbinary", "binary")) {
        $ml = [int]$r.max_length
        if ($ml -lt 0) { return "$t(max)" }
        $chars = if ($t -like "n*") { [int]($ml / 2) } else { $ml }
        return "$t($chars)"
    }
    if ($t -in @("decimal", "numeric")) {
        return "$t($($r.precision),$($r.scale))"
    }
    if ($t -eq "float") {
        if ([int]$r.precision -eq 53) { return "float" }
        return "float($($r.precision))"
    }
    return $t
}

$conn = New-Object System.Data.SqlClient.SqlConnection($cs)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
SELECT t.name AS TableName, c.column_id, c.name AS ColumnName,
  TYPE_NAME(c.user_type_id) AS DataType, c.max_length, c.precision, c.scale, c.is_nullable
FROM sys.tables t
INNER JOIN sys.columns c ON c.object_id = t.object_id
WHERE SCHEMA_NAME(t.schema_id) = N'dbo'
ORDER BY t.name, c.column_id
"@
$da = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
$dt = New-Object System.Data.DataTable
[void]$da.Fill($dt)

$c2 = $conn.CreateCommand()
$c2.CommandText = "SELECT DB_NAME()"
$dbName = [string]$c2.ExecuteScalar()
$conn.Close()

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("Database: $dbName")
[void]$sb.AppendLine("Connection: DefaultConnection (src/HRMBT.Web/appsettings.json)")
[void]$sb.AppendLine("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
[void]$sb.AppendLine("")

$groups = $dt | Group-Object TableName
foreach ($g in $groups) {
    [void]$sb.AppendLine("[dbo].[$($g.Name)]")
    foreach ($row in $g.Group) {
        $typ = Get-SqlDisplayType $row
        $nullStr = if ($row.is_nullable) { "YES" } else { "NO" }
        [void]$sb.AppendLine("  - $($row.ColumnName) ($typ, Nullable: $nullStr)")
    }
    [void]$sb.AppendLine("")
}

$text = ($sb.ToString().TrimEnd() + "`r`n")
[System.IO.File]::WriteAllText($outPath, $text, [System.Text.UTF8Encoding]::new($false))
Write-Host "OK: $($groups.Count) tables, $($dt.Rows.Count) columns -> $outPath"
