# Troop Training Expanded

A Mount & Blade II: Bannerlord mod that adds configurable training fights to town arenas. Players can fight selected troops which will gain promotions if the player defeats them. Bring companions as allies, or duel them while earning combat XP.

## Project Structure

- `Main.cs` - Module entry point and Harmony initialization.
- `Behaviors/` - Campaign menu, troop selection, mission spawning, and fight logic.
- `Patches/` - Harmony patches for arena behavior and combat XP.
- `Helpers/` - Configuration and troop promotion utilities.
- `ModuleData/Languages/` - Localization files.
- `SubModule.xml` - Module metadata, dependencies, and supported game versions.
- `settings.json` - User settings such as troop limit and horse usage.

## Building

Open `TroopTrainingExpanded.slnx` in Rider or your preferred IDE and build the `x64` project. Debug builds are copied directly to the mod's `Win64_Shipping_Client` directory; Release builds go to `bin/Release`.

The project targets .NET Framework 4.7.2 and references Bannerlord assemblies from the local game installation.

## For new Bannerlord versions

1. Update Bannerlord and confirm the DLL paths in `TroopTrainingExpanded.csproj`.
2. Reload the project in Rider and rebuild to find changed or removed APIs.
3. Update the game and dependency versions in `SubModule.xml`.
4. Review Harmony targets in `Patches/`, as game updates may rename methods or change signatures.
5. Test troop selection, companion roles and spawning, XP gain, mission exit, and save compatibility in game.

## Upgrading the mod's version & release

1. Update the version in `SubModule.xml`.
2. Add any changes to CHANGELOG.md.
3. Build the mod and test it in game.
4. For the Nexus release, go to the mod's folder inside /Modules in the Bannerlord directory and inside /bin/Win64_Shipping_Client remove all the dlls except NewtonsoftJson.dll and TroopTrainingExpanded.dll.
5. Copy SubModule.xml into /TroopTrainingExpanded overwriting the existing one.
6. Zip the entire folder and upload it to Nexus Mods.
