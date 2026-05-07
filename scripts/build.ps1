$ErrorActionPreference = "Stop"

Set-Location $PSScriptRoot
Set-Location ..\dist

# Windows package
New-Item -ItemType Directory -Force -Path Conay | Out-Null
Copy-Item ..\Conay\bin\Release\net9.0\win-x64\publish\Conay.exe .\Conay\
Copy-Item ..\Conay\bin\Release\net9.0\win-x64\publish\av_libglesv2.dll .\Conay\
Copy-Item ..\Conay\bin\Release\net9.0\win-x64\publish\libHarfBuzzSharp.dll .\Conay\
Copy-Item ..\Conay\bin\Release\net9.0\win-x64\publish\libSkiaSharp.dll .\Conay\
Copy-Item ..\Conay\bin\Release\net9.0\win-x64\publish\steam_api64.dll .\Conay\
Compress-Archive -Path .\Conay -DestinationPath .\Conay.zip -Force

# Linux package
New-Item -ItemType Directory -Force -Path ConayLinux | Out-Null
Copy-Item ..\Conay\bin\Release\net9.0\linux-x64\publish\* .\ConayLinux\
Copy-Item ..\assets\conay.desktop .\ConayLinux\conay.desktop
Copy-Item ..\assets\icon.png .\ConayLinux\conay.png
tar -czf conay-linux.tar.gz -C ConayLinux .

# Installer
Set-Location ..
makensis installer.nsi

Remove-Item -Recurse -Force dist\Conay
Remove-Item -Recurse -Force dist\ConayLinux
