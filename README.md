# StarfieldSaveTool

A tool to decompress and convert Starfield save games to JSON format. Save games are stored in a compressed format (`.sfs`) and this tool will decompress the data and output the metadata to JSON. Save games are normally found in the `C:\Users\<USERNAME>\Documents\My Games\Starfield\Saves` directory.

This was primarily developed as part of the [Starfield](https://www.nexusmods.com/starfield) extension for [Vortex](https://www.nexusmods.com/site/mods/1), the mod manager created by [Nexus Mods](https://www.nexusmods.com/). 

Any help with the file format would be appreciated, the majority of unknowns are in the header of the compressed `sfs` file, and the plugins data within the decompressed file. I'm not including the main data blocks of the decompressed file as we are primarily interested in the metadata.

Thanks to Mod Organizer 2 for it's help to get started writing this tool and to help formalize this file format research. Special thanks to Silarn for the reverse engineering help.

## Contribute

This is all work in progress and there is plenty more with this file format that needs reverse engineering and explaining. All help is welcome and here are some tools that might help:

* Template files for [.sfs (compressed)](https://github.com/Nexus-Mods/StarfieldSaveTool/blob/master/SFS.bt) and [.sfs (decompressed)](https://github.com/Nexus-Mods/StarfieldSaveTool/blob/master/SFS-decompressed.bt) that can be used in [010 Editor](https://www.sweetscape.com/010editor/).

## Usage

The tool will always output JSON to the console. See below for options to write JSON to a file and to write the decompressed data to a file (useful for reverse engineering).

### Drag-and-drop

The simplest way to use the tool is to drag-and-drop a save file (`.sfs`) onto the `Launch.bat` file. This uses the `--output-json-file` and `--output-raw-file` options below.

### Command Line

`StarfieldSaveTool <file> [options]`

As the tool always outputs JSON to the console, you can use this tool via another program to parse the JSON output.

#### Arguments

`<file>`: Starfield save file to parse (`.sfs`)

#### Options

```shell
  -j, --output-json-file  Write JSON output to file [default: False]
  -r, --output-raw-file   Write raw output to file [default: False]
  -c, --change-file       Experimental: Writes the modified save file [default: False]
  --version               Show version information
  -?, -h, --help          Show help and usage information
```

### Usage Examples

* `StarfieldSaveTool C:\Test\MySave.sfs`
  will output metadata only to the console

* `StarfieldSaveTool C:\Test\MySave.sfs --output-json-file` will output metadata to the console and
  to `C:\Test\MySave.json`

* `StarfieldSaveTool C:\Test\MySave.sfs --output-json-file --output-raw-file` will output metadata to the console and
  to `C:\Test\MySave.json` and save the decompressed data chunks from the `.sfs` file to `C:\Test\MySave.raw`

## Example JSON

See [Example.json](Example.json) for a complete JSON export.

## Starfield Save file (.SFS)

The `.sfs` file is a compressed file format used by Starfield. The file format is a series of compressed data chunks in the Zlib format. The data chunks are compressed using the `Deflate` algorithm using Fast Compression. Here is a work-in-progress breakdown of the binary file format.

| Name                   | Type      | Description                                                                                                     |
|------------------------|-----------|-----------------------------------------------------------------------------------------------------------------|
| magic                  | `char[4]` | Magic bytes `"BCPS"`                                                                                            |
| version0               | `uint`    | Version number                                                                                                  |
| chunkSizesOffset       | `uint64`  | Position where the compressedChunkSizes array begins                                                            |  
| unknown0               | `uint64`  |                                                                                                                 |
| compressedDataOffset   | `uint64`  | Position where the compressed chunks begins                                                                     |
| uncompressedDataSize   | `uint64`  | Size of uncompressed save file                                                                                  |
| version1               | `float`   | Different version number?                                                                                       |
| unknown1               | `uint`    |                                                                                                                 |
| sizeUncompressedChunks | `uint64`  | Size of each uncompressed chunk                                                                                 |
| paddingSize            | `uint64`  | Determines start of each compressed chunk. Chunks are padded out to the nearest 16 bytes                        |
| unknown2               | `uint`    |                                                                                                                 |
| compressionType        | `char[4]` | `"ZIP "`. Might have other compression types? Denotes start of compressed chunks                                |
| compressedChunkSizes   | `uint[]`  | Array of compressed sizes for each chunk                                                                        |
| compressedChunks       | `chunk[]` | The size of each chunk is determined by the `compressedChunkSizes` array and is padded to the nearest 16 bytes. |


Below is the file that is decompressed from the compressed chunks in the `.sfs` save file as described above.

### Dat file (Decompressed Starfield Save file)

| Name                   | Type                        | Description                               |
|------------------------|-----------------------------|-------------------------------------------|
| magic                  | `char[12]`                  | Magic bytes `"SFS_SAVEGAME"`              |
| headerSize             | `uint`                      | Size of header                            |
| header                 | [HEADER](#HEADER)           | Header data block                         |
| saveVersion            | `ubyte`                     | Save file format version. Currently `122` |
| currentGameVersionSize | `ushort`                    | Size of the current game version string   |
| currentGameVersion     | `string`                    | Current game version string               |
| createdGameVersionSize | `ushort`                    | Size of the created game version string   |
| createdGameVersion     | `string`                    | Created game version string               |
| pluginInfoSize         | `ushort`                    | Size of the plugin information data       |
| pluginInfo             | [PLUGIN_INFO](#PLUGIN_INFO) | Plugin information data block             |

Rest of file has not been worked out yet.

### HEADER

| Name               | Type       | Description                                                                       |
|--------------------|------------|-----------------------------------------------------------------------------------| 
| engineVersion     | `uint`     | Engine version? This is `27`, Fallout 4 was `11`.                                 |
| saveVersion        | `byte`     | Save file format version. Currently `143`                                         |
| saveNumber         | `uint`     | Incremented by 1 each time overwritten                                            |
| playerNameSize     | `ushort`   | Size of player name string                                                        |
| playerName         | `string`   | Player Name. Not used in game, only used for load screen?                         |
| playerLevel        | `uint`     | Player Level                                                                      |
| playerLocationSize | `ushort`   | Size of Player Location string                                                    |
| playerLocation     | `string`   | Player Location                                                                   |
| playtimeSize       | `ushort`   | Size of Playtime string                                                           |
| playtime           | `string`   | Playtime. `xd.yh.zm.x days.y hours.z minutes` where x=days, y=hours and z=minutes |
| raceNameSize       | `ushort`   | Size of Race Name string                                                          |
| raceName           | `string`   | Race Name                                                                         |
| gender             | `ushort`   | Gender. 0=Male, 1=, 2=                                                            |
| experience         | `float`    | Player's experience                                                               |
| experienceRequired | `float`    | Experience required for next level                                                |
| time               | `FILETIME` | Last played timestamp.                                                            |
| padding            | `byte[8]`  | Unknown                                                                           |
| unknown            | `uint`     | Unknown                                                                           |

What follows the INFO block is the PLUGIN_INFO block

### PLUGIN_INFO

| Name             | Type       | Description          |
|------------------|------------|----------------------|
| unknown0         | `byte`     |                      |
| unknown1         | `byte`     |                      |
| pluginCount      | `byte`     |                      |
| plugins          | `PLUGIN[]` | Array of plugin data |
| lightPluginCount | `byte`     |                      |
| lightPlugins     | `PLUGIN[]` | Array of plugin data |

Medium plugins were added in save file version 122. They are stored after the light plugins if `saveVersion >= 122`

| Name              | Type       | Description          |
|-------------------|------------|----------------------|
| mediumPluginCount | `byte`     |                      |
| mediumPlugins     | `PLUGIN[]` | Array of plugin data |

### PLUGIN

There are different types of Plugin data blocks.

* Native game plugins only contain the pluginName
* Non-native plugins (if `SaveVersion >= 122`) contain extra data, including Creation information and flags where appropriate. If an older SaveVersion, the extra data is not present.
* If `SaveVersion >= 140`, there is an extra byte to indicate if there is extra data for the plugin.

| Name           | Type          | Description         | Comment                           |
|----------------|---------------|---------------------|-----------------------------------|
| pluginNameSize | `ushort`      | Size of plugin name |                                   |
| pluginName     | `string`      | Plugin name         |                                   |
| extraInfo      | `ubyte` | 0=no, 1=yes           | Only exists if `SaveVersion >= 140` |

#### Extra Data

| Name                  | Type              | Description                                          |
|-----------------------|-------------------|------------------------------------------------------|
| creationNameSize      | `ushort`          | Size of Creation Name string. `0` if not a Creation. |
| creationName          | `string`          | Creation Name string                                 |
| creationIdSize        | `ushort`          | Size of Creation ID. `0` if not a Creation.          |
| creationId            | `string`          | Creation ID. GUID that matches xyz                   |
| flagsSize             | `ushort`          | Size of flags data. Sometimes `0`.                   |
| flags                 | `byte[flagsSize]` | Unknown data structure                               |
| achievementCompatible | `byte`            | 0=no, 1=yes                                          |
