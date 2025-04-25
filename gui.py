from tkinter import *
from tkinter.messagebox import *
from tkinter.simpledialog import askstring
from CTkListbox import *
from PIL import Image
import customtkinter
import os
import re
import sys
import json
import glob
import requests
import subprocess

os.chdir(os.path.dirname(os.path.abspath(sys.argv[0])))

VERSION = "0.1.1"
GITHUB_REPOSITORY = "RatajVaver/conay"

ORIGIN_LISTS = { # Higher priority first
    "ratajmods": "https://ratajmods.net/conay/servers.json",
    "github": "https://raw.githubusercontent.com/{}/main/servers.json".format(GITHUB_REPOSITORY),
}

SESSION = requests.Session()
SESSION.headers.update({'User-Agent': "Conay v{}".format(VERSION)})

class App(customtkinter.CTk):
    def __init__(self):
        super().__init__()

        customtkinter.set_appearance_mode("dark")
        customtkinter.set_default_color_theme("blue")

        self.rowconfigure(0, weight=1)
        self.columnconfigure(0, weight=1)
        self.title("Conay")
        self.resizable(False, False)
        self.minsize(470, 300)
        self.iconbitmap("assets/icon.ico", default="assets/icon.ico")
        self.eval("tk::PlaceWindow . center")

        self.saveConfigOnExit = False
        self.selectedServer = 0
        self.settingsWindow = None
        self.serverListWindow = None

        self.loadConfig()
        self.servers = []
        self.iconCache = {}

        # Vars

        self.launchToggle = IntVar( value = int(self.launcherConfig.get("launch", True) == True) )
        self.offlineToggle = IntVar( value = int(self.launcherConfig.get("offline", False) == True) )
        self.verboseToggle = IntVar( value = int(self.launcherConfig.get("verbose", False) == True) )
        self.forceToggle = IntVar( value = int(self.launcherConfig.get("force", False) == True) )
        self.directToggle = IntVar( value = int(self.launcherConfig.get("direct", True) == True) )
        self.updateToggle = IntVar( value = int(self.launcherConfig.get("checkUpdates", True) == True) )
        self.cinematicToggle = IntVar( value = int(self.launcherConfig.get("disableCinematic", False) == True) )

        self.fancyToggle = IntVar( value = int(self.launcherConfig.get("fancy", None) == True) )
        if self.launcherConfig.get("fancy", None) == None:
            self.fancyToggle.set(-1)

        # Components

        self.serverList = CTkListbox(self)
        self.serverList.pack(expand=True, fill=BOTH, side=LEFT, padx=10, pady=10)
        self.serverList.bind('<<ListboxSelect>>', self.selectServer)

        start = customtkinter.CTkButton(self, text="Run updater", command=self.startUpdater)
        start.pack(side=BOTTOM, padx=20, pady=20)

        launchCheckBox = customtkinter.CTkCheckBox(self, text="Launch the game", variable=self.launchToggle, onvalue=1, offvalue=0, command=self.checkBoxChange)
        launchCheckBox.pack(side=BOTTOM, padx=20)

        settings = customtkinter.CTkButton(self, text="Settings", fg_color="#191919", hover_color="#0c0c0c", command=self.openSettings)
        settings.pack(side=TOP, padx=20, pady=10)

        #serversButton = customtkinter.CTkButton(self, text="Servers", fg_color="#191919", hover_color="#0c0c0c", command=self.openServerList)
        #serversButton.pack(side=TOP, padx=20, pady=0)

        loadedImage = Image.open("assets/default.ico")
        self.defaultIcon = customtkinter.CTkImage(light_image=loadedImage, dark_image=loadedImage, size=(128,128))

        self.serverIcon = customtkinter.CTkLabel(self, text="", image=self.defaultIcon)
        self.serverIcon.pack(side=TOP, padx=10, pady=10)

        self.attributes("-topmost", True)
        self.update()
        self.attributes("-topmost", False)

        self.protocol("WM_DELETE_WINDOW", self.saveAndExit)

        if self.launcherConfig.get("checkUpdates", True):
            self.checkUpdates()

        self.fillServerSelection()

    def fillServerSelection(self):
        self.loadServers()

        for i,x in enumerate(self.servers):
            self.serverList.insert(i, x['name'])
            if x['file'] == self.launcherConfig.get("favorite", ""):
                self.selectedServer = i

        self.serverList.select(self.selectedServer)
        self.updateServerSelection()

    def saveAndExit(self):
        if self.saveConfigOnExit:
            self.saveConfig()
        self.destroy()

    def startUpdater(self):
        args = ["Conay.exe"]
        server = self.servers[self.selectedServer]

        if server['file'] == "_vanilla":
            args.append("--nomods")
        elif server['file'] != "":
            args.append("--server")
            args.append(server['file'])
            if "origin" in server:
                args.append("--origin")
                args.append(server['origin'])


        if self.launchToggle.get() == 1:
            args.append("--launch")

        if self.launcherConfig.get("fancy", None) == True:
            args.append("--emoji")
        elif self.launcherConfig.get("fancy", None) == False:
            args.append("--plain")

        if self.launcherConfig.get("force", False):
            args.append("--force")
        if self.launcherConfig.get("verbose", False):
            args.append("--verbose")

        if self.directToggle.get() == 0:
            args.append("--menu")

        if self.launcherConfig.get("debug", False):
            print(args)
        else:
            try:
                subprocess.Popen(args)
            except Exception as ex:
                print(args)
                print(ex)
                showerror("Conay - Error", "Cannot find Conay.exe, please reinstall the application.")
                return

        history = self.launcherConfig.get("history", [])
        if len(history) == 0 or history[0] is not server['file']:
            if server['file'] in history:
                history.remove(server['file'])
            history.insert(0, server['file'])
            self.saveConfigOnExit = True

        self.launcherConfig["history"] = history

        if self.saveConfigOnExit:
            self.saveConfig()

        self.destroy()
        sys.exit(0)

    def selectServer(self, _):
        selected = self.serverList.curselection()
        self.selectedServer = selected
        self.updateServerSelection()

    def updateServerSelection(self):
        selected = self.servers[self.selectedServer]
        iconImage = self.getServerIcon(selected['file'], 'icon' in selected and selected['icon'] or None)
        if iconImage:
            self.currentIcon = customtkinter.CTkImage(light_image=iconImage, dark_image=iconImage, size=(128,128))
            self.serverIcon.configure(image = self.currentIcon)
            self.serverIcon.image = self.currentIcon
        else:
            self.serverIcon.configure(image = self.defaultIcon)
            self.serverIcon.image = self.defaultIcon

    def loadConfig(self):
        # Default config
        self.launcherConfig = {
            "launch": True, # whether the checkbox is checked or not by default
            "fancy": None, # None = system default / True = emoji parameter / False = plain parameter
            "force": False,
            "verbose": False,
            "direct": True,
            "offline": False, # False = load remote server list / True = only read local json files
            "disableCinematic": False, # WhAt WiLl YoU dO, eXiLe?
            "favorite": "",
            "checkUpdates": True, # whether to check for Conay self updates on launch
            #"servers": {},
            "history": []
        }

        if os.path.exists("config.json"):
            config = []
            try:
                with open("config.json", "r") as configFile:
                    config = json.load(configFile)
                    for x in config:
                        self.launcherConfig[x] = config[x]
            except:
                showwarning("Conay - Warning", "Failed to read config! This might cause some issues.")

            if len(config) != len(self.launcherConfig):
                self.saveConfigOnExit = True
        else:
            self.saveConfig()

    def saveConfig(self):
        with open("config.json", "w") as configFile:
            configFile.write( json.dumps(self.launcherConfig, indent=4, sort_keys=True) )

    def checkBoxChange(self):
        self.saveConfigOnExit = True

        checkBoxes = [
            # Var object            Config name     Defaults to false? (otherwise requires 0 to be False)
            [ self.launchToggle,    "launch",       True ],
            [ self.offlineToggle,   "offline",      True ],
            [ self.fancyToggle,     "fancy",        False ],
            [ self.verboseToggle,   "verbose",      True ],
            [ self.forceToggle,     "force",        True ],
            [ self.directToggle,    "direct",       True ],
            [ self.updateToggle,    "checkUpdates", True ],
        ]

        for x in checkBoxes:
            if x[0].get() == 1:
                self.launcherConfig[x[1]] = True
            elif x[0].get() == 0 or x[2]:
                self.launcherConfig[x[1]] = False
            else:
                self.launcherConfig[x[1]] = None

    def loadServers(self):
        modCount = 0
        if os.path.exists("../Mods/modlist.txt"):
            with open("../Mods/modlist.txt", "r") as modlistFile:
                modCount = len(modlistFile.readlines())

        favorite = self.launcherConfig.get("favorite", "")
        history = self.launcherConfig.get("history", [])

        self.servers = [
            {
                "file": "",
                "name": "Current modlist ({} mods)".format(modCount),
                "rank": 99
            },
            {
                "file": "_vanilla",
                "name": "Vanilla game (no mods)",
                "rank": 98
            },
        ]

        serverFiles = ["","_vanilla"]

        # Local servers
        for filename in glob.glob("servers/*.json"):
            with open(filename, "r") as content:
                serverData = json.load(content)
                serverFile = os.path.basename(filename).replace(".json", "")
                self.servers.append({
                    "file": serverFile,
                    "name": serverData['name'],
                    "rank": 90 - len(history) - len(self.servers)
                })
                serverFiles.append(serverFile)

        # Remote servers
        if not self.launcherConfig.get("offline", False):
            failedOrigins = 0
            for origin in ORIGIN_LISTS:
                try:
                    response = SESSION.get(ORIGIN_LISTS[origin])
                    if response.status_code != 200:
                        failedOrigins += 1
                        continue

                    response = response.content.decode("utf-8")
                    servers = json.loads(response)

                    for x in servers:
                        if x['file'] in serverFiles: # Already exists = local file / remote file with higher priority
                            serverIndex = serverFiles.index(x['file'])
                            if "origin" not in self.servers[serverIndex]: # Local files have priority, but let's inform the user they're overwriting
                                self.servers[serverIndex]['name'] = "⚠ {}".format(self.servers[serverIndex]['name'])
                        else:
                            self.servers.append({
                                "file": x['file'],
                                "name": x['name'],
                                "icon": x['icon'],
                                "origin": origin,
                                "rank": 90 - len(history) - len(self.servers)
                            })
                            serverFiles.append(x['file'])
                except Exception as ex:
                    print(ex)
                    failedOrigins += 1

            if failedOrigins >= len(ORIGIN_LISTS):
                showwarning("Conay - Warning", "Failed to download the server list!\nMake sure you are connected to the internet and that your firewall is not blocking this application.\n\nYou can set offline to true in config.json to only load your own local modlists and to hide this message.")
            elif failedOrigins > 0:
                showwarning("Conay - Warning", "Failed to download part of the server list!\nSome servers may not appear.")

        for x in self.servers:
            if x['file'] == favorite:
                x['rank'] = 97
            elif x['file'] in history:
                x['rank'] = 90 - history.index(x['file'])

        self.servers.sort(key=lambda x: x['rank'], reverse=True)

    def getServerIcon(self, server, url = None):
        if server not in self.iconCache:
            self.iconCache[server] = None

            iconPath = "assets/servers/{}.ico".format(server)
            if os.path.exists(iconPath):
                self.iconCache[server] = Image.open(iconPath)
            elif url is not None:
                self.iconCache[server] = Image.open(SESSION.get(url, stream=True).raw)

        return self.iconCache[server]

    def openDiscord(self):
        os.system("start \"\" https://discord.gg/3WJNxCTn8m")

    def saveFavorite(self, _):
        selected = self.favoriteList.get()
        self.launcherConfig['favorite'] = selected
        self.saveConfigOnExit = True

    def toggleCinematic(self):
        defaultGamePath = "../Config/DefaultGame.ini"
        if os.path.exists(defaultGamePath):
            try:
                content = ""
                with open(defaultGamePath, "r", encoding="utf8") as file:
                    for line in file:
                        if self.cinematicToggle.get() == 1:
                            if line.startswith("+StartupMovies=") and len(line) > 18:
                                content = content + line.replace("+", "-")
                            else:
                                content = content + line
                        else:
                            if line.startswith("-StartupMovies=") and len(line) > 18:
                                content = content + line.replace("-", "+")
                            else:
                                content = content + line

                with open(defaultGamePath, "w", encoding="utf8") as file:
                    file.write(content)

                self.saveConfigOnExit = True

                if self.cinematicToggle.get() == 1:
                    self.launcherConfig['disableCinematic'] = True
                    showinfo("Conay - Success", "Cinematic intro has been disabled!\n\nYou will now see silent black screen when loading into the game.")
                else:
                    self.launcherConfig['disableCinematic'] = False
                    showinfo("Conay - Success", "Cinematic intro has been enabled!\n\n\"What will you do, exile?\" is back.")
            except Exception as ex:
                self.cinematicToggle.set( 1 - self.cinematicToggle.get() )
                showerror("Conay - Error", "Conay failed to overwrite DefaultGame.ini!\nMake sure the game is not running.\n\nWindows error:\n{}".format(ex))
        else:
            self.cinematicToggle.set( 1 - self.cinematicToggle.get() )
            showerror("Conay - Error", "DefaultGame.ini could not be found!\nMake sure Conay is installed correctly.")

    def createServer(self):
        name = askstring("Conay - Add a server", "Please enter a name for this server, use only lowercase alphanumeric characters and no spaces. Example: myserver")
        if name and len(name) > 0 and name.isalnum():
            args = ["Conay.exe", "--copy", name]

            try:
                subprocess.Popen(args)
            except Exception as ex:
                print(args)
                print(ex)
                showerror("Conay - Error", "Cannot find Conay.exe, please reinstall the application.")

            self.destroy()
            sys.exit(0)
        elif name:
            showerror("Conay - Error", "The server name can only contain lowercase alphanumeric characters and no spaces.")

    def checkUpdates(self):
        try:
            update = ""
            latestRelease = "N/A"

            if "-" in VERSION:
                print("Unreleased version, won't check updates!")
            else:
                response = SESSION.get("https://api.github.com/repos/{}/releases/latest".format(GITHUB_REPOSITORY))
                if response.status_code == 200:
                    response = response.content.decode("utf-8")
                    releaseData = json.loads(response)
                    if releaseData['tag_name']:
                        latestRelease = releaseData['tag_name']
                        if latestRelease != VERSION:
                            localVersion = re.sub(r'[^0-9.]', '', VERSION).split('.')
                            remoteVersion = latestRelease.split('.')
                            if remoteVersion[0] > localVersion[0]:
                                update = "major update"
                            elif remoteVersion[0] == localVersion[0] and remoteVersion[1] > localVersion[1]:
                                update = "update"
                            elif remoteVersion[0] == localVersion[0] and remoteVersion[1] == localVersion[1] and remoteVersion[2] > localVersion[2]:
                                update = "patch"

            if update != "":
                confirm = askyesno("Conay - Update", "There's a new {} ({}) available for Conay!\nWould you like to download it now?".format(update, latestRelease))
                if confirm:
                    args = ["Conay.exe", "--update"]

                    if self.launcherConfig.get("fancy", None) == True:
                        args.append("--emoji")
                    elif self.launcherConfig.get("fancy", None) == False:
                        args.append("--plain")

                    try:
                        subprocess.Popen(args)
                    except Exception as ex:
                        print(ex)
                        showerror("Conay - Error", "Cannot find Conay.exe, please reinstall the application.")

                    self.destroy()
                    sys.exit(0)
        except:
            print("Failed to check Conay updates!")

    def openSettings(self):
        if self.settingsWindow and self.settingsWindow.winfo_exists:
            self.settingsWindow.focus()
            return

        self.settingsWindow = customtkinter.CTkToplevel(self)
        self.settingsWindow.title("Conay - Settings")
        self.settingsWindow.resizable(False, False)
        self.settingsWindow.minsize(250, 320)
        self.settingsWindow.iconbitmap("assets/icon.ico")
        self.settingsWindow.after(210, lambda: self.settingsWindow.iconbitmap("assets/icon.ico"))
        self.eval("tk::PlaceWindow {} center".format(str(self.settingsWindow)))

        customtkinter.CTkButton(self.settingsWindow, text="Discord for support and updates", command=self.openDiscord).pack(fill=BOTH, padx=10, pady=10)
        customtkinter.CTkButton(self.settingsWindow, text="Add current modlist as a server", command=self.createServer).pack(fill=BOTH, padx=10, pady=0)
        customtkinter.CTkLabel(self.settingsWindow, text="", height=0).pack(fill=BOTH, padx=10, pady=5)

        directCheckBox = customtkinter.CTkCheckBox(self.settingsWindow, text="Direct connect to server after launch", variable=self.directToggle, onvalue=1, offvalue=0, command=self.checkBoxChange)
        directCheckBox.pack(fill=BOTH, padx=10, pady=5)

        offlineCheckBox = customtkinter.CTkCheckBox(self.settingsWindow, text="Offline mode (don't fetch remote servers)", variable=self.offlineToggle, onvalue=1, offvalue=0, command=self.checkBoxChange)
        offlineCheckBox.pack(fill=BOTH, padx=10, pady=5)

        fancyCheckBox = customtkinter.CTkCheckBox(self.settingsWindow, text="Display emojis and colors in the updater", variable=self.fancyToggle, onvalue=1, offvalue=0, command=self.checkBoxChange)
        fancyCheckBox.pack(fill=BOTH, padx=10, pady=5)

        verboseCheckBox = customtkinter.CTkCheckBox(self.settingsWindow, text="Print detailed info in the updater", variable=self.verboseToggle, onvalue=1, offvalue=0, command=self.checkBoxChange)
        verboseCheckBox.pack(fill=BOTH, padx=10, pady=5)

        forceCheckBox = customtkinter.CTkCheckBox(self.settingsWindow, text="Force updates (let SteamCMD decide)", variable=self.forceToggle, onvalue=1, offvalue=0, command=self.checkBoxChange)
        forceCheckBox.pack(fill=BOTH, padx=10, pady=5)

        updateCheckBox = customtkinter.CTkCheckBox(self.settingsWindow, text="Check for Conay updates on launch", variable=self.updateToggle, onvalue=1, offvalue=0, command=self.checkBoxChange)
        updateCheckBox.pack(fill=BOTH, padx=10, pady=5)

        cinematicCheckBox = customtkinter.CTkCheckBox(self.settingsWindow, text="Disable Conan's cinematic intro", variable=self.cinematicToggle, onvalue=1, offvalue=0, command=self.toggleCinematic)
        cinematicCheckBox.pack(fill=BOTH, padx=10, pady=5)

        customtkinter.CTkLabel(self.settingsWindow, text="", height=0).pack(fill=BOTH, padx=10, pady=5)
        customtkinter.CTkLabel(self.settingsWindow, text="Favorite server (selected on startup):").pack(fill=BOTH, padx=10, pady=0)

        serverFiles = []
        for x in self.servers:
            serverFiles.append(x['file'])

        self.favoriteList = customtkinter.CTkComboBox(self.settingsWindow, values=serverFiles, command=self.saveFavorite)
        self.favoriteList.set(self.launcherConfig.get("favorite", ""))
        self.favoriteList.pack(fill=BOTH, padx=10, pady=10)

        self.settingsWindow.transient(self)
        self.settingsWindow.protocol("WM_DELETE_WINDOW", lambda : self.clearChildWindow("settingsWindow"))

    def openServerList(self):
        if self.serverListWindow and self.serverListWindow.winfo_exists:
            self.serverListWindow.focus()
            return

        self.serverListWindow = customtkinter.CTkToplevel(self)
        self.serverListWindow.title("Conay - Servers")
        self.serverListWindow.resizable(False, False)
        self.serverListWindow.minsize(580, 380)
        self.serverListWindow.iconbitmap("assets/icon.ico")
        self.serverListWindow.grid_columnconfigure((0, 1), weight=1)
        self.serverListWindow.grid_rowconfigure((0), weight=1)
        self.serverListWindow.after(210, lambda: self.serverListWindow.iconbitmap("assets/icon.ico"))
        self.eval("tk::PlaceWindow {} center".format(str(self.serverListWindow)))

        serverListAll = CTkListbox(self.serverListWindow)
        serverListAll.grid(row=0, column=0, padx=10, pady=10, sticky="nsew")

        serverListSelected = CTkListbox(self.serverListWindow)
        serverListSelected.grid(row=0, column=1, padx=10, pady=10, sticky="nsew")

        customtkinter.CTkButton(self.serverListWindow, text="Add", command=self.openDiscord).grid(row=1, column=0, padx=10, pady=10, sticky="ew")
        customtkinter.CTkButton(self.serverListWindow, text="Remove", command=self.createServer).grid(row=1, column=1, padx=10, pady=10, sticky="ew")

        self.serverListWindow.transient(self)
        self.serverListWindow.protocol("WM_DELETE_WINDOW", lambda : self.clearChildWindow("serverListWindow"))

        selectedServers = self.launcherConfig.get("servers", {})

        for i,x in enumerate(self.servers):
            if x['name'] not in selectedServers:
                serverListAll.insert(i, x['name'])

        for x in selectedServers:
            serverListSelected.insert(i, x)

    def clearChildWindow(self, window):
        getattr(self, window).destroy()
        setattr(self, window, None)

if __name__ == '__main__':
    app = App()
    app.mainloop()