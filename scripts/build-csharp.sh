#!/bin/sh

cd "$(dirname "$0")"

#cd ../Conay
#dotnet publish -c Release --self-contained -p:PublishSingleFile=True -p:PublishReadyToRun=True

cd ../dist
mkdir -p Conay
cp ../Conay/bin/Release/net9.0/win-x64/publish/Conay.exe ./Conay
cp ../Conay/bin/Release/net9.0/win-x64/publish/av_libglesv2.dll ./Conay
cp ../Conay/bin/Release/net9.0/win-x64/publish/libHarfBuzzSharp.dll ./Conay
cp ../Conay/bin/Release/net9.0/win-x64/publish/libSkiaSharp.dll ./Conay
cp ../Conay/bin/Release/net9.0/win-x64/publish/steam_api64.dll ./Conay
zip -FSr Conay.zip Conay

cd ..
makensis installer.nsi

rm -r dist/Conay