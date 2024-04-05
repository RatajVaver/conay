#!/bin/sh

cd "$(dirname "$0")"
cd ..

create-version-file metadata.yml --outfile version.txt
pyinstaller conay.py -F -n Conay -i assets/icon.ico --version-file version.txt
pyinstaller gui.py -F -w -n ConayGUI -i assets/icon.ico --version-file version.txt

cd dist
mkdir -p Conay/assets
cp ../assets/icon.ico Conay/assets/icon.ico
cp ../assets/default.ico Conay/assets/default.ico
cp -r ../assets/servers Conay/assets/servers
cp ../assets/instructions.txt Conay/README.txt
cp Conay.exe Conay/Conay.exe
cp ConayGUI.exe Conay/ConayGUI.exe
zip -r Conay.zip Conay
rm -r Conay

cd ..
makensis installer.nsi

cd scripts
python update_server_list.py