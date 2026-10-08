[CmdletBinding()]
param(
    # Includes the original six products; new IDs run from 7 through Count.
    [ValidateRange(7, 10000000)][int]$Count = 1000000,
    [ValidateRange(1000, 100000)][int]$BatchSize = 50000
)

$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$seedSql = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'data/seed-catalog.sql') -Raw
$timer = [Diagnostics.Stopwatch]::StartNew()
Push-Location -LiteralPath $repoPath
try {
    for ($first = 7; $first -le $Count; $first += $BatchSize) {
        $last = [Math]::Min($first + $BatchSize - 1, $Count)
        $seedSql | docker compose exec -T postgres sh -c 'exec psql -X -q -U "$POSTGRES_USER" -d catalog_db -v ON_ERROR_STOP=1 -v first="$1" -v last="$2"' sh $first $last
        if ($LASTEXITCODE -ne 0) { throw "Seed batch $first..$last failed. Fix the error and rerun; existing rows are preserved." }
        Write-Host ("Seeded through SKU {0:N0}/{1:N0} ({2:N1}s)" -f $last, $Count, $timer.Elapsed.TotalSeconds)
    }
    @'
ANALYZE "Products";
SELECT json_build_object('database', current_database(), 'rows', count(*),
    'fixtureRows', count(*) FILTER (WHERE "Id"::text LIKE '10000000-%'),
    'tableBytes', pg_total_relation_size('"Products"'),
    'databaseBytes', pg_database_size(current_database())) FROM "Products";
\connect inventory_db
ANALYZE "Stocks";
SELECT json_build_object('database', current_database(), 'rows', count(*),
    'fixtureRows', count(*) FILTER (WHERE "ProductId"::text LIKE '10000000-%'),
    'tableBytes', pg_total_relation_size('"Stocks"'),
    'databaseBytes', pg_database_size(current_database())) FROM "Stocks";
'@ | docker compose exec -T postgres sh -c 'exec psql -X -q -U "$POSTGRES_USER" -d catalog_db -v ON_ERROR_STOP=1'
    if ($LASTEXITCODE -ne 0) { throw 'Post-seed ANALYZE/report failed.' }
    Write-Host ("Finished in {0:N1}s. Re-running inserts only missing rows; lowering Count never deletes data." -f $timer.Elapsed.TotalSeconds)
}
finally { Pop-Location }
