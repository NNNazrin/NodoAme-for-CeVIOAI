# 重新编译 cevio-NodoAme-talk.exe 与 bin\cevio-render2.exe
# 用法: pwsh -File build.ps1 [-CeVioDir "D:\path\to\CeVIO AI"]
param([string]$CeVioDir = "")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$fw   = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$csc  = Join-Path $fw "csc.exe"
$wpf  = Join-Path $fw "WPF"
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

function Resolve-CeVio([string]$arg) {
    if ($arg) { return $arg.TrimEnd('\') }
    foreach ($k in @("SOFTWARE\CeVIO_NV\Subject\Editor\x64", "SOFTWARE\CeVIO_NV\Subject\Editor")) {
        foreach ($hive in @([Microsoft.Win32.Registry]::LocalMachine, [Microsoft.Win32.Registry]::CurrentUser)) {
            $rk = $hive.OpenSubKey($k)
            if (-not $rk) { continue }
            $dir = [string]$rk.GetValue("InstallFolder", "")
            if ($dir -and (Test-Path $dir)) { return $dir.TrimEnd('\') }
        }
    }
    return ""
}

Write-Host "== csc: $csc"

# 1) 主程序（不依赖 CeVIO，纯 .NET）
$main = Join-Path $root "cevio-NodoAme-talk.exe"
if (Test-Path $main) { Get-Process cevio-NodoAme-talk -ErrorAction SilentlyContinue | Stop-Process -Force }
& $csc /nologo /langversion:5 /codepage:65001 /target:exe /platform:anycpu /optimize+ `
    /out:$main /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll `
    (Join-Path $root "CeVioTalk.cs")
if ($LASTEXITCODE -ne 0) { throw "main build failed ($LASTEXITCODE)" }
Write-Host "OK  cevio-NodoAme-talk.exe"

# 2) 渲染器（编译期引用本机 CeVIO DLL，运行期加载）
$cevio = Resolve-CeVio $CeVioDir
if (-not $cevio) { throw "CeVIO AI folder not found. Run: pwsh -File build.ps1 -CeVioDir '<CeVIO AI dir>'" }
Write-Host "== CeVIO: $cevio"
$r2 = Join-Path $root "bin\cevio-render2.exe"
& $csc /nologo /langversion:5 /codepage:65001 /target:exe /platform:x64 /optimize+ `
    /out:$r2 `
    /r:"$(Join-Path $wpf 'WindowsBase.dll')" /r:"$(Join-Path $wpf 'PresentationCore.dll')" `
    /r:"$(Join-Path $wpf 'PresentationFramework.dll')" /r:"$(Join-Path $wpf 'UIAutomationClient.dll')" `
    /r:"$(Join-Path $wpf 'UIAutomationTypes.dll')" `
    /r:"$cevio\CeVIO.SongEditorControl.dll" /r:"$cevio\CeVIO.Song.dll" /r:"$cevio\CeVIO.Audio.dll" `
    /r:"$fw\netstandard.dll" /r:"$fw\System.Xml.Linq.dll" `
    (Join-Path $root "CeVioRender2.cs")
if ($LASTEXITCODE -ne 0) { throw "render build failed ($LASTEXITCODE)" }
Write-Host "OK  bin\cevio-render2.exe"
Write-Host "`nDone."
