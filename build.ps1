param([switch]$SkipCompile)
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
if(-not $SkipCompile) {
    dotnet build .\RDL-AccountManager.csproj --configuration Release
    if($LASTEXITCODE -ne 0){throw 'Build failed'}
}
$packageDir=Join-Path $PSScriptRoot 'dist\RDL-AccountManager'
New-Item -ItemType Directory -Force -Path $packageDir | Out-Null
foreach($name in @('RDL-AccountManager.exe','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "bin\Release\net48\$name") -Destination $packageDir
}
foreach($name in @('README.md','PRIVACY.md','CODE-SIGNING-POLICY.md','WebView2-LICENSE.txt','WebView2-NOTICE.txt')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $packageDir
}
# Only include a final LICENSE once the owner approves the license and asset rights.
if(Test-Path -LiteralPath 'LICENSE'){Copy-Item -LiteralPath 'LICENSE' -Destination $packageDir}
Compress-Archive -Path $packageDir -DestinationPath (Join-Path $PSScriptRoot 'dist\RDL-AccountManager-unsigned.zip') -Force

