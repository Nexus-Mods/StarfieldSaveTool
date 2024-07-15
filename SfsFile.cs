using System.Text.Json.Serialization;
using Ionic.Zlib;
using NLog;

namespace StarfieldSaveTool;

public class SfsFile(FileInfo fileInfo)
{
    struct FileHeader
    {
        public int Version0 { get; set; }
        public long ChunkSizesOffset { get; set; }
        public long Unknown0 { get; set; }
        public long CompressedDataOffset { get; set; }
        public long UncompressedDataSize { get; set; }
        public float Version1 { get; set; }
        public int Unknown1 { get; set; }
        public long SizeUncompressedChunks { get; set; }
        public long PaddingSize { get; set; }
        public int Unknown2 { get; set; }
        public char[] CompressionType { get; set; }
        public int ChunkCount { get; set; }
        public int[] CompressedChunkSizes { get; set; }
        public int[] CompressedChunkSizesWithoutPadding { get; set; }
    }
    
    [JsonIgnore] public char[] Magic { get; private set; }
    FileHeader Header { get; set; }
    public byte[] DecompressedChunks { get; private set; } = [];
    
    private Stream _fileStream = fileInfo.OpenRead();
    private Logger _logger = LogManager.GetCurrentClassLogger();

    private const string SfsMagic = "BCPS";
    private const string PadString = "nexus\0"; // used when compressing to pad to 16 byte boundary

    public void ProcessFile()
    {
        using var br = new BinaryReader(_fileStream);
        br.BaseStream.Seek(0, SeekOrigin.Begin);

        // quick check for magic bytes
        Magic = br.ReadChars(4);
        
        if (new string(Magic) != SfsMagic)
        {
            _logger.Error("Invalid file format");
            throw new Exception($"Not a valid Starfield save. Magic bytes ({SfsMagic}) not found.");
        }
        
        // read the header
        Header = ReadHeader(br);
        
        // read the compressed data blocks
        DecompressedChunks = DecompressChunks(br);
    }

    public void WriteFile(string path, byte[] data)
    {
        using var bw = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write));
        bw.BaseStream.Seek(0, SeekOrigin.Begin);
        
        // get the compressed data chunks
        var chunks = CompressChunks(data, (int)Header.SizeUncompressedChunks); 
        
        // update the header
        var fileHeader = Header;
        fileHeader.CompressedDataOffset = fileHeader.ChunkSizesOffset + chunks.Count * 4; // start of compressed data blocks : ChunkSizesOffset + size of chunks array of ints
        Header = fileHeader;
        
        // write the header
        bw.Write(SfsMagic.ToCharArray()); // "BCPS"
        bw.Write(Header.Version0);
        bw.Write(Header.ChunkSizesOffset);
        bw.Write(Header.Unknown0);
        bw.Write(Header.CompressedDataOffset);
        bw.Write(Header.UncompressedDataSize);
        bw.Write(Header.Version1);
        bw.Write(Header.Unknown1);
        bw.Write(Header.SizeUncompressedChunks);
        bw.Write(Header.PaddingSize);
        bw.Write(Header.Unknown2);
        bw.Write(Header.CompressionType); // "ZIP "
        
        // write the compressed chunk sizes
        foreach (var chunk in chunks)
        {
            bw.Write(chunk.Length);
        }
        
        // write the compressed chunks (and we need to pad)
        foreach (var chunk in chunks)
        {
            // write data chunk
            bw.Write(chunk);
            
            // work out if we need to pad with bytes to get to the next 16 byte boundary
            var padding = PadToNearestSize((int)Header.PaddingSize, chunk.Length) - chunk.Length;
            _logger.Debug($"We need to pad by {padding} bytes");
            
            // write padding to fill the gap
            bw.Write(GetPadBytesFromLoopingString(PadString, padding));
        }
    }

    private FileHeader ReadHeader(BinaryReader br)
    {
        var fileHeader = new FileHeader();
        
        fileHeader.Version0 = br.ReadInt32();
        fileHeader.ChunkSizesOffset = br.ReadInt64();
        fileHeader.Unknown0 = br.ReadInt64();
        fileHeader.CompressedDataOffset = br.ReadInt64();
        fileHeader.UncompressedDataSize = br.ReadInt64();
        fileHeader.Version1 = br.ReadSingle();
        fileHeader.Unknown1 = br.ReadInt32();
        fileHeader.SizeUncompressedChunks = br.ReadInt64();
        fileHeader.PaddingSize = br.ReadInt64();
        fileHeader.Unknown2 = br.ReadInt32();
        
        fileHeader.CompressionType = br.ReadChars(4);
        fileHeader.ChunkCount = (int)Math.Ceiling((float)fileHeader.UncompressedDataSize / fileHeader.SizeUncompressedChunks);
        fileHeader.CompressedChunkSizes = new int[fileHeader.ChunkCount];
        fileHeader.CompressedChunkSizesWithoutPadding = new int[fileHeader.ChunkCount];
        
        // read the compressed chunk sizes
        for (var i = 0; i < fileHeader.ChunkCount; i++)
        {
            fileHeader.CompressedChunkSizes[i] = br.ReadInt32();
            
            // need the size without padding
            fileHeader.CompressedChunkSizesWithoutPadding[i] = PadToNearestSize((int)fileHeader.PaddingSize, fileHeader.CompressedChunkSizes[i]);
        }
        
        return fileHeader;
    }
    
    byte[] DecompressChunks(BinaryReader br)
    {
        //using FileStream outputFileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using MemoryStream decompressedDataStream = new MemoryStream();
        
        // go to start of compressed data blocks
        br.BaseStream.Seek(Header.CompressedDataOffset, SeekOrigin.Begin);
        
        for (var i = 0; i < Header.ChunkCount; i++)
        {
            // Read the compressed chunk data
            byte[] compressedData = br.ReadBytes(Header.CompressedChunkSizesWithoutPadding[i]);

            // Decompress the chunk
            byte[] decompressedData = Decompress(compressedData);

            // Write the decompressed data to the output file
            //outputFileStream.Write(decompressedData, 0, decompressedData.Length);
            decompressedDataStream.Write(decompressedData, 0, decompressedData.Length);
        }

        return decompressedDataStream.ToArray();
    }
    
    List<byte[]> CompressChunks(byte[] data, int chunkSize)
    {
        using MemoryStream dataStream = new MemoryStream(data);
        
        // dataStream contains the data to be compressed
        List<byte[]> chunks = new List<byte[]>();
        
        // Start from the beginning of the data stream
        dataStream.Seek(0, SeekOrigin.Begin);
    
        byte[] buffer = new byte[chunkSize];
        int bytesRead;
    
        while ((bytesRead = dataStream.Read(buffer, 0, chunkSize)) > 0)
        {
            // Compress the chunk
            byte[] compressedChunk = Compress(buffer.Take(bytesRead).ToArray());
        
            // add the compressed chunk to the list
            chunks.Add(compressedChunk);
        }

        return chunks;
    }

    byte[] Compress(byte[] data)
    {
        using (MemoryStream inputStream = new MemoryStream(data))
        using (MemoryStream outputStream = new MemoryStream())
        using (ZlibStream deflateStream = new ZlibStream(outputStream, CompressionMode.Compress))
        {
            inputStream.CopyTo(deflateStream);
            deflateStream.Close();
            return outputStream.ToArray();
        }
    }
    
    byte[] Decompress(byte[] data)
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
    int PadToNearestSize(int paddingSize, int size)
    {
        int maxPadSize = paddingSize - 1;
        return (size + maxPadSize) & ~maxPadSize;
    }
    
    byte[] GetPadBytesFromLoopingString(string padString, int length)
    {
        List<byte> paddingBytes = new List<byte>();

        while (paddingBytes.Count < length)
        {
            foreach (char c in padString)
            {
                if (paddingBytes.Count >= length)
                    break;
                
                paddingBytes.Add((byte)c);
            }
        }

        return paddingBytes.ToArray();
    }
}