param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

# 1. 解析项目根路径与版本号
$scriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = (Get-Location).Path
}

$parentItem = Get-Item $scriptDir
if ($parentItem.Name -eq "scripts") {
    $root = $parentItem.Parent.FullName
} else {
    $root = $parentItem.FullName
}

$projPath = Join-Path $root "WinKit.csproj"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $content = Get-Content $projPath -Raw
    if ($content -match '<Version>(.*?)</Version>') {
        $Version = $matches[1].Trim()
    } else {
        $Version = "2.6"
    }
}

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  WinKit 自动化发布与打包脚本 (v$Version)" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

# 2. 停止正在运行的 WinKit 进程，避免输出目录文件占用
Write-Host "[1/4] 检查并清理旧进程与输出目录..." -ForegroundColor Yellow
Stop-Process -Name "WinKit" -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

$publishBase = Join-Path $root "bin\Publish"
$fdOutDir = Join-Path $publishBase "FrameworkDependent_V$Version"
$scOutDir = Join-Path $publishBase "SelfContained_V$Version"
$fdZip = Join-Path $publishBase "WinKit-v$Version-FrameworkDependent.zip"
$scZip = Join-Path $publishBase "WinKit-v$Version-SelfContained.zip"

if (Test-Path $fdOutDir) { Remove-Item $fdOutDir -Recurse -Force }
if (Test-Path $scOutDir) { Remove-Item $scOutDir -Recurse -Force }
if (Test-Path $fdZip) { Remove-Item $fdZip -Force }
if (Test-Path $scZip) { Remove-Item $scZip -Force }

# 3. 编译发布：框架依赖版 (Framework-Dependent)
Write-Host "[2/4] 正在编译发布：框架依赖版 (Framework-Dependent)..." -ForegroundColor Yellow
dotnet publish $projPath -c Release -r win-x64 --self-contained false -o $fdOutDir

# 4. 编译发布：独立免装版 (Self-Contained)
Write-Host "[3/4] 正在编译发布：独立免装版 (Self-Contained)..." -ForegroundColor Yellow
dotnet publish $projPath -c Release -r win-x64 --self-contained true -o $scOutDir

# 5. 自动打包压缩为 ZIP
Write-Host "[4/4] 正在压缩打包为标准发行文件..." -ForegroundColor Yellow
Compress-Archive -Path "$fdOutDir\*" -DestinationPath $fdZip -Force
Compress-Archive -Path "$scOutDir\*" -DestinationPath $scZip -Force

Write-Host "=================================================" -ForegroundColor Green
Write-Host "  发布打包完成！产物列表：" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green

$fdItem = Get-Item $fdZip
$scItem = Get-Item $scZip

Write-Host "1. 框架依赖版 ZIP: $($fdItem.FullName)" -ForegroundColor White
Write-Host "   文件大小: $([math]::Round($fdItem.Length / 1MB, 2)) MB" -ForegroundColor Gray
Write-Host "2. 独立免装版 ZIP: $($scItem.FullName)" -ForegroundColor White
Write-Host "   文件大小: $([math]::Round($scItem.Length / 1MB, 2)) MB" -ForegroundColor Gray
Write-Host ""
Write-Host "现在可直接将上述 ZIP 文件上传至 GitHub Releases 发行版页面！" -ForegroundColor Cyan
