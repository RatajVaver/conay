#!/bin/sh

create-version-file metadata.yml --outfile version.txt
pyinstaller conay.py -F -n Conay -i assets/icon.ico --version-file version.txt