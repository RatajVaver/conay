!define APP_NAME "Conay"
!define COMP_NAME "RatajVaver"
!define WEB_SITE "https://github.com/RatajVaver/conay"
!define VERSION "0.2.0.0"
!define COPYRIGHT "RatajVaver"
!define DESCRIPTION "Conan Exiles Mod Launcher"
!define INSTALLER_NAME "dist\ConayInstaller.exe"
!define MAIN_APP_EXE "Conay\Conay.exe"
!define INSTALL_TYPE "SetShellVarContext current"
!define MUI_ICON "assets\icon.ico"
!define MUI_WELCOMEFINISHPAGE_BITMAP "assets\header.bmp"
!define MUI_WELCOMEPAGE_TEXT "Conay is an open-source project made for the lovely Conan Exiles community, this launcher will always be free. Enjoy!\n\nIf you need assistance, feel free to join my Discord at: discord.gg/3WJNxCTn8m"

VIProductVersion "${VERSION}"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "CompanyName" "${COMP_NAME}"
VIAddVersionKey "LegalCopyright" "${COPYRIGHT}"
VIAddVersionKey "FileDescription" "${DESCRIPTION}"
VIAddVersionKey "FileVersion" "${VERSION}"

SetCompressor ZLIB
Name "${APP_NAME}"
Caption "${APP_NAME}"
OutFile "${INSTALLER_NAME}"
BrandingText "${APP_NAME}"
XPStyle on
InstallDir "$PROGRAMFILES\Steam\steamapps\common\Conan Exiles\ConanSandbox\"

!include "MUI.nsh"
!include "nsDialogs.nsh"
!include "LogicLib.nsh"

!define MUI_ABORTWARNING

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY

!define MUI_PAGE_CUSTOMFUNCTION_LEAVE post_install
!insertmacro MUI_PAGE_INSTFILES

!define MUI_FINISHPAGE_RUN_CHECKED
!define MUI_FINISHPAGE_RUN "$INSTDIR\${MAIN_APP_EXE}"
!define MUI_FINISHPAGE_RUN_PARAMETERS ""
!define MUI_FINISHPAGE_RUN_TEXT "Run Conay"
!define MUI_FINISHPAGE_LINK "Join my Discord"
!define MUI_FINISHPAGE_LINK_LOCATION "https://discord.gg/3WJNxCTn8m"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_LANGUAGE "English"

Section -MainProgram
  ${INSTALL_TYPE}
  SetOverwrite ifnewer
  SetOutPath "$INSTDIR\Conay"
  File "dist\Conay\Conay.exe"
  File "dist\Conay\av_libglesv2.dll"
  File "dist\Conay\libHarfBuzzSharp.dll"
  File "dist\Conay\libSkiaSharp.dll"
  File "dist\Conay\steam_api64.dll"
  Delete "$INSTDIR\Conay\ConayGUI.exe" # remove old version
  CreateShortcut "$DESKTOP\Conay.lnk" "$INSTDIR\Conay\Conay.exe"
SectionEnd

Function post_install
  SetOutPath "$INSTDIR\Conay"
Functionend