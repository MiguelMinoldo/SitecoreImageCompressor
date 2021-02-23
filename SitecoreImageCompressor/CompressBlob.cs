using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using ImageMagick;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Threading.Tasks;

namespace SitecoreImageCompressor
{
    public static class CompressBlob
    {
        [FunctionName("CompressBlob")]
        public static async void Run([BlobTrigger("blobcontainer/{name}", Connection = "myblobtestazure_STORAGE")] CloudBlockBlob inputBlob, ILogger log)
        {
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{inputBlob.Name} \n Size: {inputBlob.Properties.Length} Bytes");

            if (inputBlob.Metadata.ContainsKey("Status") && inputBlob.Metadata["Status"] == "Processed")
            {
                log.LogInformation($"blob: {inputBlob.Name} has already been processed");
            }
            else
            {
                using (var memoryStream = new MemoryStream())
                {
                    await inputBlob.DownloadToStreamAsync(memoryStream);
                    memoryStream.Position = 0;

                    var before = memoryStream.Length;
                    var optimizer = new ImageOptimizer { OptimalCompression = true, IgnoreUnsupportedFormats = true };

                    if (optimizer.IsSupported(memoryStream))
                    {
                        var compressionResult = optimizer.Compress(memoryStream);

                        if (compressionResult)
                        {
                            var after = memoryStream.Length;
                            var gain = 100 - (float)(after * 100) / before;

                            log.LogInformation($"Optimized {inputBlob.Name} - from: {before} to: {after} Bytes. Optimized {gain}%");

                            await inputBlob.UploadFromStreamAsync(memoryStream);
                        }
                        else
                        {
                            log.LogInformation($"Image {inputBlob.Name} - compression failed...");
                        }
                    }
                    else
                    {
                        var info = MagickNET.GetFormatInformation(new MagickImageInfo(memoryStream).Format);

                        log.LogInformation($"Image {inputBlob.Name} - the format is not supported. Compression skipped - {info.Format}");
                    }
                }

                inputBlob.Metadata.Add("Status", "Processed");
                
                await inputBlob.SetMetadataAsync();
            }
        }
    }
}