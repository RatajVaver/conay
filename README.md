# Conay

[![MIT License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/RatajVaver/conay/blob/main/LICENSE)
[![MIT License](https://img.shields.io/badge/Discord-Rataj's_Workshop-blue.svg)](https://discord.gg/3WJNxCTn8m)

Conay is a launcher and mod updater for Conan Exiles. Main problem this app solves is the need to manually resubscribe Steam Workshop addons after every update unless you want to wait for Steam to pick it up by itself. This launcher can read your modlist as well as load modlists from supported servers to order them for you and check for updates and force their download, saving you a headache.

## Installation

Download the latest release from the [Releases](https://github.com/RatajVaver/conay/releases) tab.

Extract the Conay folder into `steamapps\common\Conan Exiles\ConanSandbox`.

It should look like this:

![ConanSandbox folder](assets/readme_folder.png)

This folder should contain your `Conay.exe` as a single file.
Alternatively it can contain `conay.py` if you want to run the Python script directly.
You can also build the exe on your own from the source using pyinstaller.

## Usage

For basic usage (updating your currently loaded mods) just run the `Conay.exe` file.

You can also create a desktop shortcut and use parameters to tell the app what to do.

Forcing update of all mods (SteamCMD will check updates on it's own):

```sh
Conay.exe --force
```

Running the game without mods (disabling them for the session):
```sh
Conay.exe --nomods
```

Restore previously saved mods (after disabling them):
```sh
Conay.exe --restore
```

Choosing a server to load and order modlist automatically:

```sh
Conay.exe --server halcyon
```

Add `--launch` parameter to run the game automatically after updates are done.

You can find the supported servers in [servers](https://github.com/RatajVaver/conay/servers) directory.

## Bug reports

Have you found a bug? Please report it in the issues tab.

Do you have questions? Join my [Discord](https://discord.gg/3WJNxCTn8m) and feel free to ask.

## Contributions

Do you want to expand this project? Feel free to fork it and open PR. You can also add your server into the [servers](https://github.com/RatajVaver/conay/servers) directory. Keep your modlist updated to make it easier for your players.