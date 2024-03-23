#!/bin/sh

create-version-file metadata.yml --outfile version.txt
pyinstaller conay.py -F -n Conay -i assets/icon.ico --version-file version.txt

cd dist
mkdir -p Conay
cp ../assets/instructions.txt Conay/README.txt
cp Conay.exe Conay/Conay.exe
zip -r Conay.zip Conay
rm -r Conay

cd ..
makensis installer.nsi