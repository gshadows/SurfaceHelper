# Surface Helper plugin for Elite Observatory *Core*
This plugin is for "Elite Observatory Core" tool for "Elite Dangerous" game.

### Current features

* Display current distance to the ship in floating notification.
* Notify player when distance to ship exceeds specified range to prevent ship take off when 2km away.
* Notify when ship took off without player.

### Future plans

- Allow adding markers on surface and show it in the plugin UI tab.
- Add ship distance to the plugin UI tab.

## Sources notification.
Original ["Botanist"](https://github.com/Xjph/ObservatoryCore/tree/master/ObservatoryBotanist) plugin sources was used as a base.

## How To Install
Download plugin and extract DLL into plugins folder of Observatory Core.

Alternatively, start plugin in explorer so that Observatory install it by itself.

## How To Use
Disembark your ship or deploy SRV onto planet surface.
You will see small overlay at the left side of the screen, displaying distance to your ship.

When you leave 1-st or 2-nd range circle, you will be notified by message and voice (if enabled).
Default ranges are 1750 and 1900 meters from ship, which should be helpful to not lose your ship unexpectedly.

Additionaly you will be notified in case if ship flew away (either if you left 2km range or manaully dismissed).

On the "Core" page of Observatory you can open plugin configuration to set custom distance ranges.

## How to build

*Requirements*
- C# 9.0 / .NET 8.0
- [ObservatoryFramework](https://observatory.xjph.net/framework) (installing Observatory Core is enough).

*Building plugin DLL from sources*

1. Fix ObservatoryPath variable in all .cmd scripts.
2. Run "build_cs.cmd" to build plugin DLL.
3. Ensure Observatory is not running. If it running - exit it.
4. Run "install-release.cmd" to install plugin.
5. Run "start.cmd" to start Observatory, or run it from your usual shortcut.

*Prepare release*

4. Ensure you have [7-Zip](https://www.7-zip.org/download.html) installed.
5. Run "package.cmd. script.
6. Find "SurfaceHelper.(version).eop" plugin package inside "releases".
