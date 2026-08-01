<#
.SYNOPSIS
    浮窗小说阅读器自动化构建脚本。

.DESCRIPTION
    执行：
      - 还原依赖
      - Release 构建
      - 运行单元测试
      - 发布两种打包产物（Framework-Dependent，需 .NET 8 桌面运行时）：
        1. 单文件版：floating-novel-reader-singlefile-win-x64.exe
           单个 EXE，双击即用，首次运行会询问是否安装
        2. 便携版：  floating-novel-reader-portable-win-x64-*.zip
           解压即用（EXE + DLL 目录），含 portable.mode 标记，不弹安装提示
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
Push-Location $ProjectRoot

try {

# 重命名输出 EXE（源不存在视为发布失败）
function Rename-Exe {
    param([string]$Dir, [string]$FromName, [string]$ToName)
    $src = Join-Path $Dir $FromName
    if (-not (Test-Path $src)) { throw "发布产物缺失：$src" }
    $dst = Join-Path $Dir $ToName
    Move-Item -Path $src -Destination $dst -Force
    Write-Host "    ✓ 重命名：$FromName -> $ToName" -ForegroundColor Green
}

# 压缩输出目录（源不存在视为发布失败）
function New-Zip {
    param([string]$SourceDir, [string]$ZipPath)
    if (-not (Test-Path $SourceDir)) { throw "待打包目录不存在：$SourceDir" }
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

# 4. 发布
if (-not $SkipPublish) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $publishRoot = Join-Path $ProjectRoot "publish"
    $singleDir  = Join-Path $publishRoot "$Rid-singlefile"
    $portableDir = Join-Path $publishRoot "$Rid-portable"

    # 清理旧产物，避免残留文件被打包
    foreach ($d in @($singleDir, $portableDir)) {
        if (Test-Path $d) { Remove-Item -Recurse -Force $d }
    }

    # 4.1 单文件版：PublishSingleFile + 原生库自解压，双击即用
    Write-Host "`n==> 发布单文件版 ($Rid)..." -ForegroundColor Cyan
    dotnet publish FloatingNovelReader/FloatingNovelReader.csproj `
        -c $Configuration -r $Rid `
        --self-contained false `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $singleDir
    if ($LASTEXITCODE -ne 0) { throw "单文件版发布失败" }
    Rename-Exe -Dir $singleDir -FromName "FloatingNovelReader.exe" -ToName "floating-novel-reader-singlefile-$Rid.exe"

    # 4.2 便携版：普通发布（EXE + DLL 目录），附带 portable.mode 标记免安装提示
    Write-Host "`n==> 发布便携版 ($Rid)..." -ForegroundColor Cyan
    dotnet publish FloatingNovelReader/FloatingNovelReader.csproj `
        -c $Configuration -r $Rid `
        --self-contained false `
        -p:PublishSingleFile=false `
        -o $portableDir
    if ($LASTEXITCODE -ne 0) { throw "便携版发布失败" }
    New-Item -ItemType File -Path (Join-Path $portableDir "portable.mode") -Force | Out-Null

    $portableZip = Join-Path $publishRoot "floating-novel-reader-portable-$Rid-$timestamp.zip"
    New-Zip -SourceDir $portableDir -ZipPath $portableZip

    # 4.3 产物校验
    $singleExe = Join-Path $singleDir "floating-novel-reader-singlefile-$Rid.exe"
    if (-not (Test-Path $singleExe)) { throw "单文件版产物缺失" }
    if (-not (Test-Path $portableZip)) { throw "便携版 zip 缺失" }
}

Write-Host "`n==> 完成！" -ForegroundColor Green
Write-Host "    产物在 publish/ 目录下：" -ForegroundColor Gray
Write-Host "      单文件版: $Rid-singlefile/floating-novel-reader-singlefile-$Rid.exe" -ForegroundColor Gray
Write-Host "      便携版:   floating-novel-reader-portable-$Rid-*.zip (解压即用，需 .NET 8 桌面运行时)" -ForegroundColor Gray

}
finally {
    Pop-Location
}
