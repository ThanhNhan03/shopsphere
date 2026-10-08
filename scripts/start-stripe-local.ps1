param([switch]$Stop)
$ErrorActionPreference = 'Stop'
$repoDirectory = Split-Path -Parent $PSScriptRoot
$localDirectory = Join-Path $repoDirectory '.local'
$envFile = Join-Path $repoDirectory '.env'
$stateFile = Join-Path $localDirectory 'stripe-listener.json'
New-Item -ItemType Directory -Path $localDirectory -Force | Out-Null

# Stop only the listener recorded by this script, never another Stripe process.
if (Test-Path -LiteralPath $stateFile) {
    $state = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json
    $previous = Get-Process -Id $state.processId -ErrorAction SilentlyContinue
    if ($previous -and $previous.Path -eq $state.executable -and
        $previous.StartTime.ToUniversalTime().Ticks -eq ([DateTimeOffset]$state.startedAt).UtcTicks) {
        Stop-Process -Id $previous.Id
        $previous.WaitForExit()
    }
}
if ($Stop) { Write-Output 'The ShopSphere Stripe listener is stopped.'; return }
if (!(Test-Path -LiteralPath $envFile)) { throw 'Create the ignored root .env and set STRIPE_SECRET_KEY to your test key first.' }
$content = [IO.File]::ReadAllText($envFile)
$settings = @{}
foreach ($line in $content -split "`n") {
    if ($line -match '^([A-Z_]+)=(.*)$') { $settings[$Matches[1]] = $Matches[2].Trim().Trim('"').Trim("'") }
}
if ($settings['STRIPE_SECRET_KEY'] -notmatch '^sk_test_') { throw 'STRIPE_SECRET_KEY must be a Stripe test secret key.' }
$command = Get-Command stripe -ErrorAction SilentlyContinue
$cli = if ($command) { $command.Source } else { Join-Path $localDirectory 'stripe-cli\stripe.exe' }
if (!(Test-Path -LiteralPath $cli)) { throw 'Install the official Stripe CLI and put stripe on PATH (https://docs.stripe.com/cli/install).' }
$gatewayPort = if ($settings['GATEWAY_PORT']) { $settings['GATEWAY_PORT'] } else { '8080' }
$forwardTo = "http://127.0.0.1:$gatewayPort/api/payments/webhooks/stripe"
$stdoutFile = Join-Path $localDirectory 'stripe-listener.stdout.log'
$stderrFile = Join-Path $localDirectory 'stripe-listener.stderr.log'
$previousApiKey = $env:STRIPE_API_KEY
try {
    # Environment only: never expose the API key in process arguments or console output.
    $env:STRIPE_API_KEY = $settings['STRIPE_SECRET_KEY']
    $secretOutput = & $cli listen --print-secret 2> $stderrFile
    if ($LASTEXITCODE -ne 0) { throw 'Stripe CLI authentication failed. Check the ignored local listener log.' }
    $secretMatch = [regex]::Match(($secretOutput -join "`n"), 'whsec_[A-Za-z0-9]+')
    if (!$secretMatch.Success) { throw 'Stripe CLI did not return a webhook signing secret.' }
    $listener = Start-Process -FilePath $cli -ArgumentList @('listen', '--events', 'checkout.session.completed,checkout.session.async_payment_succeeded,checkout.session.async_payment_failed,checkout.session.expired', '--forward-to', $forwardTo) -WindowStyle Hidden -RedirectStandardOutput $stdoutFile -RedirectStandardError $stderrFile -PassThru
} finally {
    $env:STRIPE_API_KEY = $previousApiKey
}
$listenerState = @{processId=$listener.Id; executable=$listener.Path; startedAt=$listener.StartTime.ToUniversalTime().ToString('o')}
$listenerState | ConvertTo-Json | Set-Content -LiteralPath $stateFile -Encoding utf8
$ready = $false
for ($attempt = 0; $attempt -lt 20; $attempt++) {
    Start-Sleep -Seconds 1
    $listener.Refresh()
    if ($listener.HasExited) { break }
    $logText = (Get-Content -LiteralPath $stdoutFile -Raw) + (Get-Content -LiteralPath $stderrFile -Raw)
    if ($logText -match 'Ready!') { $ready = $true; break }
}
if (!$ready) { Stop-Process -Id $listener.Id -ErrorAction SilentlyContinue; throw 'Stripe listener was not ready. Check .local/stripe-listener.stderr.log.' }
foreach ($entry in @{PAYMENT_MODE='Stripe'; STRIPE_WEBHOOK_SECRET=$secretMatch.Value}.GetEnumerator()) {
    $pattern = '(?m)^' + [regex]::Escape($entry.Key) + '=.*\r?$'
    $value = $entry.Key + '=' + $entry.Value
    if ([regex]::IsMatch($content, $pattern)) { $content = [regex]::Replace($content, $pattern, [Text.RegularExpressions.MatchEvaluator]{ param($m) $value }) }
    else { $content += "`n$value`n" }
}
[IO.File]::WriteAllText($envFile, $content)
Push-Location $repoDirectory
try {
    docker compose up -d --no-deps payment-api
    if ($LASTEXITCODE -ne 0) { throw 'Payment service could not be recreated.' }
} finally { Pop-Location }
Write-Output "Stripe test mode is ready. Listener forwards to $forwardTo."
Write-Output 'Keys and listener logs stay in ignored local files. Keep the listener running during checkout.'
