using System;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace EchoBox.Engine.Services;

public class IcoConverter
{
    public async Task<string> ConvertAndSaveToIcoAsync(string inputFilePath, string outputDirectory, string? preferredName = null, bool overwrite = false)
    {
        if (!File.Exists(inputFilePath))
        {
            throw new FileNotFoundException("Input file does not exist", inputFilePath);
        }

        Directory.CreateDirectory(outputDirectory);

        string safeName = string.IsNullOrWhiteSpace(preferredName)
            ? Path.GetFileNameWithoutExtension(inputFilePath)
            : preferredName;

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            safeName = safeName.Replace(c, '_');
        }

        string destinationPath = Path.Combine(outputDirectory, $"{safeName}.ico");
        if (!overwrite)
        {
            int counter = 1;
            while (File.Exists(destinationPath))
            {
                destinationPath = Path.Combine(outputDirectory, $"{safeName}_{counter++}.ico");
            }
        }

        // If source is already an .ico file, copy directly
        if (Path.GetExtension(inputFilePath).Equals(".ico", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(Path.GetFullPath(inputFilePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(inputFilePath, destinationPath, overwrite: true);
            }
            return destinationPath;
        }

        string tempPath = Path.GetTempFileName();
        File.Copy(inputFilePath, tempPath, overwrite: true);

        try
        {
            using (Image image = await Image.LoadAsync(tempPath))
            {
                // Resize image into 256x256 using Lanczos3 resampler
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(256, 256),
                    Mode = ResizeMode.Max,
                    Sampler = KnownResamplers.Lanczos3
                }));

                using var ms = new MemoryStream();
                await image.SaveAsPngAsync(ms);
                byte[] pngBytes = ms.ToArray();

                // Save as Windows ICO container wrapping PNG stream
                using var fs = File.Create(destinationPath);
                using var bw = new BinaryWriter(fs);

                // ICONDIR structure
                bw.Write((ushort)0); // Reserved
                bw.Write((ushort)1); // Type 1 = ICO
                bw.Write((ushort)1); // Image count = 1

                // ICONDIRENTRY structure
                bw.Write((byte)0); // Width 256 (0 means 256)
                bw.Write((byte)0); // Height 256 (0 means 256)
                bw.Write((byte)0); // Color count
                bw.Write((byte)0); // Reserved
                bw.Write((ushort)1); // Planes
                bw.Write((ushort)32); // Bits per pixel
                bw.Write((uint)pngBytes.Length); // Image size
                bw.Write((uint)(6 + 16)); // Offset to image data (header 6 bytes + entry 16 bytes)

                // Image Data
                bw.Write(pngBytes);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }

        return destinationPath;
    }
}
