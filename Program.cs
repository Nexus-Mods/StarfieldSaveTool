using NLog;
using System.CommandLine;
using System.Diagnostics;
using System.Text;
using Ionic.Zlib;
using System.Xml;
using Newtonsoft.Json;
using CompressionMode = Ionic.Zlib.CompressionMode;

namespace StarfieldSaveTool;

struct SfsFileHeader
{
    public char[] magic;
    public int version;
    public long dataOffset;
    public long unknown0;
    public long compressedDataOffset;
    public long uncompressedDataSize;
    public float version1;
    public int unknown1;
    public long sizeUncompressedChunks;
    public long paddingSize;
    public int unknown2;
    public char[] zip;
    public int chunkCount;
    public int[] compressedChunkSizes;
    public int[] compressedChunkSizesWithoutPadding;
}

public struct Header
{
    public char[] magic;
    public uint headerSize;
    public uint version;
    public byte saveVersion;
    public uint saveNumber;
    public ushort playerNameSize;
    public string playerName;
    public uint playerLevel;
    public ushort playerLocationSize;
    public string playerLocation;
    public ushort playtimeSize;
    public string playtime;
    public ushort raceNameSize;
    public string raceName;
    public ushort gender;
    public float experience;
    public float experienceRequired;
    public ulong time;
    public DateTime dateTime;
}

public struct Info
{
    public byte[] temp;
    public byte saveVersion;
    public ushort gameVersionSize0;
    public string gameVersion0;
    public ushort gameVersionSize1;
    public string gameVersion1;
    public ushort pluginInfoSize;
} 

public struct PluginInfo {

    public byte[] Padding;
    
    public byte PluginCount;
    public ushort LightPluginCount;
    public uint MediumPluginCount;
    
    public List<PluginBase> Plugins;
    public List<PluginBase> LightPlugins;
    public List<PluginBase> MediumPlugins;
}

public abstract class PluginBase
{
    public ushort PluginNameSize { get; set; }
    public string PluginName { get; set; }
}

public class Plugin: PluginBase
{

}

public class ExtendedPlugin : PluginBase
{
    public ushort FlagsSize { get; set; }
    public byte IsCreation { get; set; }
   public byte[] Padding { get; set; }
}

public class CreationPlugin : Plugin
{
    public ushort CreationNameSize { get; set; }
    public string CreationName { get; set; }
    public ushort CreationIdSize { get; set; }
    public string CreationId { get; set; }
    public byte[] Padding { get; set; }
}

public class DecompressedSaveFile(Stream stream)
{
    private Header _header;
    private Info _info;
    private PluginInfo _pluginInfo;
    
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
        _pluginInfo = ReadPluginInfo(br);
         
    }

    public string ToJson()
    {
        var json = JsonConvert.SerializeObject(_pluginInfo, Newtonsoft.Json.Formatting.Indented);
        return json;
    }
    
    
     private PluginInfo ReadPluginInfo(BinaryReader br)
     {
            _pluginInfo = new PluginInfo();

            _pluginInfo.Padding = br.ReadBytes(2);
            _pluginInfo.PluginCount = br.ReadByte();
            
            _pluginInfo.Plugins = new List<PluginBase>();

            // loop through normal plugins
            for (int i = 0; i < _pluginInfo.PluginCount; i++)
            {
                _pluginInfo.Plugins.Add(ReadPlugin(br));
            }
            
            _pluginInfo.LightPluginCount = br.ReadUInt16();
            _pluginInfo.LightPlugins = new List<PluginBase>();
            
            // loop through light plugins
            for (int i = 0; i < _pluginInfo.LightPluginCount; i++)
            {
                _pluginInfo.LightPlugins.Add(ReadPlugin(br));
            }

            _pluginInfo.MediumPluginCount = br.ReadUInt32();
            _pluginInfo.MediumPlugins = new List<PluginBase>();
            
            // loop through light plugins
            for (int i = 0; i < _pluginInfo.MediumPluginCount; i++)
            {
                _pluginInfo.MediumPlugins.Add(ReadPlugin(br));
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
             creationPlugin.Padding = br.ReadBytes(9);
             
             _logger.Info($"{pluginName} is a creation plugin ({creationPlugin.CreationName}).");
             return creationPlugin;
         }
         else
         {
             // extended plugin
             ExtendedPlugin extendedPlugin = new ExtendedPlugin();
             extendedPlugin.PluginName = pluginName;
             extendedPlugin.FlagsSize = br.ReadUInt16();
             extendedPlugin.IsCreation = br.ReadByte();
             extendedPlugin.Padding = br.ReadBytes(10);
             _logger.Info($"{pluginName} is a extended plugin.");
             return extendedPlugin;
         }

     }

     private Info ReadInfo(BinaryReader br)
     {
            var info = new Info();
            
            info.temp = br.ReadBytes(12);
            info.saveVersion = br.ReadByte();
            info.gameVersionSize0 = br.ReadUInt16();
            info.gameVersion0 = Encoding.ASCII.GetString(br.ReadBytes(info.gameVersionSize0));
            info.gameVersionSize1 = br.ReadUInt16();
            info.gameVersion1 = Encoding.ASCII.GetString(br.ReadBytes(info.gameVersionSize1));
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
            
        return header;
    }
}

class Program
{
    private static Logger logger;

    private const string SFS_MAGIC = "BCPS";
   
    static async Task<int> Main(string[] args)
    {
        var fileArgument = new Argument<FileInfo>(name: "file", description: "Starfield Save File (.sfs) to read");

        var rootCommand = new RootCommand("Decompress Starfield Save file")
        {
            fileArgument,
        };

        rootCommand.SetHandler(ReadFile, fileArgument);

        // logging stuff

        // create a configuration instance
        var config = new NLog.Config.LoggingConfiguration();

        // create a console logging target
        var logConsole = new NLog.Targets.ConsoleTarget();

        var debugConsole = new NLog.Targets.DebugSystemTarget();

        // send logs with levels from Info to Fatal to the console
        config.AddRule(NLog.LogLevel.Warn, NLog.LogLevel.Fatal, logConsole);
        // send logs with levels from Debug to Fatal to the console
        config.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, debugConsole);

        // apply the configuration
        LogManager.Configuration = config;

        // create a logger
        logger = LogManager.GetCurrentClassLogger();

        return await rootCommand.InvokeAsync(args);
    }

    private static void ReadFile(FileInfo file)
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();

        logger.Info("VortexPak Started");
        logger.Debug($"fileArgument={file.FullName}");

        try
        {
            using var originalBr = new BinaryReader(file.OpenRead());

            var magic = originalBr.ReadBytes(4);
            
            if (Encoding.ASCII.GetString(magic) != SFS_MAGIC)
            {
                logger.Error("Invalid file format");
                throw new Exception($"Not a valid Starfield save. Magic bytes not found.");
            }
            
            // reset the file pointer to the beginning of the file
            originalBr.BaseStream.Seek(0, SeekOrigin.Begin);
            
            var sfsFileHeader = ReadOriginalHeader(originalBr);
            
            // Get the directory, filename without extension, and create the new file path with the new extension
            string directory = Path.GetDirectoryName(file.FullName);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.FullName);
            string newFilePath = Path.Combine(directory, fileNameWithoutExtension + ".decompressed");

            var ms = DecompressChunks(originalBr, sfsFileHeader);
            
            logger.Debug($"Decompressed in {sw.Elapsed:ss\\.ffff} seconds");

            //originalBr.Close();
            
            var saveFile = new DecompressedSaveFile(ms);

            
            using var br = new BinaryReader(ms);
            br.BaseStream.Seek(0, SeekOrigin.Begin);

            var decompressedSaveFile = new DecompressedSaveFile(ms);
            decompressedSaveFile.ReadFile();
            Console.WriteLine(decompressedSaveFile.ToJson());
            
            logger.Debug($"Processed in {sw.Elapsed:ss\\.ffff} seconds");
            
        }
        catch (FileNotFoundException fnfe)
        {
            // Exception handler for FileNotFoundException
            // We just inform the user that there is no such file
            logger.Error($"The file {file} is not found.");
        }
        catch (IOException ioe)
        {
            // Exception handler for other input/output exceptions
            logger.Error(ioe.StackTrace);
        }
        catch (Exception ex)
        {
            // Exception handler for any other exception that may occur and was not already handled specifically
            logger.Error(ex.ToString());
        }

    }

     


    private static SfsFileHeader ReadOriginalHeader(BinaryReader br)
    {
        var sfsFileHeader = new SfsFileHeader();
        
        sfsFileHeader.magic = br.ReadChars(4);
        sfsFileHeader.version = br.ReadInt32();
        sfsFileHeader.dataOffset = br.ReadInt64();
        sfsFileHeader.unknown0 = br.ReadInt64();
        sfsFileHeader.compressedDataOffset = br.ReadInt64();
        sfsFileHeader.uncompressedDataSize = br.ReadInt64();
        sfsFileHeader.version1 = br.ReadSingle();
        sfsFileHeader.unknown1 = br.ReadInt32();
        sfsFileHeader.sizeUncompressedChunks = br.ReadInt64();
        sfsFileHeader.paddingSize = br.ReadInt64();
        sfsFileHeader.unknown2 = br.ReadInt32();
        sfsFileHeader.zip = br.ReadChars(4);
        sfsFileHeader.chunkCount = (int)Math.Ceiling((float)sfsFileHeader.uncompressedDataSize / sfsFileHeader.sizeUncompressedChunks);
        sfsFileHeader.compressedChunkSizes = new int[sfsFileHeader.chunkCount];
        sfsFileHeader.compressedChunkSizesWithoutPadding = new int[sfsFileHeader.chunkCount];

        // read the compressed chunk sizes
        for (var i = 0; i < sfsFileHeader.chunkCount; i++)
        {
            sfsFileHeader.compressedChunkSizes[i] = br.ReadInt32();
            
            // need the size without padding
            sfsFileHeader.compressedChunkSizesWithoutPadding[i] = PadToNearestSize((int)sfsFileHeader.paddingSize, sfsFileHeader.compressedChunkSizes[i]);
        }
        
        return sfsFileHeader;
    }
    
    static MemoryStream DecompressChunks(BinaryReader br, SfsFileHeader header)
    {
        //using FileStream outputFileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        MemoryStream decompressedDataStream = new MemoryStream();
        
        // go to start of compressed data blocks
        br.BaseStream.Seek(header.compressedDataOffset, SeekOrigin.Begin);
        
        for (var i = 0; i < header.chunkCount; i++)
        {
            // Read the compressed chunk data
            byte[] compressedData = br.ReadBytes(header.compressedChunkSizesWithoutPadding[i]);

            // Decompress the chunk
            byte[] decompressedData = Decompress(compressedData);

            // Write the decompressed data to the output file
            //outputFileStream.Write(decompressedData, 0, decompressedData.Length);
            decompressedDataStream.Write(decompressedData, 0, decompressedData.Length);
        }

        return decompressedDataStream;
    }
    
    static byte[] Decompress(byte[] data)
    {
        using (MemoryStream inputStream = new MemoryStream(data))
        using (MemoryStream outputStream = new MemoryStream())
        using (ZlibStream deflateStream = new ZlibStream(inputStream, CompressionMode.Decompress))
        {
            deflateStream.CopyTo(outputStream);
            return outputStream.ToArray();
        }
    }
    
    // Function to pad a size to the nearest multiple of paddingSize
    static int PadToNearestSize(int paddingSize, int size)
    {
        int maxPadSize = paddingSize - 1;
        return (size + maxPadSize) & ~maxPadSize;
    }
}