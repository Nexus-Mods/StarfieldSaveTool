using NLog;
using System.CommandLine;
using System.Diagnostics;
using System.Text;
using Ionic.Zlib;

namespace StarfieldSaveTool;

class Program
{
    private static Logger logger;
   
    static async Task<int> Main(string[] args)
    {
        var fileArgument = new Argument<FileInfo>(name: "file", description: "Starfield Save File (.sfs) to read");
        
        var jsonOutputOption = new Option<bool>(new[] { "--output-json-file", "-j" }, () => false, "Write JSON output to file");
        var rawOutputOption = new Option<bool>(new[] { "--output-raw-file", "-r" }, () => false, "Write raw output to file");
        var changeFileOption = new Option<bool>(new[] { "--change-file", "-c" }, () => false, "Test change and write back to file");

        var rootCommand = new RootCommand("Decompress Starfield Save file")
        {
            fileArgument,
            jsonOutputOption,
            rawOutputOption,
            changeFileOption
        };
        

        rootCommand.SetHandler(Start, fileArgument, jsonOutputOption, rawOutputOption, changeFileOption);

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

    private static void Start(FileInfo file, bool jsonOutputOption, bool rawOutputOption, bool changeFileOption)
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();

        logger.Info("StarfieldSaveTool Started");
        logger.Debug($"fileArgument={file.FullName}");

        try
        {   
            var sfs = new SfsFile(file);
            sfs.ProcessFile();
            var decompressedBytes = sfs.DecompressedChunks;
            
            logger.Debug($"Decompressed {decompressedBytes.Length} bytes in {sw.Elapsed:ss\\.ffff} seconds.");

            // write decompressed data to disk if option is set
            if (rawOutputOption)
            {
                var path = Path.ChangeExtension(file.FullName, ".dat");
                logger.Debug($"Writing Raw to {path}...");
                File.WriteAllBytes(path, decompressedBytes);
                logger.Info($"Raw output written to {path}"); 
            }

            // read the decompressed data into a new file
            var dat = new DatFile(decompressedBytes);
            dat.ProcessFile();
            
            // write decompressed data to disk if option is set
            if (changeFileOption)
            {
                sw.Restart();

                // test change and write back to file?
                //dat.ChangeFile();
                //logger.Debug($"Compressed {dat.Data.Length} bytes in {sw.Elapsed:ss\\.ffff} seconds.");

                // test write back to file
                var path = AddSuffixToFilePath(file.FullName, "_modified");
                sfs.WriteFile(path, dat.Data);
                logger.Info($"sfs written to {path}");
            }

            var json = dat.ToJson();

            if (jsonOutputOption)
            {
                var path = Path.ChangeExtension(file.FullName, ".json");
                logger.Debug($"Writing JSON to {path}...");
                File.WriteAllText(path, json);
                logger.Info($"JSON output written to {path}");
            }
            
            // always write to console
            Console.WriteLine(json);
            
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