# Save Room Teleport

A BepInEx mod for **Castlevania: Belmont's Curse** that lets you travel to any save room you have already used, straight from the area map, including save rooms in other areas.

- **From the map:** select a used save room on the area map and press the map's interact button. The prompt uses the game's own "Travel to the destination" text, translated into whatever language you play in.
- **Confirmation:** the game's own boatman popup asks before you travel, so nothing happens by accident.
- **Arriving counts as saving there:** you land on the room's mirror and the game runs its normal save point: HP and potions refill, monsters respawn and the game saves. That is the trade-off for travelling fast. It can be turned off (see Configuration).
- **Any area:** same-area trips reload the area; trips to another area load it directly.
- **Safe landing:** if a load ever puts you in the wrong room, the mod sends you back to where you stood.
- **Controller first:** works with gamepad and with mouse and keyboard.

## Requirements

- Castlevania: Belmont's Curse 1.1.3 (Steam, Windows)
- [BepInEx 6 IL2CPP](https://builds.bepinex.dev/projects/bepinex_be), tested with 6.0.0-be.788

## Install

1. **BepInEx:** download `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788` from the link above and extract it into the game folder (the one with `Castlevania Belmont's Curse.exe`; in Steam: right-click the game > Manage > Browse local files).
2. Start the game once and wait until it reaches the title screen. The first start takes longer because BepInEx generates its files. Then close the game.
3. **The mod:** download `SaveRoomTeleport-<version>.zip` from [Releases](../../releases) and extract it into the same game folder, so the DLL ends up at `BepInEx/plugins/SaveRoomTeleport.dll`.
4. Start the game. A save room counts as used once you have saved at its mirror at least once.

## Configuration

After the first start with the mod, the settings are in `BepInEx/config/marcelo.belmontscurse.saveroomteleport.cfg`:

| Setting | Default | What it does |
|---|---|---|
| `Enabled` | `true` | Turns travelling on or off. |
| `ArrivalUsesSavePoint` | `true` | Arriving uses the destination's save point (refill, respawn, save). Off = travel without resting. |
| `PreserveHealthAndPotions` | `true` | Only when the setting above is off: keeps HP and potions (including overdrive charges) as they were before travelling. |
| `LoadingType` | `LoadSave` | How the level is reloaded. `LoadSave` and `Default` were tested; avoid `Death` and `FastTravel`. |

## Uninstall

Delete `BepInEx/plugins/SaveRoomTeleport.dll` and, if you want, the `.cfg` file above. The mod does not change your save file format, so saves keep working without it.

## How it works

There are no Harmony patches: the map's interact methods are inlined by IL2CPP and patching them crashed the game. Instead, a component watches the map, shows the prompt on used save rooms, closes the map and asks the game to load the destination level, then snaps the player onto the save point and starts the mirror's own interaction. Closing the map and loading another area follow the same approach as the Mirror Fast Travel mod.

## Build

The build expects the game at the path in `SaveRoomTeleport.csproj` (`GameDir`); pass `-p:GameDir=...` to use a different one.

```sh
dotnet build SaveRoomTeleport.csproj -c Release                 # build
dotnet build SaveRoomTeleport.csproj -c Release -p:Deploy=true  # build and copy into BepInEx/plugins
./package.sh                                                    # build the release zip into dist/
```

- `lab/` is the TeleportLab test harness (save snapshots, diffs, spawn probes) used to work out the mechanism. It is not part of the mod build.
- `tools/` has the scripts used to inspect the game's interop assemblies; see `tools/README.md`.

## License

[MIT](LICENSE)
