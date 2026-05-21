# Túnel rápido Cloudflare hacia la API local (http://localhost:5098).
# Requiere: dotnet run en Api/ y cloudflared instalado (winget install Cloudflare.cloudflared).

$ErrorActionPreference = "Stop"
$port = if ($env:API_PORT) { $env:API_PORT } else { "5098" }
$target = "http://localhost:$port"

$cloudflared = Get-Command cloudflared -ErrorAction SilentlyContinue
if (-not $cloudflared) {
    $candidates = @(
        "${env:ProgramFiles}\cloudflared\cloudflared.exe",
        "${env:ProgramFiles(x86)}\cloudflared\cloudflared.exe"
    )
    foreach ($path in $candidates) {
        if (Test-Path $path) {
            $cloudflared = @{ Source = $path }
            break
        }
    }
}

if (-not $cloudflared) {
    Write-Host "cloudflared no encontrado. Instala con:" -ForegroundColor Red
    Write-Host "  winget install Cloudflare.cloudflared" -ForegroundColor Yellow
    exit 1
}

Write-Host "Iniciando túnel hacia $target ..." -ForegroundColor Cyan
Write-Host "Cuando aparezca la URL https://....trycloudflare.com:" -ForegroundColor Cyan
Write-Host "  1. Abre la app por esa URL (no localhost)" -ForegroundColor White
Write-Host "  2. En Google Cloud Console agrega redirect URI:" -ForegroundColor White
Write-Host "     https://TU-URL.trycloudflare.com/signin-google" -ForegroundColor White
Write-Host "  3. Actualiza App:PublicOrigin en Api/appsettings.Development.json" -ForegroundColor White
Write-Host ""

& $cloudflared.Source tunnel --url $target
