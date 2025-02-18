import os
import re
import sys
import json
import math
import pyuac
import shutil
import argparse
import requests
import subprocess
import webbrowser
from time import sleep,time
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
SERVER_PASSWORD = ""
SERVER_ORIGIN = "github"
SINGLEPLAYER = False
MENU = False

VERSION = "0.1.0"
GITHUB_REPOSITORY = "RatajVaver/conay"

STEAMCMD_PATH = "./steamcmd"
STEAM_LIBRARY_PATH = "../../../../../" # from steamapps\common\Conan Exiles\ConanSandbox\Conay
MODLIST_PATH = "../Mods/modlist.txt"
GAMEINI_PATH = "../Saved/Config/WindowsNoEditor/Game.ini"
EXE_PATH = "../Binaries/Win64/ConanSandbox.exe"

STEAM_LIBRARY_PATH = os.path.abspath(STEAM_LIBRARY_PATH)

HEADERS = {'Accept-Encoding': 'gzip'}
SESSION = requests.Session()
SESSION.headers.update({'User-Agent': "Conay v{}".format(VERSION)})

WORKSHOP_API_URL = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/"
ORIGIN_URLS = {
    "github": "https://raw.githubusercontent.com/{}/main/servers/{}.json".format(GITHUB_REPOSITORY, "{}"),
    "ratajmods": "https://ratajmods.net/conay/servers/{}.json"
}

NON_WORKSHOP_MOD_SOURCES = [
    [ "ratajmods", "https://ratajmods.net/conay/mods.json", "https://ratajmods.net/assets/mods/{}.pak" ]
]

def main():
    parseArguments()
    pathCheck()

    if VERBOSE:
        fprint("<📂> Steam Library Path: {}".format(STEAM_LIBRARY_PATH))

    versionCheck()
    installSteamCmd()
    mods, modNames = parseModlist()

    steamMods, steamModNames, nonWorkshopMods = [], [], []
    for x, modId in enumerate(mods):
        if int(modId) < 0:
            nonWorkshopMods.append([ abs(int(modId)) - 1, modNames[x] ])
        else:
            steamMods.append(modId)
            steamModNames.append(modNames[x])

    if len(steamMods) > 0:
        if UPDATE_ALL:
            downloadList(steamMods)
        else:
            checkUpdates(steamMods, steamModNames)

    if len(nonWorkshopMods) > 0:
        checkNonWorkshopUpdates(nonWorkshopMods)

    fprint("<🆗\033[96m> All done!<\033[0m>")

    if SERVER_IP == "":
        if LAUNCH:
            fprint("<🎲> Launching the game..")

            if os.path.exists(EXE_PATH):
                subprocess.Popen(os.path.abspath(EXE_PATH))
            else:
                webbrowser.open("steam://run/440900/")

            if not KEEP_OPEN:
                sleep(15)
    else:
        continueSession = False
        if LAUNCH:
            if not SINGLEPLAYER and not MENU:
                fprint("<🎲> Launching the game and connecting to the selected server ({})..".format(SERVER_IP))

            if MENU and os.path.exists(EXE_PATH):
                fprint("<🎲> Launching the game..")
                subprocess.Popen(os.path.abspath(EXE_PATH))
            elif os.path.exists(GAMEINI_PATH) and os.path.exists(EXE_PATH):
                changed = updateIni("utf16")
                if not changed:
                    changed = updateIni("utf8")

                if changed:
                    subprocess.Popen("\"{}\" -continuesession".format(os.path.abspath(EXE_PATH)))
                    continueSession = True
                else:
                    webbrowser.open("steam://run/440900/")
            else:
                webbrowser.open("steam://run/440900/")

        subprocess.check_call("echo {}|clip".format(SERVER_IP), shell=True)

        if SINGLEPLAYER:
            fprint("<🎲> Launching the game and starting a singleplayer session..")
            if not KEEP_OPEN:
                if continueSession:
                    fprint("<🗿> This window will close in 20 seconds, or you can close it manually. Conan Exiles might take a moment to launch.")
                    sleep(10)
                sleep(10)
        elif MENU:
            fprint("<🔔> TIP: Server IP was saved to your clipboard. You can use Ctrl+V later on in Direct Connect.")
            if not KEEP_OPEN:
                sleep(15)
        elif LAUNCH:
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
    global STEAMCMD_PATH, STEAM_LIBRARY_PATH, MODLIST_PATH, UPDATE_ALL, LAUNCH, VERIFY, VERBOSE, KEEP_OPEN, PLAIN, SINGLEPLAYER, MENU, SERVER_ORIGIN

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
    parser.add_argument('-g','--single', help="Runs the selected modlist and starts a singleplayer session", action='store_true')
    parser.add_argument('-m','--menu', help="Hang in menu, don't connect directly to the server", action='store_true')
    parser.add_argument('-u','--update', help="Update Conay, download and install the newest version", action='store_true')
    parser.add_argument('-o','--origin', help="Choose the data source for the server file", choices=list(ORIGIN_URLS.keys()))
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

    if args['single']:
        LAUNCH = True
        SINGLEPLAYER = True

    if args['menu']:
        MENU = True

    if args['origin']:
        if args['origin'] in ORIGIN_URLS:
            SERVER_ORIGIN = args['origin']

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
    elif args['update']:
        if not pyuac.isUserAdmin():
            fprint("<🔃> Relaunching Conay as administrator..")
            try:
                if sys.argv[0].endswith(".py"):
                    pyuac.runAsAdmin([sys.executable] + sys.argv)
                else:
                    pyuac.runAsAdmin([sys.executable] + sys.argv[1:])
            except Exception as ex:
                if VERBOSE:
                    print(ex)
                fprint("<❌\033[91m> Conay needs administrator privileges to run a self-update!<\033[0m>")
                sleep(5)
                sys.exit(1)
            sys.exit(0)
        pathCheck()
        selfUpdate()

def fprint(text, newLine=True):
    if PLAIN:
        text = re.sub(r'\<.+?\>', '', text).strip()
    else:
        text = text.replace('<','').replace('>','')

    if newLine:
        print(text, flush=True)
    else:
        print(text, end='', flush=True)

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
        if int(modId) < 0:
            modId = "@{}".format(NON_WORKSHOP_MOD_SOURCES[abs(int(modId)) - 1])
        entry = "{}/{}.pak".format(modId, modNames[x])
        modlist.append(entry)

    lastIp = getLastIp("utf16")
    if lastIp == False:
        lastIp = getLastIp("utf8")

    serverData = { "name": server, "ip": lastIp or "", "mods": modlist }
    jsonData = json.dumps(serverData, indent=4)

    with open("./servers/{}.json".format(server), "w") as outfile:
        outfile.write(jsonData)

    fprint("<✅> Modlist was saved to a local server file (servers/{}.json)".format(server))

    os.startfile(os.path.abspath("./servers/{}.json".format(server)))

    if KEEP_OPEN:
        fprint("<🙏> Conay will remain open until you close it or press a key.")
        os.system("pause")
    else:
        sleep(3)

    sys.exit(0)

def loadServerData(server):
    global SERVER_IP, SERVER_PASSWORD, SERVER_ORIGIN, SINGLEPLAYER

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
        try:
            response = SESSION.get(ORIGIN_URLS[SERVER_ORIGIN].format(server))
            if response.status_code == 404:
                fprint("<❌\033[91m> Unsupported server! Cannot fetch data.<\033[0m>")
                sleep(5)
                sys.exit(1)

            response = response.content.decode("utf-8")
            serverData = json.loads(response)
        except:
            fprint("<❌\033[91m> Failed to load server data! Make sure you're connected to the internet and try again.<\033[0m>")
            sleep(5)
            sys.exit(1)

    fprint("<🔮> Processing modlist for server <\033[1m\033[92m>{}<\033[0m>..".format(serverData['name']))

    if "ip" in serverData:
        SERVER_IP = serverData['ip']

    if "password" in serverData:
        SERVER_PASSWORD = serverData['password']

    if SERVER_IP == "singleplayer":
        SINGLEPLAYER = True

    if os.path.exists(MODLIST_PATH):
        try:
            os.rename(MODLIST_PATH, MODLIST_PATH.replace(".txt",".bak"))
        except WindowsError:
            os.remove(MODLIST_PATH.replace(".txt",".bak"))
            os.rename(MODLIST_PATH, MODLIST_PATH.replace(".txt",".bak"))

    f = open(MODLIST_PATH, 'w', encoding="utf-8")
    for modFile in serverData['mods']:
        if modFile.startswith('@'):
            f.write(os.path.abspath(os.path.join(MODLIST_PATH.replace("modlist.txt",""), modFile)) + '\n')
        else:
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
                            sleep(5)
                        elif remoteVersion[0] == localVersion[0] and remoteVersion[1] > localVersion[1]:
                            fprint("<🔶> There's a new update available for Conay, consider updating the app! (<\033[91m>{}<\033[0m> → <\033[1m\033[92m>{}<\033[0m>)".format(VERSION, releaseData['tag_name']))
                            fprint("<👉> {}".format(releaseData['html_url']))
                            sleep(4)
                        elif remoteVersion[0] == localVersion[0] and remoteVersion[1] == localVersion[1] and remoteVersion[2] > localVersion[2]:
                            fprint("<🔶> There's a new patch available for Conay, consider updating the app! (<\033[91m>{}<\033[0m> → <\033[1m\033[92m>{}<\033[0m>)".format(VERSION, releaseData['tag_name']))
                            fprint("<👉> {}".format(releaseData['html_url']))
                            sleep(3)
                        else:
                            fprint("<🔶> Your version ({}) doesn't match the latest release ({})".format(VERSION, releaseData['tag_name']))
    except:
        fprint("<❌\033[91m> Checking Conay updates failed. Check your internet connection.<\033[0m>")

def pathCheck():
    if os.path.exists("../AssetRegistry.bin") and os.path.exists(os.path.join(STEAM_LIBRARY_PATH, "steamapps")):
        if not os.path.exists(MODLIST_PATH.replace("modlist.txt","")):
            fprint("<🔩> Mods folder not found! Creating..")
            os.mkdir(MODLIST_PATH.replace("modlist.txt",""))
    else:
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
                modlistIds.append(match.group(1))
                modlistNames.append(match.group(2))
            else:
                match = re.search("\@([a-z]+)\/(.*)\.pak", line.strip())
                if match:
                    for x, source in enumerate(NON_WORKSHOP_MOD_SOURCES):
                        if source[0] == match.group(1):
                            modlistIds.append(-1-x)
                            modlistNames.append(match.group(2))
                            break

        fprint("<✅> <\033[1m\033[92m>{}<\033[0m> mods found!".format(count))
    else:
        fprint("<❎> Modlist file not found! Running without mods..")

    return modlistIds, modlistNames

def getLastIp(encoding):
    lastIp = None
    singleplayer = False
    try:
        with open(GAMEINI_PATH, "r", encoding=encoding) as file:
            for line in file:
                if line.startswith("LastConnected="):
                    lastIp = line.replace("LastConnected=", "").replace("\n", "").strip()
                elif line.startswith("StartedListenServerSession=True"):
                    singleplayer = True
    except Exception as ex:
        if VERBOSE:
            print(ex)
        return False

    if singleplayer:
        lastIp = None

    return lastIp

def updateIni(encoding):
    try:
        content = ""
        with open(GAMEINI_PATH, "r", encoding=encoding) as file:
            for line in file:
                if line.startswith("LastConnected=") and SERVER_IP != "singleplayer":
                    content = content + "LastConnected=" + SERVER_IP + "\n"
                elif line.startswith("LastPassword=") and SERVER_PASSWORD != "":
                    content = content + "LastPassword=" + SERVER_PASSWORD + "\n"
                elif line.startswith("StartedListenServerSession="):
                    if SINGLEPLAYER:
                        content = content + "StartedListenServerSession=True\n"
                    else:
                        content = content + "StartedListenServerSession=False\n"
                else:
                    content = content + line

        with open(GAMEINI_PATH, "w", encoding=encoding) as file:
            file.write(content)

        return True
    except Exception as ex:
        if VERBOSE:
            print(ex)
        return False

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
                    fprint("<✅> Downloaded from {}".format(url.split('/')[2]))
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

    if LAUNCH:
        webbrowser.open("steam://-/")

def downloadList(modlist):
    args = [os.path.join(STEAMCMD_PATH, 'steamcmd.exe')]
    args.append('+force_install_dir "{}"'.format(STEAM_LIBRARY_PATH))
    args.append("+login anonymous")
    for modId in modlist:
        if int(modId) > 0:
            args.append("+workshop_download_item 440900 {} validate".format(int(modId)))
    args.append("+quit")

    try:
        subprocess.call(args)
    except Exception as ex:
        print(ex)
        sleep(5)
    print("")

def downloadMod(modId, retrying=False):
    args = [os.path.abspath(os.path.join(STEAMCMD_PATH, 'steamcmd.exe'))]
    args.append('+force_install_dir "{}"'.format(STEAM_LIBRARY_PATH))
    args.append("+login anonymous")
    args.append("+workshop_download_item 440900 {} validate".format(int(modId)))
    args.append("+quit")

    failedFirstTry = False

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
                    fileCreated = os.path.exists(os.path.join(STEAM_LIBRARY_PATH, "steamapps/workshop/downloads/440900", str(modId)))

                if secondsPassed > 10 and not fileCreated and not retrying:
                    if VERBOSE:
                        fprint("<🔃> Failed to verify download, retrying ", VERBOSE)

                    failedFirstTry = True
                    while proc.poll() is None:
                        proc.kill()
                        sleep(1)
        else:
            subprocess.call(args)

    except Exception as ex:
        print(ex)
        sleep(5)

    if not failedFirstTry or VERBOSE:
        print("")

def checkUpdates(modlistIds, modlistNames):
    fprint("<🔽> Checking mod updates..")

    postData = {
        "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8",
        "itemcount": len(modlistIds),
    }

    modsInfo = {}
    for x, modId in enumerate(modlistIds):
        postData["publishedfileids[{}]".format(x)] = modlistIds[x]
        modsInfo[int(modId)] = { "title": modlistNames[x] }

    if len(modlistIds) > 0:
        success = False
        attempt = 0
        while not success and attempt < 3:
            attempt = attempt + 1
            try:
                apiRequest = SESSION.post(WORKSHOP_API_URL, headers=HEADERS, data=postData)
                response = apiRequest.json()
                for modData in response['response']['publishedfiledetails']:
                    if "title" in modData and "time_updated" in modData:
                        modsInfo[int(modData['publishedfileid'])] = { "title": modData['title'], "updated": modData['time_updated'] }
                success = True
            except Exception as ex:
                if VERBOSE:
                    print(ex)
                fprint("<🔃> Failed to check updates! Trying again..")
                sleep(1)

        if not success:
            fprint("<❌\033[91m> Failed to check updates! Check your internet connection.<\033[0m>")
            fprint("<💔> Conay will now launch the game without updates, some mods may be outdated. Close this window if you don't want to run the game without updates.")
            sleep(5)

    for modId in modsInfo:
        updateNeeded = False
        modData = modsInfo[modId]
        modTitle = modData['title']
        modPath = os.path.join(STEAM_LIBRARY_PATH, "steamapps/workshop/content/440900", str(modId))
        if os.path.isdir(modPath) and len(os.listdir(modPath)) > 0:
            if 'updated' in modData:
                updated = datetime.fromtimestamp(modData['updated'])
                created = datetime.fromtimestamp(os.path.getmtime(modPath))
                updateNeeded = updated >= created
            else:
                updateNeeded = False
        else:
            updateNeeded = True

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
                retries = 0

                while not verified:
                    downloadMod(modId, retries > 0)
                    if os.path.isdir(modPath) and len(os.listdir(modPath)) > 0:
                        createdNew = datetime.fromtimestamp(os.path.getmtime(modPath))
                        if createdNew > createdOld:
                            verified = True
                            fprint("<✅> Download complete and verified!")
                        elif retries > 3:
                            fprint("<🔃> Failed after 3 tries, trying to reinstall mod ", VERBOSE)
                            shutil.rmtree(modPath)
                            retries = 1
                        else:
                            if VERBOSE or retries > 0:
                                fprint("<🔃> Failed to verify download, retrying ", VERBOSE)
                            retries = retries + 1
                    elif retries > 3:
                        fprint("<❌\033[91m> Failed to update mod! Run Conay again or try resubscribing this mod manually on Steam.<\033[0m>")
                        sleep(10)
                        sys.exit(1)
                    elif retries == 3:
                        fprint("<🔃> Failed after 3 tries, trying again with clear cache ", VERBOSE)
                        sleep(1)

                        cachePaths = [
                            modPath,
                            os.path.join(STEAM_LIBRARY_PATH, "steamapps/workshop/appworkshop_440900.acf"),
                            os.path.join(STEAM_LIBRARY_PATH, "steamapps/workshop/temp"),
                            os.path.join(STEAM_LIBRARY_PATH, "steamapps/workshop/downloads/state_440900_440900_{}.patch".format(modId)),
                            os.path.join(STEAM_LIBRARY_PATH, "steamapps/workshop/downloads/440900", str(modId)),
                        ]

                        try:
                            for cachePath in cachePaths:
                                if os.path.isdir(cachePath):
                                    shutil.rmtree(cachePath)
                                elif os.path.exists(cachePath):
                                    os.remove(cachePath)
                        except:
                            if VERBOSE:
                                fprint("<❌\033[91m> Failed to delete cache.<\033[0m>")

                        retries = retries + 1
                        sleep(1)
                    else:
                        if VERBOSE or retries > 0:
                            fprint("<🔃> Failed to verify download, retrying ", VERBOSE)
                        retries = retries + 1

            else:
                fprint("<⌛> Downloading mod #{} ({})..".format(modId, modTitle))
                downloadMod(modId)
        else:
            fprint("<✅> No update required for #{} ({})".format(modId, modTitle))

def checkNonWorkshopUpdates(mods):
    fprint("<🔽> Checking external mod updates..")

    modsInfo = {}
    for mod in mods:
        modsInfo[mod[1]] = { "title": mod[1], "source": mod[0] }

    for source in NON_WORKSHOP_MOD_SOURCES:
        success = False
        attempt = 0
        while not success and attempt < 3:
            attempt = attempt + 1
            try:
                apiRequest = SESSION.get(source[1], headers=HEADERS)
                response = apiRequest.json()
                for modData in response['mods']:
                    if modData['file'] in modsInfo:
                        if "file" in modData and "updated" in modData:
                            modsInfo[modData['file']]['updated'] = modData['updated']
                            if "title" in modData:
                                modsInfo[modData['file']]['title'] = modData['title']
                            if "size" in modData:
                                modsInfo[modData['file']]['size'] = math.ceil(modData['size'] / 1024 / 1024)
                success = True
            except Exception as ex:
                if VERBOSE:
                    print(ex)
                fprint("<🔃> Failed to check updates! Trying again..")
                sleep(1)

        if not success:
            fprint("<❌\033[91m> Failed to check updates from source: {}! Some mods may be outdated.<\033[0m>".format(source[0]))
            sleep(3)

    for modFile in modsInfo:
        updateNeeded = False
        modData = modsInfo[modFile]
        modTitle = modData['title']
        modSource = NON_WORKSHOP_MOD_SOURCES[modData['source']][0]
        modPath = os.path.join(MODLIST_PATH.replace("modlist.txt",""), "@{}/{}.pak".format(modSource, modFile))
        if os.path.exists(modPath) and os.path.isfile(modPath):
            if 'updated' in modData:
                updated = datetime.fromtimestamp(modData['updated'])
                created = datetime.fromtimestamp(os.path.getmtime(modPath))
                updateNeeded = updated >= created
            else:
                updateNeeded = False
        else:
            updateNeeded = True

        if updateNeeded:
            if 'size' in modData:
                fprint("<⌛> Downloading mod @{}/{} ({}) [{}MB] ..".format(modSource, modFile, modTitle, modData['size']), False)
            else:
                fprint("<⌛> Downloading mod @{}/{} ({}) ..".format(modSource, modFile, modTitle), False)
            downloadExternalMod(modData['source'], modFile)
        else:
            fprint("<✅> No update required for @{}/{} ({})".format(modSource, modFile, modTitle))

def downloadExternalMod(source, modFile):
    url = NON_WORKSHOP_MOD_SOURCES[source][2].format(modFile)
    dirPath = os.path.join(MODLIST_PATH.replace("modlist.txt",""), "@{}".format(NON_WORKSHOP_MOD_SOURCES[source][0]))
    filePath = os.path.join(MODLIST_PATH.replace("modlist.txt",""), "@{}/{}.pak".format(NON_WORKSHOP_MOD_SOURCES[source][0], modFile))

    if not os.path.exists(dirPath):
        os.mkdir(dirPath)

    try:
        seconds = time()
        with SESSION.get(url, stream=True) as r:
            r.raise_for_status()
            with open(filePath, 'wb') as f:
                for chunk in r.iter_content(chunk_size=8192): 
                    f.write(chunk)
                    if time() - seconds > 1:
                        seconds = time()
                        print(".", end='', flush=True)

        print("")
        fprint("<✅> Download complete!")
    except Exception as ex:
        print("")
        if VERBOSE:
            print(ex)
        fprint("<❌\033[91m> Failed to update mod! Try running Conay again.<\033[0m>")
        sleep(1)

def selfUpdate():
    downloadPath = "./ConayInstaller.exe"

    if os.path.exists(downloadPath):
        os.remove(downloadPath)

    fprint("<⌛> Downloading Conay update..")
    response = SESSION.get("https://github.com/{}/releases/latest/download/ConayInstaller.exe".format(GITHUB_REPOSITORY), allow_redirects=True)
    if response.status_code == 200:
        with open(downloadPath, 'wb') as f:
            f.write(response.content)

        try:
            subprocess.Popen("ConayInstaller.exe /D=" + os.path.abspath("../"))
        except Exception as ex:
            print(ex)
            sleep(3)

        sys.exit(0)
    else:
        fprint("<❌\033[91m> Failed to download update, please try again later or download it manually.<\033[0m>")

if __name__ == "__main__":
    main()
