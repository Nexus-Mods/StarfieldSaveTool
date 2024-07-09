﻿using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NLog;

namespace StarfieldSaveTool;



public struct Header
{
    public uint Version { get; set; }
    public byte SaveVersion { get; set; }
    public uint SaveNumber { get; set; }
    [JsonIgnore] public ushort PlayerNameSize { get; set; }
    public string PlayerName { get; set; }
    public uint PlayerLevel { get; set; }
    [JsonIgnore] public ushort PlayerLocationSize { get; set; }
    public string PlayerLocation { get; set; }
    [JsonIgnore] public ushort PlaytimeSize { get; set; }
    public string Playtime { get; set; }
    [JsonIgnore] public ushort RaceNameSize { get; set; }
    public string RaceName { get; set; }
    public ushort Gender { get; set; }
    public float Experience { get; set; }
    public float ExperienceRequired { get; set; }
    [JsonIgnore] public ulong Time { get; set; }
    public DateTime DateTime { get; set; }
    [JsonIgnore] public uint Unknown0 { get; set; }
    [JsonIgnore] public byte[] Padding { get; set; }
}

public struct PluginInfo
{
    [JsonIgnore] public byte[] Padding { get; set; }

    public byte PluginCount { get; set; }
    public ushort LightPluginCount { get; set; }
    public uint MediumPluginCount { get; set; }

    public List<Plugin> Plugins { get; set; }
    public List<Plugin> LightPlugins { get; set; }
    public List<Plugin> MediumPlugins { get; set; }
}

public struct Plugin
{
    //public ushort PluginNameSize { get; set; }
    public string PluginName { get; set; }
    [JsonIgnore] public ushort CreationNameSize { get; set; }
    public string CreationName { get; set; }
    [JsonIgnore] public ushort CreationIdSize { get; set; }
    public string CreationId { get; set; }
    [JsonIgnore] public ushort FlagsSize { get; set; }
    [JsonIgnore] public byte[] Flags { get; set; }
    [JsonIgnore] public byte AchievementCompatible { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(DecompressedSaveFile))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}

public class DecompressedSaveFile(Stream stream)
{
    [JsonIgnore] public char[] Magic { get; private set; }
    [JsonIgnore] public uint HeaderSize { get; private set; }
    public Header Header { get; private set; }
    public byte SaveVersion { get; private set; }
    [JsonIgnore] public ushort CurrentGameVersionSize { get; private set; }
    public string CurrentGameVersion { get; private set; } = "";
    [JsonIgnore] public ushort CreatedGameVersionSize { get; private set; }
    public string CreatedGameVersion { get; private set; } = "";
    [JsonIgnore] public ushort PluginInfoSize { get; private set; }
    public PluginInfo PluginInfo { get; private set; }

    private Stream _stream = stream;
    private Logger _logger = LogManager.GetCurrentClassLogger();

    const string SAVE_MAGIC = "SFS_SAVEGAME";

    readonly string[] NATIVE_PLUGINS =
    {
        "Starfield.esm", "Constellation.esm", "OldMars.esm", "BlueprintShips-Starfield.esm", "SFBGS007.esm",
        "SFBGS008.esm", "SFBGS006.esm", "SFBGS003.esm"
    };

    public void ReadFile()
    {
        using var br = new BinaryReader(_stream);
        br.BaseStream.Seek(0, SeekOrigin.Begin);

        // quick check for magic bytes
        Magic = br.ReadChars(12);

        if (new string(Magic) != SAVE_MAGIC)
        {
            _logger.Error("Invalid file format");
            throw new Exception($"Not a valid decompressed Starfield save. Magic bytes not found.");
        }

        HeaderSize = br.ReadUInt32();

        Header = ReadHeader(br);

        SaveVersion = br.ReadByte();
        CurrentGameVersionSize = br.ReadUInt16();
        CurrentGameVersion = Encoding.ASCII.GetString(br.ReadBytes(CurrentGameVersionSize));
        CreatedGameVersionSize = br.ReadUInt16();
        CreatedGameVersion = Encoding.ASCII.GetString(br.ReadBytes(CreatedGameVersionSize));
        PluginInfoSize = br.ReadUInt16();

        PluginInfo = ReadPluginInfo(br, SaveVersion);
    }

    public string ToJson()
    {
        var options = new JsonSerializerOptions
            { 
                WriteIndented = true, 
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                TypeInfoResolver = SourceGenerationContext.Default
            };
        return JsonSerializer.Serialize(this, options);
    }


    private PluginInfo ReadPluginInfo(BinaryReader br, byte infoSaveVersion)
    {
        var pluginInfo = new PluginInfo();

        pluginInfo.Padding = br.ReadBytes(2);
        pluginInfo.PluginCount = br.ReadByte();

        pluginInfo.Plugins = new List<Plugin>();
        pluginInfo.LightPlugins = new List<Plugin>();
        pluginInfo.MediumPlugins = new List<Plugin>();

        // loop through normal plugins
        for (int i = 0; i < pluginInfo.PluginCount; i++)
        {
            pluginInfo.Plugins.Add(ReadPlugin(br));
        }

        pluginInfo.LightPluginCount = br.ReadUInt16();

        // loop through light plugins
        for (int i = 0; i < pluginInfo.LightPluginCount; i++)
        {
            pluginInfo.LightPlugins.Add(ReadPlugin(br));
        }

        // previous save versions didn't have medium plugins
        if (infoSaveVersion >= 122)
        {
            pluginInfo.MediumPluginCount = br.ReadUInt32();

            // loop through medium plugins
            for (int i = 0; i < pluginInfo.MediumPluginCount; i++)
            {
                pluginInfo.MediumPlugins.Add(ReadPlugin(br));
            }
        }

        return pluginInfo;
    }

    string ReadString(BinaryReader br)
    {
        // read the string from current position
        // made up of an ushort for the size of the string and then the string itself
        var size = br.ReadUInt16();
        return Encoding.ASCII.GetString(br.ReadBytes(size));
    }

    private Plugin ReadPlugin(BinaryReader br)
    {
        // record the current position
        // var offset = br.BaseStream.Position;

        var plugin = new Plugin();

        // read the plugin name
        plugin.PluginName = ReadString(br);

        // reset the position
        //br.BaseStream.Seek(offset, SeekOrigin.Begin);

        if (NATIVE_PLUGINS.Contains(plugin.PluginName))
        {
            _logger.Info($"{plugin.PluginName} is a native plugin.");
            return plugin;
        }

        /*
        // read ahead if next short is 00 then it's a creation plugin
        var nextShort = br.ReadUInt16();

        // reset position
        br.BaseStream.Seek(-2, SeekOrigin.Current);
        */

        // non-native plugin so we are expecting some extra info and possibly creation info

        // creation name not always here
        plugin.CreationNameSize = br.ReadUInt16();
        if (plugin.CreationNameSize != 0)
            plugin.CreationName = Encoding.ASCII.GetString(br.ReadBytes(plugin.CreationNameSize));

        // creation id not always here
        plugin.CreationIdSize = br.ReadUInt16();
        if (plugin.CreationIdSize != 0)
            plugin.CreationId = Encoding.ASCII.GetString(br.ReadBytes(plugin.CreationIdSize));

        plugin.FlagsSize = br.ReadUInt16();
        plugin.Flags = br.ReadBytes(plugin.FlagsSize);
        plugin.AchievementCompatible = br.ReadByte();

        _logger.Info($"{plugin.PluginName} is a normal plugin ({plugin.CreationName}).");
        return plugin;
    }

    static Header ReadHeader(BinaryReader br)
    {
        var header = new Header();

        header.Version = br.ReadUInt32();
        header.SaveVersion = br.ReadByte();
        header.SaveNumber = br.ReadUInt32();
        header.PlayerNameSize = br.ReadUInt16();
        header.PlayerName = Encoding.ASCII.GetString(br.ReadBytes(header.PlayerNameSize));
        header.PlayerLevel = br.ReadUInt32();
        header.PlayerLocationSize = br.ReadUInt16();
        header.PlayerLocation = Encoding.ASCII.GetString(br.ReadBytes(header.PlayerLocationSize));
        header.PlaytimeSize = br.ReadUInt16();
        header.Playtime = Encoding.ASCII.GetString(br.ReadBytes(header.PlaytimeSize));
        header.RaceNameSize = br.ReadUInt16();
        header.RaceName = Encoding.ASCII.GetString(br.ReadBytes(header.RaceNameSize));
        header.Gender = br.ReadUInt16();
        header.Experience = br.ReadSingle();
        header.ExperienceRequired = br.ReadSingle();
        header.Time = br.ReadUInt64();
        header.DateTime = DateTime.FromFileTimeUtc((long)header.Time);
        header.Unknown0 = br.ReadUInt32();
        header.Padding = br.ReadBytes(8);

        return header;
    }
}