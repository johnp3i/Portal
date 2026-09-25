<#
.SYNOPSIS
    Migration 212 (file-move half): moves business logo files from the public web root
    (wwwroot/uploads/logos) to the private file-storage root ({BasePath}/{businessId}/logos).

.DESCRIPTION
    Business logos moved from wwwroot/uploads/logos/{file} to {FileStorage:BasePath}/{businessId}/logos/{file}
    (redeploy-safe, tenant-isolated, single backup root). The bytes live on disk, so this script
    handles the physical move; the companion SQL script 212_MigrateLogosToBasePath.sql rewrites
    BusinessLogo.PublicUrl to the new /logo/{businessId}/{file} route.

    The script reads every logo row from the DB (BusinessId, FileName), then COPIES each file from
    the web root to the private root. Copy (not move) so a re-run is safe; delete the wwwroot copies
    manually once you have verified logos render.

    Recommended deployment order:
      1. Deploy the new build.
      2. Run this script (optionally with -WhatIf first to preview).
      3. Run 212_MigrateLogosToBasePath.sql.
      4. Verify logos render (management screen, an invoice PDF, a shared proposal snapshot),
         then delete the old wwwroot/uploads/logos folder.

    Idempotent: existing destination files are overwritten with the same bytes; missing sources are warned.

.PARAMETER WebRoot
    Absolute path to the deployed Portal.Web wwwroot folder (the old logo location's parent).
    Example: C:\inetpub\Portal\wwwroot

.PARAMETER BasePath
    The FileStorage:BasePath value from appsettings (the new private root).
    Example (prod): C:/BusinessPortalUploads/Uploads

.PARAMETER ConnectionString
    SQL Server connection string to the Portal database.
    Example: "Server=.;Database=Portal;Integrated Security=true;TrustServerCertificate=true"

.PARAMETER WhatIf
    Dry run — reports what WOULD be moved without copying anything.

.EXAMPLE
    # Preview first
    .\212_MigrateLogosToBasePath.ps1 -WebRoot "C:\inetpub\Portal\wwwroot" -BasePath "C:/BusinessPortalUploads/Uploads" -ConnectionString "Server=.;Database=Portal;Integrated Security=true;TrustServerCertificate=true" -WhatIf

.EXAMPLE
    # Real run
    .\212_MigrateLogosToBasePath.ps1 -WebRoot "C:\inetpub\Portal\wwwroot" -BasePath "C:/BusinessPortalUploads/Uploads" -ConnectionString "Server=.;Database=Portal;Integrated Security=true;TrustServerCertificate=true"

.NOTES
    Requires the SqlServer module (Invoke-Sqlcmd). Install once with:  Install-Module SqlServer -Scope CurrentUser
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $WebRoot,

    [Parameter(Mandatory = $true)]
    [string] $BasePath,

    [Parameter(Mandatory = $true)]
    [string] $ConnectionString,

    [switch] $WhatIf
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $WebRoot)) {
    throw "WebRoot path not found: $WebRoot"
}

Write-Host "Logo migration 212 (file move)" -ForegroundColor Cyan
Write-Host "  WebRoot : $WebRoot"
Write-Host "  BasePath: $BasePath"
Write-Host "  Mode    : $(if ($WhatIf) { 'DRY RUN (-WhatIf)' } else { 'LIVE' })"
Write-Host ""

# Pull logo rows from the DB.
# Force an array with @(...) so a single-row result still exposes .Count (Windows PowerShell 5.x
# returns a bare object, not an array, for one row).
$rows = @(Invoke-Sqlcmd -ConnectionString $ConnectionString -Query `
    "SELECT BusinessId, FileName FROM portal.BusinessLogo WHERE FileName IS NOT NULL AND FileName <> ''")

if ($rows.Count -eq 0) {
    Write-Host "No logo rows found. Nothing to do." -ForegroundColor Yellow
    return
}

$moved   = 0
$missing = 0
$skipped = 0

foreach ($r in $rows) {
    $src    = Join-Path $WebRoot ("uploads\logos\" + $r.FileName)
    $dstDir = Join-Path $BasePath ($r.BusinessId.ToString() + "\logos")
    $dst    = Join-Path $dstDir $r.FileName

    if (-not (Test-Path $src)) {
        Write-Warning "Missing source (business $($r.BusinessId)): $src"
        $missing++
        continue
    }

    if (Test-Path $dst) {
        Write-Host "Already at destination (business $($r.BusinessId)): $($r.FileName)" -ForegroundColor DarkGray
        $skipped++
        # still ensure it's the same file below? we simply skip re-copy to keep the run fast/idempotent
        continue
    }

    if ($WhatIf) {
        Write-Host "[WhatIf] Would copy: $src  ->  $dst"
        $moved++
        continue
    }

    New-Item -ItemType Directory -Force -Path $dstDir | Out-Null
    Copy-Item -Path $src -Destination $dst -Force
    Write-Host "Copied logo (business $($r.BusinessId)): $($r.FileName)" -ForegroundColor Green
    $moved++
}

Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Total rows      : $($rows.Count)"
Write-Host "  $(if ($WhatIf) { 'Would copy    ' } else { 'Copied        ' }): $moved"
Write-Host "  Already present : $skipped"
Write-Host "  Missing source  : $missing"

if (-not $WhatIf) {
    Write-Host ""
    Write-Host "Next: run 212_MigrateLogosToBasePath.sql, verify logos render, then delete the old wwwroot/uploads/logos folder." -ForegroundColor Yellow
}
