# SeedExplorer

# Description

[Workshop Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=3464788018)

Use the CLI (F1 in-game) to generate the full description of nodes for a given shard and seed.
Can batch seeds.
Files are stored in the game's AppData (LocalLow/Landfall/Haste/SeedFinder).
This project is more developers focused. You can plug yourself on this mod to do what you want.

The functions available in CLI:
SeedExplorerProgram.CurrentMapToFile (saves the current shard map in files)
SeedExplorerProgram.GenerateMapToFile (saves the given shard+seed map in files)
SeedExplorerProgram.BatchGenerateMapToFile (saves all shard+seeds maps between min and max in files)
SeedExplorerProgram.CurrentMapToLogs (logs the current shard map in the console)
SeedExplorerProgram.GenerateMapToLogs (logs the given shard+seed map in the console)
SeedExplorerProgram.BatchGenerateMapToLogs (logs all shard+seeds maps between min and max in the console)
SeedExplorerProgram.CompareCurrentVSGenerated (debug function, to check if generation works properly)
SeedExplorerProgram.CompareSeeds (displays all differences between shard+seed1 and shard+seed2)

Additional unexposed functions available in the dll to plug actions and handle generation.

More available soon.
