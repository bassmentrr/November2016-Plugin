# November Plugin

November Plugin is a **BepInEx plugin for the [Bassment November Server](https://github.com/bassmentrr/November2016-Server)**.

It is primarily designed for a **November 16th, 2016 build of Rec Room**, and is intended to work alongside the [Bassment November Server](https://github.com/bassmentrr/November2016-Server).

From what I investigated, this plugin should work from a version from November 9th, 2016 onwards, as they added the account system in that version!

## Important!

If you want the version that I was using to test, put this into the Steam Console:

download_depot 471710 471711 424546141649632564

Make sure to put a "steam_appid.txt" inside the root of the install. I set the ID inside the .txt to 471710 (Rec Room's ID) but if you want somebody who doesn't have Rec Room on Steam to play, make sure their "steam_appid.txt" is set to either 92 (Codename Gordon (steam://install/92)) or 480 (Spacewar (steam://install/480))

## Building

Make sure you have **.NET 9.0** installed.

### 1. Clone the repository

```bash
git clone https://github.com/bassmentrr/November2016-Plugin.git
cd November2016-Plugin
```

or if you're lazy, just download the ZIP from GitHub!

### 2. Build the plugin

```bash
dotnet build
```

The compiled plugin will be located at:

```bash
bin/Debug/net35/BassmentPlugin.dll
```

### 3. Install the plugin

Copy `BassmentPlugin.dll` into your BepInEx plugins folder:

```bash
BepInEx/plugins/
```

Make sure **BepInEx is installed and working before installing the plugin**.

The version of BepInEx that I use during testing is 5.4.21.0!

In the BepInEx config, make sure under "[Preloader.Entrypoint]" that "Type" is set to MonoBehaviour!

## Releases

Pre-built versions of the plugin are available from the **Releases** page!

The releases contain the plugin DLL directly, so you can download `BassmentPlugin.dll` and drop it straight into your `BepInEx/plugins/` folder!
