<#
.SYNOPSIS
    浮窗小说阅读器自动化构建脚本。

.DESCRIPTION
    执行：
      - 还原依赖
      - Release 构建
      - 运行单元测试
      - 发布 portable 单文件 EXE（Framework-Dependent，需 .NET 8 桌面运行时）
      - 打包 zip
#>

[CmdletBinding()]
param(
    [switch]$SkipTests,
    [switch]$SkipPublish,
    [string]$Configuration = "Release",
    [string]$Rid = "win-x64"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $ProjectRoot

# 重命名输出 EXE
function Rename-Exe {
    param([string]$Dir, [string]$FromName, [string]$ToName)
    $src = Join-Path $Dir $FromName
    if (Test-Path $src) {
        $dst = Join-Path $Dir $ToName
        Move-Item -Path $src -Destination $dst -Force
        Write-Host "    ✓ 重命名：$FromName -> $ToName" -ForegroundColor Green
    }
}

# 压缩输出目录
function New-Zip {
    param([string]$SourceDir, [string]$ZipPath)
    if (-not (Test-Path $SourceDir)) { return }
    $parent = Split-Path $SourceDir -Parent
    if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($SourceDir, $ZipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)
    Write-Host "    ✓ 已生成 zip：$ZipPath" -ForegroundColor Green
}

Write-Host "==> 项目根目录：$ProjectRoot" -ForegroundColor Cyan

# 1. 还原
Write-Host "`n==> 还原 NuGet 包..." -ForegroundColor Cyan
dotnet restore FloatingNovelReader.sln
if ($LASTEXITCODE -ne 0) { throw "dotnet restore 失败" }

# 2. 构建
Write-Host "`n==> 构建 ($Configuration)..." -ForegroundColor Cyan
dotnet build FloatingNovelReader.sln -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet build 失败" }

# 3. 测试
if (-not $SkipTests) {
    Write-Host "`n==> 运行单元测试..." -ForegroundColor Cyan
    dotnet test FloatingNovelReader.Tests/FloatingNovelReader.Tests.csproj -c $Configuration --no-build --logger "console;verbosity=normal"
    if ($LASTEXITCODE -ne 0) { throw "测试失败" }
}
else {
    Write-Host "`n==> 跳过测试" -ForegroundColor Yellow
}

# 4. 发布 portable
if (-not $SkipPublish) {
    Write-Host "`n==> 发布 portable 单文件 ($Rid)..." -ForegroundColor Cyan
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $publishRoot = Join-Path $ProjectRoot "publish"
    $portableDir = Join-Path $publishRoot "$Rid-portable"

    # portable：Framework-Dependent 单文件，约 4 MB，需 .NET 8 桌面运行时
    # 注意：Framework-Dependent 模式不支持 EnableCompressionInSingleFile
    dotnet publish FloatingNovelReader/FloatingNovelReader.csproj `
        -c $Configuration -r $Rid `
        --self-contained false `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $portableDir
    if ($LASTEXITCODE -ne 0) { throw "portable 发布失败" }

    Rename-Exe -Dir $portableDir -FromName "FloatingNovelReader.exe" -ToName "floating-novel-reader-portable.exe"

    $portableZip = Join-Path $publishRoot "floating-novel-reader-portable-$Rid-$timestamp.zip"
    New-Zip -SourceDir $portableDir -ZipPath $portableZip
}

Write-Host "`n==> 完成！" -ForegroundColor Green
Write-Host "    产物在 publish/ 目录下" -ForegroundColor Gray
Write-Host "      - $Rid-portable/floating-novel-reader-portable.exe (需 .NET 8 桌面运行时)" -ForegroundColor Gray
