using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NLog;

namespace StarfieldSaveTool;





[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(DatFile))]
[JsonSerializable(typeof(SfsFile))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}

public class DatFile(byte[] data)
{
    public struct FileHeader
    {
        public uint EngineVersion { get; set; }
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

    public struct FilePluginInfo
    {
        [JsonIgnore] public byte[] Padding { get; set; }

        public byte PluginCount { get; set; }
        public ushort LightPluginCount { get; set; }
        public uint MediumPluginCount { get; set; }

        public List<FilePlugin> Plugins { get; set; }
        public List<FilePlugin> LightPlugins { get; set; }
        public List<FilePlugin> MediumPlugins { get; set; }
    }

    public struct FilePlugin
    {
        //public ushort PluginNameSize { get; set; }
        public string PluginName { get; set; }
        [JsonIgnore] public ushort CreationNameSize { get; set; }
        public string CreationName { get; set; }
        [JsonIgnore] public ushort CreationIdSize { get; set; }
        public string CreationId { get; set; }
        [JsonIgnore] public ushort FlagsSize { get; set; }
        [JsonIgnore] public byte[] Flags { get; set; }
        [JsonIgnore] public byte AchievementFriendly { get; set; }
    }
    
    [JsonIgnore] char[] Magic { get; set; }
    [JsonIgnore] public uint HeaderSize { get; private set; }

    public byte JsonVersion { get; private set; } = 1; // json format version
    public FileHeader Header { get; private set; }
    public byte SaveVersion { get; private set; }
    [JsonIgnore] ushort CurrentGameVersionSize { get; set; }
    public string CurrentGameVersion { get; private set; } = "";
    [JsonIgnore] ushort CreatedGameVersionSize { get; set; }
    public string CreatedGameVersion { get; private set; } = "";
    [JsonIgnore] public ushort PluginInfoSize { get; private set; }
    public FilePluginInfo PluginInfo { get; private set; }

    [JsonIgnore] public byte[] Data { get; private set; } = data;

    private Logger _logger = LogManager.GetCurrentClassLogger();

    private const string DatMagic = "SFS_SAVEGAME";

    private readonly string[] NATIVE_PLUGINS =
    {
        "Starfield.esm", "Constellation.esm", "OldMars.esm", "BlueprintShips-Starfield.esm", "SFBGS007.esm",
        "SFBGS008.esm", "SFBGS006.esm", "SFBGS003.esm"
    };

    public void ProcessFile()
    {
        using var ms = new MemoryStream(Data);
        using var br = new BinaryReader(ms);
        
        br.BaseStream.Seek(0, SeekOrigin.Begin);

        // quick check for magic bytes
        Magic = br.ReadChars(12);

        if (new string(Magic) != DatMagic)
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

    public void ChangeFile()
    {
        using var ms = new MemoryStream(Data);
        
        // start at beginning
        ms.Seek(0, SeekOrigin.Begin);
        
        // convert utf string to byte array
        var nameByteArray = "NEXUSM"u8.ToArray();
        var locationByteArray = "Nexus Mods - Home"u8.ToArray(); 
        var spacesuitByteArray = "Nexus Mods -- Uniform"u8.ToArray();
        
        // change things

        // header: name
        ms.Seek(27, SeekOrigin.Begin);
        ms.Write(nameByteArray, 0, nameByteArray.Length); // write byte array to MemoryStream
        
        // strings array: name
        ms.Seek(2340628, SeekOrigin.Begin);
        ms.Write(nameByteArray, 0, nameByteArray.Length); // write byte array to MemoryStream
        
        // header: location
        ms.Seek(39, SeekOrigin.Begin);
        ms.Write(locationByteArray, 0, locationByteArray.Length); // write byte array to MemoryStream
        
        // strings array: Deep Mining Spacesuit
        //ms.Seek(2338625, SeekOrigin.Begin);
        //ms.Write(spacesuitByteArray, 0, spacesuitByteArray.Length); // write byte array to MemoryStream
        
        
        // write stream back to bytes
        Data = ms.ToArray();

        /*
        Data[0] = Convert.ToByte('S');
        Data[1] = Convert.ToByte('I');
        Data[2] = Convert.ToByte('M');
        Data[3] = Convert.ToByte('O');
        Data[4] = Convert.ToByte('N');
        */
    }

    public string ToJson()
    {
        var options = new JsonSerializerOptions
            { 
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                TypeInfoResolver = SourceGenerationContext.Default,
                
            };
        return JsonSerializer.Serialize(this, options);
    }


    private FilePluginInfo ReadPluginInfo(BinaryReader br, byte infoSaveVersion)
    {
        var pluginInfo = new FilePluginInfo();

        pluginInfo.Padding = br.ReadBytes(2);
        pluginInfo.PluginCount = br.ReadByte();

        pluginInfo.Plugins = new List<FilePlugin>();
        pluginInfo.LightPlugins = new List<FilePlugin>();
        pluginInfo.MediumPlugins = new List<FilePlugin>();

        // loop through normal plugins
        for (int i = 0; i < pluginInfo.PluginCount; i++)
        {
            pluginInfo.Plugins.Add(ReadPlugin(br, infoSaveVersion));
        }

        pluginInfo.LightPluginCount = br.ReadUInt16();

        // loop through light plugins
        for (int i = 0; i < pluginInfo.LightPluginCount; i++)
        {
            pluginInfo.LightPlugins.Add(ReadPlugin(br, infoSaveVersion));
        }

        // previous save versions didn't have medium plugins
        if (infoSaveVersion >= 122)
        {
            pluginInfo.MediumPluginCount = br.ReadUInt32();

            // loop through medium plugins
            for (int i = 0; i < pluginInfo.MediumPluginCount; i++)
            {
                pluginInfo.MediumPlugins.Add(ReadPlugin(br, infoSaveVersion));
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

    private FilePlugin ReadPlugin(BinaryReader br, byte infoSaveVersion)
    {
        // blank plugin
        var plugin = new FilePlugin();

        // read the plugin name
        plugin.PluginName = ReadString(br);
        
        // if save version is 140 or higher, then the next byte is a flag for extra data or not
        // if it doesn't, then just return as it's probably a native plugin
        if (infoSaveVersion >= 140)
        {
            var hasExtraData = br.ReadByte();
            
            if (hasExtraData == 0)
            {
                _logger.Info($"{plugin.PluginName} has no extra data.");
                return plugin;
            }
        }
        else
        {
            // if save version is less than 140, then we have to use the native plugins list to
            // determine if it will have extra data or not
        
            // if it's a native plugin then we are done
            if (NATIVE_PLUGINS.Contains(plugin.PluginName))
            {
                _logger.Info($"{plugin.PluginName} is a native plugin.");
                return plugin;
            }
        }

        // non-native plugin OR flag is set to show we are expecting some extra info and possibly creation info
        
        // previous save versions doesn't have this extra data
        if (infoSaveVersion >= 122)
        {
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
            plugin.AchievementFriendly = br.ReadByte();
        }

        _logger.Info($"{plugin.PluginName} is a normal plugin ({plugin.CreationName}).");
        return plugin;
    }

    static FileHeader ReadHeader(BinaryReader br)
    {
        var header = new FileHeader();

        header.EngineVersion = br.ReadUInt32();
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