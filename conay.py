import os
import re
import sys
import json
import shutil
import argparse
import requests
import subprocess
import webbrowser
from time import sleep
from zipfile import ZipFile
from datetime import datetime

os.chdir(os.path.dirname(os.path.abspath(sys.argv[0])))

LAUNCH = False
UPDATE_ALL = False
VERIFY = True
VERBOSE = False
KEEP_OPEN = False
PLAIN = False
SERVER_IP = ""

VERSION = "0.0.5"
GITHUB_REPOSITORY = "RatajVaver/conay"

STEAMCMD_PATH = "./steamcmd"
STEAM_LIBRARY_PATH = "../../../../../" # from steamapps\common\Conan Exiles\ConanSandbox\Conay
MODLIST_PATH = "../Mods/modlist.txt"
GAMEINI_PATH = "../Saved/Config/WindowsNoEditor/Game.ini"
EXE_PATH = "../Binaries/Win64/ConanSandbox.exe"

STEAM_LIBRARY_PATH = os.path.abspath(STEAM_LIBRARY_PATH)

HEADERS = {'Accept-Encoding': 'gzip'}
SESSION = requests.Session()

UPDATE_PATTERN = re.compile(r"workshopAnnouncement.*?<p id=\"(\d+)\">", re.DOTALL)
TITLE_PATTERN = re.compile(r"(?<=<div class=\"workshopItemTitle\">)(.*?)(?=<\/div>)", re.DOTALL)
WORKSHOP_CHANGELOG_URL = "https://steamcommunity.com/sharedfiles/filedetails/changelog"

def main(): 
    parseArguments()
    pathCheck()
    fprint("<📂> Steam Library Path: {}".format(STEAM_LIBRARY_PATH))
    versionCheck()
    installSteamCmd()
    mods, modNames = parseModlist()

    if UPDATE_ALL:
        downloadList(mods)
    else:
        checkUpdates(mods, modNames)

    fprint("<🆗\033[96m> All done!<\033[0m>")

    if SERVER_IP == "":
        if LAUNCH:
            fprint("<🎲> Launching the game..".format(SERVER_IP))
            webbrowser.open("steam://run/440900/")
            if not KEEP_OPEN:
                sleep(10)
    else:
        continueSession = False
        if LAUNCH:
            fprint("<🎲> Launching the game and connecting to the selected server ({})..".format(SERVER_IP))

            if os.path.exists(GAMEINI_PATH) and os.path.exists(EXE_PATH):
                try:
                    content = ""
                    with open(GAMEINI_PATH, "r", encoding="utf16") as file:
                        for line in file:
                            if line.startswith("LastConnected="):
                                content = content + "LastConnected=" + SERVER_IP + "\n"
                            elif line.startswith("StartedListenServerSession="):
                                content = content + "StartedListenServerSession=False\n"
                            else:
                                content = content + line

                    with open(GAMEINI_PATH, "w", encoding="utf16") as file:
                        file.write(content)

                    subprocess.Popen("\"{}\" -continuesession".format(os.path.abspath(EXE_PATH)))
                    continueSession = True
                except:
                    webbrowser.open("steam://run/440900//+connect {}/".format(SERVER_IP))
            else:
                webbrowser.open("steam://run/440900//+connect {}/".format(SERVER_IP))

        subprocess.check_call("echo {}|clip".format(SERVER_IP), shell=True)

        if LAUNCH:
            fprint("<🔔> TIP: Server IP was saved to your clipboard. If the launcher doesn't connect you directly to the server, you can use Ctrl+V in Direct Connect.")
            if not KEEP_OPEN:
                if continueSession:
                    fprint("<🗿> This window will close in 20 seconds, or you can close it manually. Conan Exiles might take a moment to launch.")
                    sleep(10)
                sleep(10)
        else:
            fprint("<🔔> TIP: Server IP was saved to your clipboard. You can use Ctrl+V later on in Direct Connect.")

    if KEEP_OPEN:
        fprint("<🙏> Conay will remain open until you close it or press a key.")
        os.system("pause")
    else:
        sleep(3)

def parseArguments():
    global STEAMCMD_PATH, STEAM_LIBRARY_PATH, MODLIST_PATH, UPDATE_ALL, LAUNCH, VERIFY, VERBOSE, KEEP_OPEN, PLAIN

    parser = argparse.ArgumentParser(description="Conan Exiles Mod Launcher")
    parser.add_argument('-d','--dev', help="Debugging outside of Conan Exiles folder", action='store_true')
    parser.add_argument('-f','--force', help="Force update of all mods, let SteamCMD decide", action='store_true')
    parser.add_argument('-l','--launch', help="Launch the game after updates are downloaded", action='store_true')
    parser.add_argument('-n','--nomods', help="Clear the modlist", action='store_true')
    parser.add_argument('-r','--restore', help="Restore the modlist", action='store_true')
    parser.add_argument('-s','--server', help="Load a modlist of a supported server or from local file")
    parser.add_argument('-c','--copy', help="Copy the current modlist into a server modlist file")
    parser.add_argument('-v','--verbose', help="Print all SteamCMD output", action='store_true')
    parser.add_argument('-x','--skip', help="Skip verifying of mod downloads, just trust SteamCMD", action='store_true')
    parser.add_argument('-k','--keep', help="Keep the app open after everything is done", action='store_true')
    parser.add_argument('-p','--plain', help="Display only plain text, no emojis or colors", action='store_true')
    parser.add_argument('-e','--emoji', help="Display emojis and colors (default on W11)", action='store_true')
    args = vars(parser.parse_args())

    if args['dev']:
        STEAMCMD_PATH = "../tests/steamcmd"
        STEAM_LIBRARY_PATH = os.path.abspath("../tests/mods")
        MODLIST_PATH = "../tests/modlist.txt"

    if args['force']:
        UPDATE_ALL = True

    if args['launch']:
        LAUNCH = True

    if args['verbose']:
        VERBOSE = True

    if args['skip']:
        VERIFY = False

    if args['keep']:
        KEEP_OPEN = True

    if args['plain']:
        PLAIN = True
    elif args['emoji']:
        PLAIN = False
    else:
        PLAIN = not isWin11()

    if args['server']:
        pathCheck()
        loadServerData(args['server'])
    elif args['nomods']:
        pathCheck()
        disableMods()
    elif args['restore']:
        pathCheck()
        restoreMods()
    elif args['copy']:
        pathCheck()
        MODLIST_PATH = "../servermodlist.txt"
        saveServerData(args['copy'])

def fprint(text, newLine=True):
    if PLAIN:
        text = re.sub(r'\<.+?\>', '', text).strip()
    else:
        text = text.replace('<','').replace('>','')

    if newLine:
        print(text)
    else:
        print(text, end='')

def isWin11():
    return sys.getwindowsversion().build >= 22000

def disableMods():
    fprint("<🔃> Disabling mods..")
    if os.path.exists(MODLIST_PATH):
        try:
            os.rename(MODLIST_PATH, MODLIST_PATH.replace(".txt",".bak"))
        except WindowsError:
            os.remove(MODLIST_PATH.replace(".txt",".bak"))
            os.rename(MODLIST_PATH, MODLIST_PATH.replace(".txt",".bak"))

def restoreMods():
    fprint("<🔃> Restoring mods..")
    if os.path.exists(MODLIST_PATH.replace(".txt",".bak")):
        try:
            os.rename(MODLIST_PATH.replace(".txt",".bak"), MODLIST_PATH)
        except WindowsError:
            os.remove(MODLIST_PATH)
            os.rename(MODLIST_PATH.replace(".txt",".bak"), MODLIST_PATH)

def saveServerData(server):
    if not server.isalnum():
        fprint("<❌\033[91m> Invalid server name format, use alphanumeric characters only!<\033[0m>")
        sleep(5)
        sys.exit(1)

    if not os.path.exists("./servers"):
        os.mkdir("./servers")

    modlist = []
    mods, modNames = parseModlist()

    for x, modId in enumerate(mods):
        entry = "{}/{}.pak".format(modId, modNames[x])
        modlist.append(entry)

    serverData = { "name": server, "ip": "", "mods": modlist }
    jsonData = json.dumps(serverData, indent=4)

    with open("./servers/{}.json".format(server), "w") as outfile:
        outfile.write(jsonData)

    fprint("<✅> Modlist was saved to a local server file (servers/{}.json)".format(server))

    if KEEP_OPEN:
        fprint("<🙏> Conay will remain open until you close it or press a key.")
        os.system("pause")
    else:
        sleep(3)

    sys.exit(0)

def loadServerData(server):
    global SERVER_IP

    fprint("<🔍> Searching for server '{}'..".format(server))

    if not server.isalnum():
        fprint("<❌\033[91m> Invalid server name format, use alphanumeric characters only!<\033[0m>")
        sleep(5)
        sys.exit(1)

    serverData = None
    if os.path.exists("./servers/{}.json".format(server)):
        try:
            fprint("<🧰> Using local server file..")
            file = open("./servers/{}.json".format(server), 'r', encoding="utf-8")
            serverData = json.load(file)
        except:
            fprint("<❌\033[91m> Failed to parse the local file, likely invalid JSON.<\033[0m>")
            sleep(5)
            sys.exit(1)
    else:
        response = SESSION.get("https://raw.githubusercontent.com/{}/main/servers/{}.json".format(GITHUB_REPOSITORY, server))
        if response.status_code == 404:
            fprint("<❌\033[91m> Unsupported server! Cannot fetch data.<\033[0m>")
            sleep(5)
            sys.exit(1)

        response = response.content.decode("utf-8")
        serverData = json.loads(response)

    fprint("<🔮> Processing modlist for server <\033[1m\033[92m>{}<\033[0m>..".format(serverData['name']))
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

    fprint("<✅> Modlist saved! Proceeding..")

def versionCheck():
    fprint("<🧰> Checking Conay updates..")
    try:
        if "-" in VERSION:
            fprint("<🔶> You are using unreleased version of Conay ({}) - this might affect stability!".format(VERSION))
        else:
            response = SESSION.get("https://api.github.com/repos/{}/releases/latest".format(GITHUB_REPOSITORY))
            if response.status_code == 200:
                response = response.content.decode("utf-8")
                releaseData = json.loads(response)
                if releaseData['tag_name']:
                    if releaseData['tag_name'] == VERSION:
                        fprint("<✅> You are using the latest version of Conay ({})".format(VERSION))
                    else:
                        localVersion = re.sub(r'[^0-9.]', '', VERSION).split('.')
                        remoteVersion = releaseData['tag_name'].split('.')
                        if remoteVersion[0] > localVersion[0]:
                            fprint("<🔶> There's a new major update available for Conay, consider updating the app! (<\033[91m>{}<\033[0m> → <\033[1m\033[92m>{}<\033[0m>)".format(VERSION, releaseData['tag_name']))
                            fprint("<👉> {}".format(releaseData['html_url']))
                            sleep(7)
                        elif remoteVersion[0] == localVersion[0] and remoteVersion[1] > localVersion[1]:
                            fprint("<🔶> There's a new minor update available for Conay, consider updating the app! (<\033[91m>{}<\033[0m> → <\033[1m\033[92m>{}<\033[0m>)".format(VERSION, releaseData['tag_name']))
                            fprint("<👉> {}".format(releaseData['html_url']))
                            sleep(5)
                        elif remoteVersion[0] == localVersion[0] and remoteVersion[1] == localVersion[1] and remoteVersion[2] > localVersion[2]:
                            fprint("<🔶> There's a new patch available for Conay, consider updating the app! (<\033[91m>{}<\033[0m> → <\033[1m\033[92m>{}<\033[0m>)".format(VERSION, releaseData['tag_name']))
                            fprint("<👉> {}".format(releaseData['html_url']))
                            sleep(3)
                        else:
                            fprint("<🔶> Your version ({}) doesn't match the latest release ({})".format(VERSION, releaseData['tag_name']))
    except:
        fprint("<❌\033[91m> Checking Conay updates failed.<\033[0m>")

def pathCheck():
    if not os.path.exists(MODLIST_PATH.replace("modlist.txt","")) or not os.path.exists(os.path.join(STEAM_LIBRARY_PATH, "steamapps")):
        fprint("<❌\033[91m> Conay is not installed in the correct path! Please follow install instructions.<\033[0m>")
        sleep(5)
        sys.exit(1)

def parseModlist():
    fprint("<👓> Reading modlist..")
    modlistIds, modlistNames = [], []

    if os.path.exists(MODLIST_PATH):
        modlistFile = open(MODLIST_PATH, 'r')
        modlistLines = modlistFile.readlines()

        fprint("<🔮> Parsing modlist..")
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

        fprint("<✅> <\033[1m\033[92m>{}<\033[0m> mods found!".format(count))
    else:
        fprint("<❎> Modlist file not found! Running without mods..")

    return modlistIds, modlistNames

def installSteamCmd():
    try:
        if not os.path.exists(os.path.join(STEAMCMD_PATH, "steamcmd.exe")):
            fprint("<🔽> Downloading SteamCMD..")

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
                    fprint("<✅> Downloaded from {}".format(downloadPath))
                    downloaded = True
                    break
                else:
                    fprint("<❌\033[91m> Failed to download {}<\033[0m>".format(url))

            if not downloaded:
                fprint("<❌\033[91m> All download attempts failed!<\033[0m>")
                sleep(5)
                sys.exit(1)

            fprint("<📦> Extracting SteamCMD..")
            with ZipFile(downloadPath, 'r') as zipRef:
                zipRef.extractall(STEAMCMD_PATH)

            os.remove(downloadPath)

    except Exception as ex:
        print(ex)
        sleep(5)
        sys.exit(1)

    fprint("<🔥> Performing SteamCMD warmup and checking updates..")
    if VERBOSE:
        subprocess.call([os.path.abspath(os.path.join(STEAMCMD_PATH, 'steamcmd.exe')), "+quit"])
    else:
        subprocess.call([os.path.abspath(os.path.join(STEAMCMD_PATH, 'steamcmd.exe')), "+quit"], stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT)

def downloadList(modlist):
    args = [os.path.join(STEAMCMD_PATH, 'steamcmd.exe')]
    args.append('+force_install_dir "{}"'.format(STEAM_LIBRARY_PATH))
    args.append("+login anonymous")
    for modId in modlist:
        args.append("+workshop_download_item 440900 {} validate".format(int(modId)))
    args.append("+quit")

    try:
        subprocess.call(args)
    except Exception as ex:
        print(ex)
        sleep(5)
    print("")

def downloadMod(modId):
    args = [os.path.abspath(os.path.join(STEAMCMD_PATH, 'steamcmd.exe'))]
    args.append('+force_install_dir "{}"'.format(STEAM_LIBRARY_PATH))
    args.append("+login anonymous")
    args.append("+workshop_download_item 440900 {} validate".format(int(modId)))
    args.append("+quit")

    try:
        if VERIFY:
            proc = None
            if VERBOSE:
                proc = subprocess.Popen(args)
            else:
                proc = subprocess.Popen(args, stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT)

            fileCreated = False
            secondsPassed = 0
            while proc.poll() is None:
                if not VERBOSE:
                    print(".", end='', flush=True)

                sleep(3)
                secondsPassed += 3

                if not fileCreated:
                    fileCreated = os.path.exists(os.path.join(STEAM_LIBRARY_PATH, "steamapps/workshop/downloads/440900", modId))

                if secondsPassed > 10 and not fileCreated:
                    if VERBOSE:
                        fprint("\n<❌\033[91m> SteamCMD failed to download the mod!<\033[0m>", False)

                    while proc.poll() is None:
                        proc.kill()
                        sleep(1)
        else:
            subprocess.call(args)

    except Exception as ex:
        print(ex)
        sleep(5)
    print("")

def needsUpdate(modId, path):
    if os.path.isdir(path) and len(os.listdir(path)) > 0:
        response = SESSION.get("{}/{}".format(WORKSHOP_CHANGELOG_URL, modId), headers=HEADERS)
        response = response.content.decode("utf-8")
        matchUpdate = UPDATE_PATTERN.search(response)
        matchTitle = TITLE_PATTERN.search(response)

        if matchUpdate and matchTitle:
            updated = datetime.fromtimestamp(int(matchUpdate.group(1)))
            created = datetime.fromtimestamp(os.path.getmtime(path))

            return updated >= created, matchTitle.group(1).replace("&amp;", "&")

    return True, ""

def checkUpdates(modlistIds, modlistNames):
    fprint("<🔽> Checking mod updates..")
    for x, modId in enumerate(modlistIds):
        modPath = os.path.join(STEAM_LIBRARY_PATH, "steamapps/workshop/content/440900", modId)
        updateNeeded, modTitle = needsUpdate(modId, modPath)
        if modTitle == "":
            modTitle = modlistNames[x]
        modTitle = modTitle.strip()
        modTitle = modTitle.replace("&quot;", "\"")

        if updateNeeded:
            if VERIFY:
                fprint("<⌛> Downloading mod #{} ({}) ".format(modId, modTitle), VERBOSE)

                createdOld = datetime.fromtimestamp(0)
                if os.path.isdir(modPath):
                    createdOld = datetime.fromtimestamp(os.path.getmtime(modPath))
                    try:
                        shutil.rmtree(modPath)
                    except Exception as ex:
                        print(ex)
                        fprint("<❌\033[91m> Cannot edit files, the game might be running!<\033[0m>")
                        sleep(5)
                        sys.exit(1)

                verified = False

                while not verified:
                    downloadMod(modId)
                    if os.path.isdir(modPath) and len(os.listdir(modPath)) > 0:
                        createdNew = datetime.fromtimestamp(os.path.getmtime(modPath))
                        if createdNew > createdOld:
                            verified = True
                            fprint("<✅> Download complete and verified!")
                        else:
                            fprint("<🔃> Failed to verify download, retrying ", VERBOSE)
                    else:
                        fprint("<🔃> Failed to verify download, retrying ", VERBOSE)

            else:
                fprint("<⌛> Downloading mod #{} ({})..".format(modId, modTitle))
                downloadMod(modId)
        else:
            fprint("<✅> No update required for #{} ({})".format(modId, modTitle))

if __name__ == "__main__":
    main()
