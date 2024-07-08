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
    public int version0;
    public long chunkSizesOffset;
    public long unknown0;
    public long compressedDataOffset;
    public long uncompressedDataSize;
    public float version1;
    public int unknown1;
    public long sizeUncompressedChunks;
    public long paddingSize;
    public int unknown2;
    public char[] compressionType;
    public int chunkCount;
    public int[] compressedChunkSizes;
    public int[] compressedChunkSizesWithoutPadding;
}



class Program
{
    private static Logger logger;

    private const string SFS_MAGIC = "BCPS";
   
    static async Task<int> Main(string[] args)
    {
        var fileArgument = new Argument<FileInfo>(name: "file", description: "Starfield Save File (.sfs) to read");
        
        var jsonOutputOption = new Option<bool>(new[] { "--output-json-file", "-j" }, () => false, "Write JSON output to file");
        var rawOutputOption = new Option<bool>(new[] { "--output-raw-file", "-r" }, () => false, "Write raw output to file");

        var rootCommand = new RootCommand("Decompress Starfield Save file")
        {
            fileArgument,
            jsonOutputOption,
            rawOutputOption
        };
        

        rootCommand.SetHandler(ReadFile, fileArgument, jsonOutputOption, rawOutputOption);

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

    private static void ReadFile(FileInfo file, bool jsonOutputOption, bool rawOutputOption)
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
            //string fileWithoutExtension = Path.Combine(Path.GetDirectoryName(file.FullName), Path.GetFileNameWithoutExtension(file.FullName));

            var ms = DecompressChunks(originalBr, sfsFileHeader);
            
            logger.Debug($"Decompressed in {sw.Elapsed:ss\\.ffff} seconds");

            //originalBr.Close();
            
            var saveFile = new DecompressedSaveFile(ms);
            
            using var br = new BinaryReader(ms);
            br.BaseStream.Seek(0, SeekOrigin.Begin);
            
                        
            if (rawOutputOption)
            {
                var path = Path.ChangeExtension(file.FullName, ".dat");
                logger.Debug($"Writing Raw to {path}...");
                File.WriteAllBytes(path, ms.ToArray());
                logger.Info($"Raw output written to {path}");
            }

            var decompressedSaveFile = new DecompressedSaveFile(ms);
            decompressedSaveFile.ReadFile();
            
            var json = decompressedSaveFile.ToJson();
            
            Console.WriteLine(json);
            
            if (jsonOutputOption)
            {
                var path = Path.ChangeExtension(file.FullName, ".json");
                logger.Debug($"Writing JSON to {path}...");
                File.WriteAllText(path, json);
                logger.Info($"JSON output written to {path}");
            }

            
            logger.Debug($"Processed in {sw.Elapsed:s\\.ffff} seconds");
            
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

        //Console.ReadKey();
    }

     


    private static SfsFileHeader ReadOriginalHeader(BinaryReader br)
    {
        var sfsFileHeader = new SfsFileHeader();
        
        sfsFileHeader.magic = br.ReadChars(4);
        sfsFileHeader.version0 = br.ReadInt32();
        sfsFileHeader.chunkSizesOffset = br.ReadInt64();
        sfsFileHeader.unknown0 = br.ReadInt64();
        sfsFileHeader.compressedDataOffset = br.ReadInt64();
        sfsFileHeader.uncompressedDataSize = br.ReadInt64();
        sfsFileHeader.version1 = br.ReadSingle();
        sfsFileHeader.unknown1 = br.ReadInt32();
        sfsFileHeader.sizeUncompressedChunks = br.ReadInt64();
        sfsFileHeader.paddingSize = br.ReadInt64();
        sfsFileHeader.unknown2 = br.ReadInt32();
        sfsFileHeader.compressionType = br.ReadChars(4);
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