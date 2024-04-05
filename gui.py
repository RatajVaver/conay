from tkinter import *
from tkinter.messagebox import *
from CTkListbox import *
from PIL import Image
import customtkinter
import os
import sys
import json
import glob
import requests
import subprocess

os.chdir(os.path.dirname(os.path.abspath(sys.argv[0])))

SESSION = requests.Session()

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

        self.loadConfig()
        self.loadServers()

        # Vars

        self.launchToggle = IntVar( value = int(self.launcherConfig.get("launch", True) == True) )
        self.offlineToggle = IntVar( value = int(self.launcherConfig.get("offline", False) == True) )
        self.verboseToggle = IntVar( value = int(self.launcherConfig.get("verbose", False) == True) )
        self.directToggle = IntVar( value = int(self.launcherConfig.get("direct", True) == True) )
        self.cinematicToggle = IntVar( value = int(self.launcherConfig.get("disableCinematic", False) == True) )

        self.fancyToggle = IntVar( value = int(self.launcherConfig.get("fancy", None) == True) )
        if self.launcherConfig.get("fancy", None) == None:
            self.fancyToggle.set(-1)

        # Components

        self.serverList = CTkListbox(self)

        for i,x in enumerate(self.serverNames):
            self.serverList.insert(i, x)
            if self.serverFiles[i] == self.launcherConfig.get("favorite", ""):
                self.selectedServer = i

        self.serverList.select(self.selectedServer)
        self.serverList.pack(expand=True, fill=BOTH, side=LEFT, padx=10, pady=10)
        self.serverList.bind('<<ListboxSelect>>', self.selectServer)

        start = customtkinter.CTkButton(self, text="Run updater", command=self.startUpdater)
        start.pack(side="bottom", padx=20, pady=20)

        launchCheckBox = customtkinter.CTkCheckBox(self, text='Launch the game', variable=self.launchToggle, onvalue=1, offvalue=0, command=self.checkBoxChange)
        launchCheckBox.pack(side="bottom", padx=20)

        settings = customtkinter.CTkButton(self, text="Settings", fg_color="#191919", hover_color="#0c0c0c", command=self.openSettings)
        settings.pack(side="top", padx=20, pady=10)

        loadedImage = Image.open('assets/default.ico')
        self.defaultIcon = customtkinter.CTkImage(light_image=loadedImage, dark_image=loadedImage, size=(128,128))

        self.serverIcon = customtkinter.CTkLabel(self, text="", image=self.defaultIcon)
        self.serverIcon.pack(side="top", padx=10)

        self.updateServerSelection()

        self.attributes('-topmost', True)
        self.update()
        self.attributes('-topmost', False)

        self.protocol("WM_DELETE_WINDOW", self.saveAndExit)

    def saveAndExit(self):
        if self.saveConfigOnExit:
            self.saveConfig()
        self.destroy()

    def startUpdater(self):
        args = ["Conay.exe"]
        server = self.serverFiles[self.selectedServer]

        if server == "_vanilla":
            args.append("--nomods")
        elif server != "":
            args.append("--server")
            args.append(server)

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

        if self.saveConfigOnExit:
            self.saveConfig()

        self.destroy()
        sys.exit(0)

    def selectServer(self, _):
        selected = self.serverList.curselection()
        self.selectedServer = selected
        self.updateServerSelection()

    def updateServerSelection(self):
        iconPath = "assets/servers/{}.ico".format(self.serverFiles[self.selectedServer])
        if os.path.exists(iconPath):
            loadedImage = Image.open(iconPath)
            self.currentIcon = customtkinter.CTkImage(light_image=loadedImage, dark_image=loadedImage, size=(128,128))
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
        }

        if os.path.exists("config.json"):
            config = []
            try:
                with open("config.json", "r") as configFile:
                    config = json.load(configFile)
                    for x in config:
                        self.launcherConfig[x] = config[x]
            except:
                print("oops")

            if len(config) < len(self.launcherConfig):
                self.saveConfig()
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
            [ self.directToggle,    "direct",       True ],
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
            with open("../Mods/modlist.txt", 'r') as modlistFile:
                modCount = len(modlistFile.readlines())

        self.serverNames = ["Current modlist ({} mods)".format(modCount), "Vanilla game (no mods)"]
        self.serverFiles = ["","_vanilla"]

        # Local servers
        for filename in glob.glob("servers/*.json"):
            with open(filename, "r") as content:
                serverData = json.load(content)
                serverFile = os.path.basename(filename).replace(".json", "")
                if self.launcherConfig.get("favorite", "") == serverFile:
                    self.serverNames.insert(2, serverData['name'])
                    self.serverFiles.insert(2, serverFile)
                else:
                    self.serverNames.append(serverData['name'])
                    self.serverFiles.append(serverFile)

        # Remote servers
        if not self.launcherConfig.get("offline", False):
            try:
                response = SESSION.get("https://raw.githubusercontent.com/RatajVaver/conay/main/servers.json")
                if response.status_code != 200:
                    showwarning("Conay - Error", "Failed to download the server list!\nMake sure you're connected to the internet and try again.\n\nYou can set offline to true in config.json to only load your own local modlists and to hide this message.")
                    return

                response = response.content.decode("utf-8")
                servers = json.loads(response)

                for x in servers:
                    if x['file'] in self.serverFiles: # Local files have priority, but let's inform the user they're overwriting
                        serverIndex = self.serverFiles.index(x['file'])
                        self.serverNames[serverIndex] = "⚠ {}".format(self.serverNames[serverIndex])
                    else:
                        if self.launcherConfig.get("favorite", "") == x['file']:
                            self.serverNames.insert(2, x['name'])
                            self.serverFiles.insert(2, x['file'])
                        else:
                            self.serverNames.append(x['name'])
                            self.serverFiles.append(x['file'])
            except:
                showwarning("Conay - Error", "Failed to download the server list!\nMake sure you're connected to the internet and try again.\n\nYou can set offline to true in config.json to only load your own local modlists and to hide this message.")

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
        self.eval(f"tk::PlaceWindow {str(self.settingsWindow)} center")

        #customtkinter.CTkLabel(self.settingsWindow, text="").pack(fill="both", padx=10, pady=10)
        customtkinter.CTkButton(self.settingsWindow, text="Discord for support and updates", command=self.openDiscord).pack(fill="both", padx=10, pady=10)

        directCheckBox = customtkinter.CTkCheckBox(self.settingsWindow, text="Direct connect to server after launch", variable=self.directToggle, onvalue=1, offvalue=0, command=self.checkBoxChange)
        directCheckBox.pack(fill="both", padx=10, pady=5)

        offlineCheckBox = customtkinter.CTkCheckBox(self.settingsWindow, text="Offline mode (don't fetch remote servers)", variable=self.offlineToggle, onvalue=1, offvalue=0, command=self.checkBoxChange)
        offlineCheckBox.pack(fill="both", padx=10, pady=5)

        fancyCheckBox = customtkinter.CTkCheckBox(self.settingsWindow, text="Display emojis and colors in the updater", variable=self.fancyToggle, onvalue=1, offvalue=0, command=self.checkBoxChange)
        fancyCheckBox.pack(fill="both", padx=10, pady=5)

        verboseCheckBox = customtkinter.CTkCheckBox(self.settingsWindow, text="Print detailed info in the updater", variable=self.verboseToggle, onvalue=1, offvalue=0, command=self.checkBoxChange)
        verboseCheckBox.pack(fill="both", padx=10, pady=5)

        cinematicCheckBox = customtkinter.CTkCheckBox(self.settingsWindow, text="Disable Conan's cinematic intro", variable=self.cinematicToggle, onvalue=1, offvalue=0, command=self.toggleCinematic)
        cinematicCheckBox.pack(fill="both", padx=10, pady=5)

        customtkinter.CTkLabel(self.settingsWindow, text="Favorite server (selected on startup):").pack(fill="both", padx=10, pady=10)

        self.favoriteList = customtkinter.CTkComboBox(self.settingsWindow, values=self.serverFiles, command=self.saveFavorite)
        self.favoriteList.set(self.serverFiles[self.selectedServer])
        self.favoriteList.pack(fill=BOTH, padx=10, pady=0)

        self.settingsWindow.transient(self)
        self.settingsWindow.protocol("WM_DELETE_WINDOW", lambda : self.clearChildWindow("settingsWindow"))

    def clearChildWindow(self, window):
        getattr(self, window).destroy()
        setattr(self, window, None)

if __name__ == '__main__':
    app = App()
    app.mainloop()