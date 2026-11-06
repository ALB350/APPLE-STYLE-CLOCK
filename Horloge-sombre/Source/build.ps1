$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$appRoot = Split-Path $PSScriptRoot -Parent
& "$framework\csc.exe" /nologo /target:winexe /optimize+ /win32icon:"$PSScriptRoot\Horloge.ico" /out:"$appRoot\Horloge-sombre.exe" /reference:"$framework\WPF\PresentationFramework.dll" /reference:"$framework\WPF\PresentationCore.dll" /reference:"$framework\WPF\WindowsBase.dll" /reference:System.Xaml.dll /reference:System.Xml.Linq.dll "$PSScriptRoot\Horloge.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation échouée' }
