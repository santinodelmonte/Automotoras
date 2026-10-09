# Arma la carpeta que se sube al hosting: la API compilada en Release con el frontend
# adentro de wwwroot. Un solo sitio de IIS sirve las dos cosas —sin CORS y con un solo
# binding por dominio—, y la API mete en cada página los meta tags de la automotora.
#
# Uso, desde la raíz del repo:
#   powershell -ExecutionPolicy Bypass -File tools\publicar.ps1
#
# Deja todo en .publish\ (ignorado por git). Ese contenido es lo que se copia por FTP o
# por el administrador de archivos del hosting a la carpeta del sitio.

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$salida = Join-Path $raiz '.publish'
$frontend = Join-Path $raiz 'frontend'

if (Test-Path $salida) {
    Remove-Item -Recurse -Force $salida
}

Write-Host '1/3 Frontend' -ForegroundColor Cyan
Push-Location $frontend
try {
    # Vacía a propósito: en producción la API está en el mismo origen. Las variables del
    # proceso le ganan a los archivos .env, así que ningún .env local con localhost se
    # puede colar en el bundle.
    $env:VITE_API_BASE_URL = ''
    npm ci
    if ($LASTEXITCODE -ne 0) { throw 'npm ci falló' }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw 'El build del frontend falló' }
}
finally {
    Remove-Item Env:\VITE_API_BASE_URL -ErrorAction SilentlyContinue
    Pop-Location
}

if (Select-String -Path (Join-Path $frontend 'dist\assets\*.js') -Pattern 'localhost:5080' -Quiet) {
    throw 'El bundle apunta a localhost:5080. Revisá VITE_API_BASE_URL.'
}

Write-Host '2/3 API' -ForegroundColor Cyan
dotnet publish (Join-Path $raiz 'backend\Api\Api.csproj') -c Release -o $salida --nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish falló' }

Write-Host '3/3 Frontend dentro de wwwroot' -ForegroundColor Cyan
$wwwroot = Join-Path $salida 'wwwroot'
New-Item -ItemType Directory -Force $wwwroot | Out-Null
Copy-Item -Recurse -Force (Join-Path $frontend 'dist\*') $wwwroot

# Lo de desarrollo no viaja: la configuración real va por variables de entorno o por un
# appsettings.Production.json que vive solo en el servidor.
Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $salida 'appsettings.Development.json')

Write-Host ''
Write-Host "Listo: $salida" -ForegroundColor Green
Write-Host 'Antes de subir: aplicá las migraciones (dotnet dotnet-ef migrations script --idempotent) y revisá docs/operacion.md.'
