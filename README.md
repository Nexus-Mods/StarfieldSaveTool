# StarfieldSaveTool

A tool to decompress and convert Starfield save games to JSON format. Save games are stored in a compressed format
(`.sfs`) and this tool will decompress the data and output the metadata to JSON. Save games are normally found in the
`C:\Users\<USERNAME>\Documents\My Games\Starfield\Saves` directory.

Any help with the file format would be appreciated, the majority of unknowns are in the header of the compressed `sfs`
file, and the plugins data within the decompressed file. I'm not including the main data blocks of the decompressed file
as
we are primarily interested in the metadata.

Thanks to Mod Organizer 2 for it's help to get started writing this tool and to help formalize this file format
research.

## Usage

The tool will always output JSON to the console. See below for options to write JSON to a file and to write the
decompressed data to a file (useful for reverse engineering).

### Drag-and-drop

The simplest way to use the tool is to drag-and-drop a save file (`.sfs`) onto the `Launch.bat` file. This uses
the `--output-json-file` and `--output-raw-file` options below.

### Command Line

`StarfieldSaveTool <file> [options]`

As the tool always outputs JSON to the console, you can use this tool via another program to parse the JSON output.

#### Arguments

`<file>`: Starfield save file to parse (`.sfs`)

#### Options

```shell
  -j, --output-json-file  Write JSON output to file [default: False]
  -r, --output-raw-file   Write raw output to file [default: False]
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

## File Format - Starfield Save file (.SFS)

The `.sfs` file is a compressed file format used by Starfield. The file format is a series of compressed data
chunks in the Zlib format. The data chunks are compressed using the `Deflate` algorithm.

### HEADER

| Name                   | Type      | Description                                                                              |
|------------------------|-----------|------------------------------------------------------------------------------------------|
| magic                  | `char[4]` | Magic bytes `"BCPS"`                                                                     |
| version0               | `uint`    | Version number                                                                           |
| chunkSizesOffset       | `uint64`  | Position where the compressedChunkSizes array begins                                     |  
| unknown0               | `uint64`  |                                                                                          |
| compressedDataOffset   | `uint64`  | Position where the compressed chunks begins                                              |
| uncompressedDataSize   | `uint64`  | Size of uncompressed save file                                                           |
| version1               | `float`   | Different version number?                                                                |
| unknown1               | `uint`    |                                                                                          |
| sizeUncompressedChunks | `uint64`  | Size of each uncompressed chunk                                                          |
| paddingSize            | `uint64`  | Determines start of each compressed chunk. Chunks are padded out to the nearest 16 bytes |
| unknown2               | `uint`    |                                                                                          |
| compressionType        | `char[4]` | `"ZIP "`. Might have other compression types? Denotes start of compressed chunks         |
| compressedChunkSizes   | `uint[]`  | Array of compressed sizes for each chunk                                                 |

What follows is a series of compressed data chunks. The size of each chunk is determined by the `compressedChunkSizes`
array and is padded to the nearest 16 bytes. These are decompressed to expose the save file, described
next.

## Decompressed Starfield Save file

This is decompressed from the `.sfs` save file.

### HEADER

| Name               | Type       | Description                                                                                                                      |
|--------------------|------------|----------------------------------------------------------------------------------------------------------------------------------|
| magic              | `char[12]` | Magic bytes `"SFS_SAVEGAME"`                                                                                                     |
| headerSize         | `uint`     | Total size of header (starting from next byte)                                                                                   |
| version            | `uint`     | Version                                                                                                                          |
| saveVersion        | `byte`     | Save file format version                                                                                                         |
| saveNumber         | `uint`     | Index of save ingame                                                                                                             |
| playerNameSize     | `ushort`   | Size of player name string                                                                                                       |
| playerName         | `string`   | Player name                                                                                                                      |
| playerLevel        | `uint`     | Player level                                                                                                                     |
| playerLocationSize | `ushort`   | Size of player location string                                                                                                   |
| playerLocation     | `string`   | Player location                                                                                                                  |
| playtimeSize       | `ushort`   | Size of playtime string                                                                                                          |
| playtime           | `string`   | Total playtime                                                                                                                   |
| raceNameSize       | `ushort`   | Size of race name string                                                                                                         |
| raceName           | `string`   | Race name                                                                                                                        |
| gender             | `ushort`   | Gender. 0=Male, 1=, 2=                                                                                                           |
| experience         | `float`    | Player's current experience                                                                                                      |
| experienceRequired | `float`    | Experience points required for next level                                                                                        |
| time               | `uint64`   | Last played timestamp. [FILETIME](https://learn.microsoft.com/en-us/windows/win32/api/minwinbase/ns-minwinbase-filetime) format. |
| padding            | `byte[8]`  | Unknown                                                                                                                          |
| unknown            | `uint`     | Unknown                                                                                                                          |

What follows the HEADER is an INFO block with game version information and plugin size.

### INFO

| Name                   | Type     | Description                             |
|------------------------|----------|-----------------------------------------|
| saveVersion            | `byte`   | Version of the save                     |
| currentGameVersionSize | `ushort` | Size of the current game version string |
| currentGameVersion     | `string` | Current game version string             |
| createdGameVersionSize | `ushort` | Size of the created game version string |
| createdGameVersion     | `string` | Created game version string             |
| pluginInfoSize         | `ushort` | Size of the plugin information data     |

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

There are 3 different types of Plugin data blocks.

* Base Plugins consists of just the plugin name.
* Extended Plugins are Base Plugins with extra info.
* Creation Plugins are Base Plugins that are associated with a Creation and have extra Creation Club metadata.

| Name           | Type     | Description         |
|----------------|----------|---------------------|
| pluginNameSize | `ushort` | Size of plugin name |
| pluginName     | `string` | Plugin name         |

### Extended Plugin

Includes the Plugin data above and the following:

| Name    | Type       | Description            |
|---------|------------|------------------------|
| unknown | `byte[13]` | Unknown data structure |

### Creation Plugin

Includes the Plugin data above and the following:

| Name             | Type              | Description                        |
|------------------|-------------------|------------------------------------|
| creationNameSize | `ushort`          | Size of Creation Name string       |
| creationName     | `string`          | Creation Name string               |
| creationIdSize   | `ushort`          | Size of Creation ID                |
| creationId       | `string`          | Creation ID. GUID that matches xyz |
| flagsSize        | `ushort`          | Size of flags data                 |
| flags            | `byte[flagsSize]` | Unknown data structure             |
| hasFlags         | `byte`            | 0=no flags, 1=has flags            |

Rest of file is unknown data.


