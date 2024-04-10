!define APP_NAME "Conay"
!define COMP_NAME "RatajVaver"
!define WEB_SITE "https://github.com/RatajVaver/conay"
!define VERSION "0.0.8.0"
!define COPYRIGHT "RatajVaver"
!define DESCRIPTION "Conan Exiles Mod Launcher"
!define INSTALLER_NAME "dist\ConayInstaller.exe"
!define MAIN_APP_EXE "Conay\ConayGUI.exe"
!define INSTALL_TYPE "SetShellVarContext current"
!define MUI_ICON "assets\icon.ico"
!define MUI_WELCOMEFINISHPAGE_BITMAP "assets\header.bmp"
!define MUI_WELCOMEPAGE_TEXT "Conay is an open-source project made for the lovely Conan Exiles community, this launcher will always be free. Enjoy!\n\nIf you need assistance, feel free to join my Discord at: discord.gg/3WJNxCTn8m\n\nPlease make sure to install this application into your ConanSandbox folder! Installer will verify this for you."
!define MUI_COMPONENTSPAGE_NODESC
!define MUI_COMPONENTSPAGE_TEXT_TOP "If you want shortcuts on your desktop for specific servers, please check those that you want to have. You can also create your own shortcuts or even add your own supported server, check Conay's GitHub page or Discord for more details."
!define MUI_COMPONENTSPAGE_TEXT_COMPLIST "Select servers:"

VIProductVersion  "${VERSION}"
VIAddVersionKey "ProductName"  "${APP_NAME}"
VIAddVersionKey "CompanyName"  "${COMP_NAME}"
VIAddVersionKey "LegalCopyright"  "${COPYRIGHT}"
VIAddVersionKey "FileDescription"  "${DESCRIPTION}"
VIAddVersionKey "FileVersion"  "${VERSION}"

SetCompressor ZLIB
Name "${APP_NAME}"
Caption "${APP_NAME}"
OutFile "${INSTALLER_NAME}"
BrandingText "${APP_NAME}"
XPStyle on
InstallDir "$PROGRAMFILES\Steam\steamapps\common\Conan Exiles\ConanSandbox"

!include "MUI.nsh"
!include nsDialogs.nsh
!include LogicLib.nsh

!define MUI_ABORTWARNING

!insertmacro MUI_PAGE_WELCOME

!define MUI_PAGE_CUSTOMFUNCTION_LEAVE dir_leave
!insertmacro MUI_PAGE_DIRECTORY

!define MUI_PAGE_HEADER_SUBTEXT "Choose which servers do you want added to your desktop as shortcuts."
!insertmacro MUI_PAGE_COMPONENTS

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
File "dist\Conay.exe"
File "dist\ConayGUI.exe"
File /oname=README.txt "assets\instructions.txt"
SetOutPath "$INSTDIR\Conay\assets"
File "assets\icon.ico"
File "assets\default.ico"
SetOutPath "$INSTDIR\Conay\assets\servers"
File "assets\servers\"
SetOutPath "$INSTDIR\Conay"
SectionEnd

Function dir_leave
  IfFileExists "$INSTDIR\AssetRegistry.bin" end 0
    messagebox mb_ok|mb_iconstop "This is not the correct path to your Conan Exiles folder! Conay has to be installed into: steamapps\common\Conan Exiles\ConanSandbox!"
    Abort
  end:
Functionend

Function post_install
  SetOutPath "$INSTDIR\Conay"
Functionend

Section "Default desktop shortcut (GUI) - recommended"
  SectionIn 1
  SetOutPath "$INSTDIR\Conay"
  CreateShortcut "$DESKTOP\Conay.lnk" "$INSTDIR\Conay\ConayGUI.exe"
SectionEnd

Section /o "Halcyon D&D"
  SectionIn 1
  SetOutPath "$INSTDIR\Conay"
  CreateShortcut "$DESKTOP\Halcyon.lnk" "$INSTDIR\Conay\Conay.exe" "--server halcyon --launch"
SectionEnd

Section /o "Crossroads"
  SectionIn 1
  SetOutPath "$INSTDIR\Conay"
  CreateShortcut "$DESKTOP\Crossroads.lnk" "$INSTDIR\Conay\Conay.exe" "--server crossroads --launch" "$INSTDIR\Conay\assets\servers\crossroads.ico" 0
SectionEnd
