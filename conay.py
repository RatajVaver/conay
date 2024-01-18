import os
import re
import sys
import time
import json
import argparse
import requests
import subprocess
import webbrowser
from time import sleep
from zipfile import ZipFile
from datetime import datetime

LAUNCH = False
UPDATE_ALL = False
SERVER_IP = ""

STEAMCMD_PATH = "./steamcmd"
STEAM_LIBRARY_PATH = "../../../../../" # from steamapps\common\Conan Exiles\ConanSandbox\Conay
MODLIST_PATH = "../Mods/modlist.txt"

STEAM_LIBRARY_PATH = os.path.abspath(STEAM_LIBRARY_PATH)

HEADERS = {'Accept-Encoding': 'gzip'}
SESSION = requests.Session()

UPDATE_PATTERN = re.compile(r"workshopAnnouncement.*?<p id=\"(\d+)\">", re.DOTALL)
TITLE_PATTERN = re.compile(r"(?<=<div class=\"workshopItemTitle\">)(.*?)(?=<\/div>)", re.DOTALL)
WORKSHOP_CHANGELOG_URL = "https://steamcommunity.com/sharedfiles/filedetails/changelog"

def main(): 
    parseArguments()
    print("Steam Library Path: {}".format(STEAM_LIBRARY_PATH))

    pathCheck()
    installSteamCmd()
    mods, modNames = parseModlist()

    if UPDATE_ALL:
        downloadList(mods)
    else:
        checkUpdates(mods, modNames)

    print("Done!")

    if SERVER_IP == "":
        if LAUNCH:
            print("Launching the game..".format(SERVER_IP))
            webbrowser.open("steam://run/440900/")
            time.sleep(10)
    else:
        if LAUNCH:
            print("Launching the game and connecting to the selected server ({})..".format(SERVER_IP))
            webbrowser.open("steam://run/440900//+connect {}/".format(SERVER_IP))

        subprocess.check_call("echo {}|clip".format(SERVER_IP), shell=True)
        print("TIP: Server IP was saved to your clipboard. If the launcher doesn't connect you directly to the server, you can use Ctrl+V in Direct Connect.")

        time.sleep(10)

    time.sleep(2)

def parseArguments():
    global STEAMCMD_PATH, STEAM_LIBRARY_PATH, MODLIST_PATH, UPDATE_ALL, LAUNCH

    parser = argparse.ArgumentParser(description="Conan Exiles Mod Launcher")
    parser.add_argument('-d','--dev', help="Debugging outside of Conan Exiles folder", action='store_true')
    parser.add_argument('-f','--force', help="Force update of all mods", action='store_true')
    parser.add_argument('-l','--launch', help="Launch the game after updates are downloaded", action='store_true')
    parser.add_argument('-n','--nomods', help="Clear the modlist", action='store_true')
    parser.add_argument('-r','--restore', help="Restore the modlist", action='store_true')
    parser.add_argument('-s','--server', help="Load modlist of supported server")
    args = vars(parser.parse_args())

    if args['dev']:
        STEAMCMD_PATH = "../tests/steamcmd"
        STEAM_LIBRARY_PATH = "../tests/mods"
        MODLIST_PATH = "../tests/modlist.txt"

    if args['force']:
        UPDATE_ALL = True

    if args['launch']:
        LAUNCH = True

    if args['server']:
        loadServerData(args['server'])
    elif args['nomods']:
        disableMods()
    elif args['restore']:
        restoreMods()

def disableMods():
    print("Disabling mods..")
    if os.path.exists(MODLIST_PATH):
        try:
            os.rename(MODLIST_PATH, MODLIST_PATH.replace(".txt",".bak"))
        except WindowsError:
            os.remove(MODLIST_PATH.replace(".txt",".bak"))
            os.rename(MODLIST_PATH, MODLIST_PATH.replace(".txt",".bak"))

def restoreMods():
    print("Restoring mods..")
    if os.path.exists(MODLIST_PATH.replace(".txt",".bak")):
        try:
            os.rename(MODLIST_PATH.replace(".txt",".bak"), MODLIST_PATH)
        except WindowsError:
            os.remove(MODLIST_PATH)
            os.rename(MODLIST_PATH.replace(".txt",".bak"), MODLIST_PATH)

def loadServerData(server):
    global SERVER_IP

    print("Searching for server '{}'..".format(server))
    response = SESSION.get("https://raw.githubusercontent.com/RatajVaver/conay/main/servers/{}.json".format(server))
    if response.status_code == 404:
        print("Unsupported server! Cannot fetch data.")
        time.sleep(5)
        sys.exit(1)

    response = response.content.decode("utf-8")
    serverData = json.loads(response.read())
    print("Processing modlist for server {}..".format(serverData['name']))
    SERVER_IP = serverData['ip']

    if os.path.exists(MODLIST_PATH):
        try:
            os.rename(MODLIST_PATH, MODLIST_PATH.replace(".txt",".bak"))
        except WindowsError:
            os.remove(MODLIST_PATH.replace(".txt",".bak"))
            os.rename(MODLIST_PATH, MODLIST_PATH.replace(".txt",".bak"))

    f = open(MODLIST_PATH, 'w', encoding="utf-8")
    for modFile in serverData['mods']:
        f.write(os.path.abspath(os.path.join(STEAM_LIBRARY_PATH, "steamapps/workshop/content/440900", modFile)) + '\n')
    f.close()

    print("Modlist saved! Proceeding..")

def pathCheck():
    if not os.path.exists(MODLIST_PATH.replace("modlist.txt","")) or not os.path.exists(os.path.join(STEAM_LIBRARY_PATH, "steamapps")):
        print("Conay is not installed in the correct path! Please follow install instructions.")
        time.sleep(5)
        sys.exit(1)

def parseModlist():
    print("Reading modlist..")
    modlistIds, modlistNames = [], []

    if os.path.exists(MODLIST_PATH):
        modlistFile = open(MODLIST_PATH, 'r')
        modlistLines = modlistFile.readlines()

        print("Parsing modlist..")
        count = 0
        for line in modlistLines:
            count += 1
            line = line.replace("\\", "/")
            match = re.search("440900\/([0-9]+)\/(.*)\.pak", line.strip())
            if match:
                modId = match.group(1)
                modName = match.group(2)
                modlistIds.append(modId)
                modlistNames.append(modName)

        print("{} mods found!".format(count))
    else:
        print("Modlist file not found! Running without mods..")

    return modlistIds, modlistNames

def installSteamCmd():
    try:
        if not os.path.exists(os.path.join(STEAMCMD_PATH, "steamcmd.exe")):
            print("Downloading SteamCMD..")

            steamCmdMirrors = [
                "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip",
                "http://web.archive.org/web/2/https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip",
            ]

            downloadPath = "./steamcmd.zip"
            os.makedirs(STEAMCMD_PATH, exist_ok=True)

            downloaded = False
            for url in steamCmdMirrors:
                response = SESSION.get(url, allow_redirects=True)
                if response.status_code == 200:
                    with open(downloadPath, 'wb') as f:
                        f.write(response.content)
                    print(f"Downloaded from {downloadPath}")
                    downloaded = True
                    break
                else:
                    print(f"Failed to download {url}")

            if not downloaded:
                print("All download attempts failed!")
                time.sleep(5)
                sys.exit(1)

            print("Extracting SteamCMD..")
            with ZipFile(downloadPath, 'r') as zipRef:
                zipRef.extractall(STEAMCMD_PATH)

            os.remove(downloadPath)

    except Exception as ex:
        print(ex)
        time.sleep(5)
        sys.exit(1)

def streamProcess(process):
    go = process.poll() is None
    for line in process.stdout:
        print(line)
    return go

def downloadList(modlist):
    args = [os.path.join(STEAMCMD_PATH, 'steamcmd.exe')]
    args.append('+force_install_dir "{}"'.format(STEAM_LIBRARY_PATH))
    args.append("+login anonymous")
    for modId in modlist:
        args.append("+workshop_download_item 440900 {}".format(int(modId)))
    args.append("+quit")

    process = subprocess.Popen(args, stdout=subprocess.PIPE, errors='ignore', creationflags=subprocess.CREATE_NO_WINDOW)
    while streamProcess(process):
        sleep(0.1)

def downloadMod(modId):
    args = [os.path.join(STEAMCMD_PATH, 'steamcmd.exe')]
    args.append('+force_install_dir "{}"'.format(STEAM_LIBRARY_PATH))
    args.append("+login anonymous")
    args.append("+workshop_download_item 440900 {} validate".format(int(modId)))
    args.append("+quit")

    process = subprocess.Popen(args, stdout=subprocess.PIPE, errors='ignore', creationflags=subprocess.CREATE_NO_WINDOW)
    while streamProcess(process):
        sleep(0.1)

def needsUpdate(modId, path):
    if os.path.isdir(path):
        response = SESSION.get("{}/{}".format(WORKSHOP_CHANGELOG_URL, modId), headers=HEADERS)
        response = response.content.decode("utf-8")
        matchUpdate = UPDATE_PATTERN.search(response)
        matchTitle = TITLE_PATTERN.search(response)

        if matchUpdate and matchTitle:
            updated_at = datetime.fromtimestamp(int(matchUpdate.group(1)))
            created_at = datetime.fromtimestamp(os.path.getmtime(path))

            return updated_at >= created_at, matchTitle.group(1).replace("&amp;", "&")

    return True, ""

def checkUpdates(modlistIds, modlistNames):
    print("Checking mod updates..")
    for x, modId in enumerate(modlistIds):
        updateNeeded, modTitle = needsUpdate(modId, os.path.join(STEAM_LIBRARY_PATH, "steamapps/workshop/content/440900", modId))
        if modTitle == "":
            modTitle = modlistNames[x]
        modTitle = modTitle.strip()

        if updateNeeded:
            print("Downloading mod #{} ({})..".format(modId, modTitle))
            downloadMod(modId)
        else:
            print("No update required for #{} ({})".format(modId, modTitle))

if __name__ == "__main__":
    main()
