using NLog;
using System.CommandLine;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ionic.Zlib;

namespace StarfieldSaveTool;

class Program
{
    private static Logger logger;
   
    static async Task<int> Main(string[] args)
    {
        var filesArgument = new Argument<string>(name: "file", description: "Starfield Save File(s) (.sfs) to read, semi-color delimited string");
        
        var jsonOutputOption = new Option<bool>(new[] { "--output-json-file", "-j" }, () => false, "Write JSON output to file");
        var rawOutputOption = new Option<bool>(new[] { "--output-raw-file", "-r" }, () => false, "Write raw output to file");
        var changeFileOption = new Option<bool>(new[] { "--change-file", "-c" }, () => false, "Test change and write back to file");

        var rootCommand = new RootCommand("Decompress Starfield Save file")
        {
            filesArgument,
            jsonOutputOption,
            rawOutputOption,
            changeFileOption
        };
        

        rootCommand.SetHandler(Start, filesArgument, jsonOutputOption, rawOutputOption, changeFileOption);

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

    private static void Start(string files, bool jsonOutputOption, bool rawOutputOption, bool changeFileOption)
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();

        logger.Info("StarfieldSaveTool Started");
        
        // split the files argument into an array and remove any empty entries
        var fileArray = files.Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        logger.Debug($"Argument is {fileArray.Length} file(s):\n{string.Join("\n", fileArray)}");
        
        var outputList = new List<DatFile>();
        
        try
        {   
            foreach (var file in fileArray)
            {
                var sfs = new SfsFile(file);
                sfs.ProcessFile();
                var decompressedBytes = sfs.DecompressedChunks;
                
                logger.Debug($"Decompressed {decompressedBytes.Length} bytes in {sw.Elapsed:ss\\.ffff} seconds.");

                // write decompressed data to disk if option is set
                if (rawOutputOption)
                {
                    var path = Path.ChangeExtension(file, ".dat");
                    logger.Debug($"Writing Raw to {path}...");
                    File.WriteAllBytes(path, decompressedBytes);
                    logger.Info($"Raw output written to {path}"); 
                }

                // read the decompressed data into a new file
                var dat = new DatFile(decompressedBytes, Path.GetFileName(file));
                dat.ProcessFile();
                
                // write decompressed data to disk if option is set
                if (changeFileOption)
                {
                    sw.Restart();

                    // test change and write back to file?
                    //dat.ChangeFile();
                    //logger.Debug($"Compressed {dat.Data.Length} bytes in {sw.Elapsed:ss\\.ffff} seconds.");

                    // test write back to file
                    var path = AddSuffixToFilePath(file, "_modified");
                    sfs.WriteFile(path, dat.Data);
                    logger.Info($"sfs written to {path}");
                }
                
                outputList.Add(dat);
            }
            
            var newJson = JsonSerializer.Serialize(outputList, new JsonSerializerOptions
            { 
                WriteIndented = true, 
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                TypeInfoResolver = SourceGenerationContext.Default
            });
                
            if (jsonOutputOption)
            {
                var path = Path.Combine(Path.GetDirectoryName(fileArray[0]), "StarfieldSaveTool.json");
                logger.Debug($"Writing JSON to {path}...");
                File.WriteAllText(path, newJson);
                logger.Info($"JSON output written to {path}");
            }
            
            // always write to console
            Console.WriteLine(newJson);
            
            logger.Debug($"Processed in {sw.Elapsed:s\\.ffff} seconds");
            
        }
        catch (FileNotFoundException fnfe)
        {
            // Exception handler for FileNotFoundException
            // We just inform the user that there is no such file
            logger.Error($"The file {fnfe.FileName} is not found.");
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

     
    public static string AddSuffixToFilePath(string originalFilePath, string suffix)
    {
        // Extract the directory path
        var directoryPath = Path.GetDirectoryName(originalFilePath);
    
        // Extract the file name without the extension
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalFilePath);
    
        // Extract the file extension
        var fileExtension = Path.GetExtension(originalFilePath);
    
        // Concatenate the new file path with the suffix before the extension
        var newFilePath = Path.Combine(directoryPath, $"{fileNameWithoutExtension}{suffix}{fileExtension}");
    
        return newFilePath;
    }

    
    
    
    
}