# 「秘密档案」单文件发布脚本 / CryptedNotex single-file publish script
# 产物 / Output: Rampage.CryptedNotex.WinForm\bin\Release\net8.0-windows\win-x64\publish\
#   单个 exe + config\ + Assets\Markdown\(运行时再生成 data\、__Logs\)
#   one exe plus config\ and Assets\Markdown\ (data\ and __Logs\ are created at runtime)
# 依赖框架发布 / Framework-dependent: 目标机需已安装 .NET 8 Desktop Runtime (x64)
#   target machines need the .NET 8 Desktop Runtime (x64); without the WebView2 runtime only the Markdown preview is disabled
# 用法 / Usage: powershell -ExecutionPolicy Bypass -File .\publish.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'Rampage.CryptedNotex.Net\Rampage.CryptedNotex.WinForm\Rampage.CryptedNotex.WinForm.csproj'
$publishDir = Join-Path $repoRoot 'Rampage.CryptedNotex.Net\Rampage.CryptedNotex.WinForm\bin\Release\net8.0-windows\win-x64\publish'

# 清理旧产物,防止历史上松散发布的残留文件混进安装包 / Clean stale output so leftovers from earlier loose publishes never leak into the installer
if (Test-Path $publishDir)
{
    Remove-Item $publishDir -Recurse -Force
}

Write-Host '==> dotnet publish (framework-dependent single file, win-x64)...'
# 注意 / Notes:
#   SelfContained 必须用 -p: 属性形式显式传 false —— .NET 10 SDK 下仅传 -r 时默认倾向自包含,--self-contained false 参数形式实测未生效;
#     SelfContained must be passed explicitly as an MSBuild property: under the .NET 10 SDK, -r alone leans self-contained
#     and the --self-contained false argument form was observed not to take effect.
#   依赖框架发布不支持 EnableCompressionInSingleFile(NETSDK1176),csproj 中未开启 / compression is self-contained-only (NETSDK1176), so it stays off.
#   DebugType=embedded: pdb 嵌入 exe,发布目录不产生散装 .pdb / embeds the pdb so no loose .pdb ships beside the exe
dotnet publish $project -c Release -r win-x64 -p:SelfContained=false -p:DebugType=embedded
if ($LASTEXITCODE -ne 0)
{
    throw "publish failed with exit code $LASTEXITCODE"
}

Write-Host ''
Write-Host '==> 发布完成 / Published:' -ForegroundColor Green
Get-ChildItem $publishDir | ForEach-Object {
    if ($_.PSIsContainer)
    {
        Write-Host ("    {0}\ (dir)" -f $_.Name)
    }
    else
    {
        Write-Host ("    {0}  {1:N0} KB" -f $_.Name, ($_.Length / 1KB))
    }
}
Write-Host ''
Write-Host '提示 / Note: 目标机需 .NET 8 Desktop Runtime (x64);缺少 WebView2 运行时时仅 Markdown 预览不可用。'
