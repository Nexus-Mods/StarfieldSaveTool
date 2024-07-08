using System.Text;
using Newtonsoft.Json;
using NLog;

namespace StarfieldSaveTool;

public struct Header
{
    [JsonIgnore] public char[] magic;
    [JsonIgnore] public uint headerSize;
    public uint version;
    public byte saveVersion;
    public uint saveNumber;
    [JsonIgnore] public ushort playerNameSize;
    public string playerName;
    public uint playerLevel;
    [JsonIgnore] public ushort playerLocationSize;
    public string playerLocation;
    [JsonIgnore] public ushort playtimeSize;
    public string playtime;
    [JsonIgnore] public ushort raceNameSize;
    public string raceName;
    public ushort gender;
    public float experience;
    public float experienceRequired;
    [JsonIgnore] public ulong time;
    public DateTime dateTime;  
    public uint unknown0;  
    [JsonIgnore] public byte[] padding;
}

public struct Info
{
    public byte saveVersion;
    [JsonIgnore] public ushort currentGameVersionSize;
    public string currentGameVersion;
    [JsonIgnore] public ushort createdGameVersionSize;
    public string createdGameVersion;
    [JsonIgnore] public ushort pluginInfoSize;
} 

public struct PluginInfo {
    
    [JsonIgnore] public byte[] Padding;
    
    public byte PluginCount;
    public ushort LightPluginCount;
    public uint MediumPluginCount;
    
    public List<PluginBase> Plugins;
    public List<PluginBase> LightPlugins;
    public List<PluginBase> MediumPlugins;
}

public abstract class PluginBase
{
    //public ushort PluginNameSize { get; set; }
    public string PluginName { get; set; }
}

public class Plugin : PluginBase
{
    
}

public class ExtendedPlugin : PluginBase
{
    [JsonIgnore] public byte[] Unknown { get; set; }
}

public class CreationPlugin : PluginBase
{
    [JsonIgnore] public ushort CreationNameSize { get; set; }
    public string CreationName { get; set; }
    [JsonIgnore] public ushort CreationIdSize { get; set; }
    public string CreationId { get; set; }
    [JsonIgnore] public ushort FlagsSize { get; set; }
    [JsonIgnore] public byte[] Flags { get; set; }
    [JsonIgnore] public byte HasFlags { get; set; }
}

public class DecompressedSaveFile(Stream stream)
{
    [JsonProperty] private Header _header;
    [JsonProperty] private Info _info;
    [JsonProperty] private PluginInfo _pluginInfo;
    
    private Stream _stream = stream;
    private Logger _logger = LogManager.GetCurrentClassLogger();
    
    const string SAVE_MAGIC = "SFS_SAVEGAME";
    readonly string[] NATIVE_PLUGINS = { "Starfield.esm", "Constellation.esm", "OldMars.esm", "BlueprintShips-Starfield.esm", "SFBGS007.esm", "SFBGS008.esm", "SFBGS006.esm", "SFBGS003.esm" };
    
    public void ReadFile()
    {
        using var br = new BinaryReader(_stream);
        br.BaseStream.Seek(0, SeekOrigin.Begin);

        // quick check for magic bytes
        var magic = br.ReadBytes(12);
            
        if (Encoding.ASCII.GetString(magic) != SAVE_MAGIC)
        {
            //_logger.Error("Invalid file format");
            throw new Exception($"Not a valid decompressed Starfield save. Magic bytes not found.");
        }

        _header = ReadHeader(br);
        _info = ReadInfo(br);
        _pluginInfo = ReadPluginInfo(br, _info.saveVersion);
    }

    public string ToJson()
    {
        var json = JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
        return json;
    }
    
    
    private PluginInfo ReadPluginInfo(BinaryReader br, byte infoSaveVersion)
    {
        _pluginInfo = new PluginInfo();

        _pluginInfo.Padding = br.ReadBytes(2);
        _pluginInfo.PluginCount = br.ReadByte();
            
        _pluginInfo.Plugins = new List<PluginBase>();
        _pluginInfo.LightPlugins = new List<PluginBase>();
        _pluginInfo.MediumPlugins = new List<PluginBase>();

        // loop through normal plugins
        for (int i = 0; i < _pluginInfo.PluginCount; i++)
        {
            _pluginInfo.Plugins.Add(ReadPlugin(br));
        }
            
        _pluginInfo.LightPluginCount = br.ReadUInt16();
            
        // loop through light plugins
        for (int i = 0; i < _pluginInfo.LightPluginCount; i++)
        {
            _pluginInfo.LightPlugins.Add(ReadPlugin(br));
        }

        // previous save versions didn't have medium plugins
        if (infoSaveVersion >= 122)
        {
            _pluginInfo.MediumPluginCount = br.ReadUInt32();

            // loop through medium plugins
            for (int i = 0; i < _pluginInfo.MediumPluginCount; i++)
            {
                _pluginInfo.MediumPlugins.Add(ReadPlugin(br));
            }
        }

        return _pluginInfo;
    }

    string ReadString(BinaryReader br)
    {
        // read the string from current position
        // made up of an ushort for the size of the string and then the string itself
        var size = br.ReadUInt16();
        return Encoding.ASCII.GetString(br.ReadBytes(size)); 
    }

    private PluginBase ReadPlugin(BinaryReader br)
    {
        // record the current position
        // var offset = br.BaseStream.Position;

        // read the plugin name
        var pluginName = ReadString(br);

        // reset the position
        //br.BaseStream.Seek(offset, SeekOrigin.Begin);

        if (NATIVE_PLUGINS.Contains(pluginName))
        {
            _logger.Info($"{pluginName} is a native plugin.");

            return new Plugin
            {
                PluginName = pluginName
            };
        }

        // read ahead if next short is 00 then it's a creation plugin
        var nextShort = br.ReadUInt16();

        // reset position
        br.BaseStream.Seek(-2, SeekOrigin.Current);

        if (nextShort != 0)
        {
            // creation plugin
            var creationPlugin = new CreationPlugin();
            creationPlugin.PluginName = pluginName;
            creationPlugin.CreationNameSize = br.ReadUInt16();
            creationPlugin.CreationName = Encoding.ASCII.GetString(br.ReadBytes(creationPlugin.CreationNameSize));
            creationPlugin.CreationIdSize = br.ReadUInt16();
            creationPlugin.CreationId = Encoding.ASCII.GetString(br.ReadBytes(creationPlugin.CreationIdSize));
            creationPlugin.FlagsSize = br.ReadUInt16();
            creationPlugin.Flags = br.ReadBytes(creationPlugin.FlagsSize);
            creationPlugin.HasFlags = br.ReadByte();
             
            _logger.Info($"{pluginName} is a creation plugin ({creationPlugin.CreationName}).");
            return creationPlugin;
        }
        else
        {
            // extended plugin
            ExtendedPlugin extendedPlugin = new ExtendedPlugin();
            extendedPlugin.PluginName = pluginName;
            extendedPlugin.Unknown = br.ReadBytes(13);
            _logger.Info($"{pluginName} is a extended plugin.");
            return extendedPlugin;
        }

    }

    private Info ReadInfo(BinaryReader br)
    {
        var info = new Info();
        
        info.saveVersion = br.ReadByte();
        info.currentGameVersionSize = br.ReadUInt16();
        info.currentGameVersion = Encoding.ASCII.GetString(br.ReadBytes(info.currentGameVersionSize));
        info.createdGameVersionSize = br.ReadUInt16();
        info.createdGameVersion = Encoding.ASCII.GetString(br.ReadBytes(info.createdGameVersionSize));
        info.pluginInfoSize = br.ReadUInt16();
            
        return info;
    }

    static Header ReadHeader(BinaryReader br)
    {
        var header = new Header();
        
        // reset position to start of file
        br.BaseStream.Seek(0, SeekOrigin.Begin);
        
        header.magic = br.ReadChars(12);
        header.headerSize = br.ReadUInt32();
        header.version = br.ReadUInt32();
        header.saveVersion = br.ReadByte();
        header.saveNumber = br.ReadUInt32();
        header.playerNameSize = br.ReadUInt16();
        header.playerName = Encoding.ASCII.GetString(br.ReadBytes(header.playerNameSize));
        header.playerLevel = br.ReadUInt32();
        header.playerLocationSize = br.ReadUInt16();
        header.playerLocation = Encoding.ASCII.GetString(br.ReadBytes(header.playerLocationSize));
        header.playtimeSize = br.ReadUInt16();
        header.playtime = Encoding.ASCII.GetString(br.ReadBytes(header.playtimeSize));
        header.raceNameSize = br.ReadUInt16();
        header.raceName = Encoding.ASCII.GetString(br.ReadBytes(header.raceNameSize));
        header.gender = br.ReadUInt16();
        header.experience = br.ReadSingle();
        header.experienceRequired = br.ReadSingle();
        header.time = br.ReadUInt64();
        header.dateTime = DateTime.FromFileTimeUtc((long)header.time);
        header.unknown0 = br.ReadUInt32();
        header.padding = br.ReadBytes(8);
            
        return header;
    }
}