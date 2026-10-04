param (
    [string]$InstallDir = "$env:LOCALAPPDATA\TiaMcpServer"
)

Write-Host "========================================="
Write-Host " TiaMcpServer Installer                  "
Write-Host "========================================="
Write-Host ""

Write-Host "[1/3] Checking dependencies..."
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "The .NET SDK is not installed or not in the PATH. Please install it first."
    exit 1
}

Write-Host "[2/3] Building and publishing the server..."
$projectPath = Join-Path $PSScriptRoot "src\TiaMcpServer\TiaMcpServer.csproj"
dotnet publish $projectPath -c Release -o $InstallDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed. Check the logs above for details."
    exit $LASTEXITCODE
}

Write-Host "[3/3] Installation Complete!"
Write-Host ""
Write-Host "TiaMcpServer has been successfully installed to:"
Write-Host "  $InstallDir"
Write-Host ""
Write-Host "Executable Path: $InstallDir\TiaMcpServer.exe"
Write-Host ""
Write-Host "Please check INSTALL.md for instructions on how to integrate the server"
Write-Host "into Claude Code, Antigravity 2.0, or Claude Desktop."
Write-Host "========================================="
