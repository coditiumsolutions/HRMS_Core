<#
.SYNOPSIS
  Fills dbo.EmployeeDocuments.FileHash (SHA-256) for rows that were uploaded before the column existed.

.DESCRIPTION
  The redesigned schema (docs/db/db_changes.md) has a unique index on (EmployeeId, FileHash) for live documents.
  Rows with FileHash NULL are ignored by that index and by the upload duplicate check, so run this once after the
  schema is applied, on the server that holds the files.

  - Files that are not on disk are listed and skipped (restore them from the uploads backup first).
  - Two live documents of one employee with identical content are reported and the second one is left NULL
    (resolve them by deleting one in the app, then run again).
  - Nothing is written with -WhatIf.

.PARAMETER ConnectionString
  SQL Server connection string. Not stored anywhere; pass it on the command line.

.PARAMETER StorageRoot
  Folder that the stored FilePath (for example uploads/employees/143/abc.pdf) is relative to:
  the site's wwwroot folder, or EmployeeDocuments:StorageBasePath when that is set.

.EXAMPLE
  .\backfill-document-hashes.ps1 -ConnectionString "Server=.;Database=HRBTK;Integrated Security=True;TrustServerCertificate=True" -StorageRoot C:\HRMSDeploy\wwwroot -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$ConnectionString,
    [Parameter(Mandatory = $true)][string]$StorageRoot
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data

$conn = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
$conn.Open()
try {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT Id, EmployeeId, FilePath FROM dbo.EmployeeDocuments WHERE FileHash IS NULL AND IsDeleted = 0 ORDER BY Id"
    $rows = @()
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) { $rows += [pscustomobject]@{ Id = $reader.GetInt32(0); EmployeeId = $reader.GetInt32(1); FilePath = $reader.GetString(2) } }
    $reader.Close()

    $seen = @{}   # "employee|hash" -> document id
    $updated = 0; $missing = 0; $duplicate = 0

    foreach ($row in $rows) {
        $physical = Join-Path $StorageRoot ($row.FilePath -replace '/', '\')
        if (-not (Test-Path -LiteralPath $physical)) {
            Write-Warning "Document $($row.Id) (employee $($row.EmployeeId)): file not found: $physical"
            $missing++
            continue
        }
        $hash = (Get-FileHash -LiteralPath $physical -Algorithm SHA256).Hash.ToLowerInvariant()
        $key = "$($row.EmployeeId)|$hash"

        $check = $conn.CreateCommand()
        $check.CommandText = "SELECT TOP 1 Id FROM dbo.EmployeeDocuments WHERE EmployeeId = @e AND FileHash = @h AND IsDeleted = 0"
        [void]$check.Parameters.AddWithValue('@e', $row.EmployeeId)
        [void]$check.Parameters.AddWithValue('@h', $hash)
        $other = $check.ExecuteScalar()

        if ($seen.ContainsKey($key) -or $null -ne $other) {
            $first = if ($seen.ContainsKey($key)) { $seen[$key] } else { $other }
            Write-Warning "Document $($row.Id) has the same content as document $first (employee $($row.EmployeeId)); left without a hash. Delete one of them in the app and run again."
            $duplicate++
            continue
        }

        if ($PSCmdlet.ShouldProcess("EmployeeDocuments.Id $($row.Id)", "set FileHash $($hash.Substring(0, 12))...")) {
            $upd = $conn.CreateCommand()
            $upd.CommandText = "UPDATE dbo.EmployeeDocuments SET FileHash = @h WHERE Id = @id AND FileHash IS NULL"
            [void]$upd.Parameters.AddWithValue('@h', $hash)
            [void]$upd.Parameters.AddWithValue('@id', $row.Id)
            [void]$upd.ExecuteNonQuery()
            $updated++
        }
        $seen[$key] = $row.Id
    }

    Write-Host ("Rows without a hash: {0} | updated: {1} | file missing: {2} | duplicates left: {3}" -f $rows.Count, $updated, $missing, $duplicate)
}
finally {
    $conn.Close()
}
