using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace BlobTriggerTask
{
    public class Function1
    {

        private readonly ILogger _logger;
        public Function1(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Function1>();
        }

        [Function("ProcessPngToJpg")]
        public async Task Run(
            [BlobTrigger("images/{name}", Connection = "AzureWebJobsStorage")] Stream inputBlob,
            string name,
            FunctionContext context)
        {
            _logger.LogInformation($"Blob trigger function processed blob: {name}");

            if (Path.GetExtension(name).ToLower() == ".png")
            {
                try
                {
                    string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
                    var blobClient = new BlobServiceClient(connectionString);
                    var containerClient = blobClient.GetBlobContainerClient("images");

                    string newBlobName = Path.ChangeExtension(name, ".jpg");

                    using (var outputStream = new MemoryStream())
                    {
                        using (var image = await Image.LoadAsync(inputBlob))
                        {
                            var jpegEncoder = new JpegEncoder
                            {
                                Quality = 75
                            };
                            image.Mutate(x => x.AutoOrient());
                            await image.SaveAsync(outputStream, jpegEncoder);
                        }

                        outputStream.Position = 0;

                        var newBlobClient = containerClient.GetBlobClient(newBlobName);
                        await newBlobClient.UploadAsync(outputStream, overwrite: true);

                        _logger.LogInformation($"Converted {name} to {newBlobName} and saved successfully.");

                        var oldBlobClient = containerClient.GetBlobClient(name);
                        await oldBlobClient.DeleteIfExistsAsync();
                        _logger.LogInformation($"Deleted original PNG blob: {name}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error processing blob {name}: {ex.Message}");
                    throw;
                }
            }
            else
            {
                _logger.LogInformation($"Blob {name} is not a PNG file. No action taken.");
            }
        }
    }

}