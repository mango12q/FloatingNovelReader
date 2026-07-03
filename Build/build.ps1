<#
.SYNOPSIS
    浮窗小说阅读器自动化构建脚本。

.DESCRIPTION
    执行：
      - 还原依赖
      - 调试 + Release 构建
      - 运行单元测试
      - 发布 Framework-Dependent 单文件
      - 发布自包含单文件（可选）
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
    if ($LASTEXITCODE -ne 0) { Write-Warning "测试失败，但继续后续步骤" }
}
else {
    Write-Host "`n==> 跳过测试" -ForegroundColor Yellow
}

# 4. 发布 Framework-Dependent
if (-not $SkipPublish) {
    Write-Host "`n==> 发布 Framework-Dependent 单文件 (win-x64)..." -ForegroundColor Cyan
    $fdOut = Join-Path $ProjectRoot "publish/$Rid-framework-dependent"
    dotnet publish FloatingNovelReader/FloatingNovelReader.csproj `
        -c $Configuration -r $Rid `
        --self-contained false `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true `
        -o $fdOut
    if ($LASTEXITCODE -ne 0) { throw "Framework-Dependent 发布失败" }
    Write-Host "    ✓ 已生成：$fdOut" -ForegroundColor Green

    Write-Host "`n==> 发布自包含单文件 (win-x64)..." -ForegroundColor Cyan
    $scOut = Join-Path $ProjectRoot "publish/$Rid-self-contained"
    dotnet publish FloatingNovelReader/FloatingNovelReader.csproj `
        -c $Configuration -r $Rid `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true `
        -o $scOut
    if ($LASTEXITCODE -ne 0) { throw "Self-Contained 发布失败" }
    Write-Host "    ✓ 已生成：$scOut" -ForegroundColor Green
}

Write-Host "`n==> 完成！" -ForegroundColor Green
Write-Host "    Framework-Dependent 版本：publish/$Rid-framework-dependent" -ForegroundColor Gray
Write-Host "    自包含版本：publish/$Rid-self-contained" -ForegroundColor Gray
