using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Table;
using SampleStore.Models;
using SampleStore;
using Microsoft.WindowsAzure.Storage.Blob;
using NAudio.Wave;
using NLayer.NAudioSupport;
using Microsoft.WindowsAzure.Storage;
using System.Configuration;

namespace Sample_WebJob
{
    public static class Functions
    {
        // The trigger method, this runs when a new message is detected in a queue. 
        // The queue is named "audiosamplemaker".
        // The storage container name is "musiclibrary"; "audio" and "originalAudio" are the the folder names. 
        // The "{queueTrigger}" is an inbuilt variable taking on value of contents of message automatically;
        // And then the other variables gets their values automatically.
        public static void GenerateAudioSample([QueueTrigger("audiosamplemaker")] SampleEntity sampleInQueue,
                                    [Table("Samples", "{PartitionKey}", "{RowKey}")] SampleEntity sampleInTable,
                                    [Table("Samples")] CloudTable tableBinding, TextWriter logger)
        {
            // GETTING THE BLOB STORAGE using the RowKey
            CloudStorageAccount storageAccount;
            CloudTableClient tableClient;
            storageAccount = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["AzureWebJobsStorage"].ToString());

            // Create a retrieve operation that takes a sample entity
            TableOperation getOperation = TableOperation.Retrieve<SampleEntity>("Samples_Partition_1", sampleInQueue.RowKey);

            // Execute the retrieve operation
            TableResult getOperationResult = tableBinding.Execute(getOperation);
            sampleInTable = (SampleEntity)getOperationResult.Result;

            // use log.WriteLine             
            //logger.WriteLine("Mp3Blob " + sampleInTable.Mp3Blob);
            //logger.WriteLine("GeneratedAudioSample started...");
            var inputBlob = BlobStorageService.getCloudBlobContainer().GetBlockBlobReference("originaludio/" + sampleInTable.Mp3Blob);

            String sampleBlobName = String.Format("{0}{1}", Guid.NewGuid(), ".mp3");

            var outputBlob = BlobStorageService.getCloudBlobContainer().GetBlockBlobReference("audio/" + sampleBlobName);

            // Open streams to blobs for reading and writing as appropriate.
            // Pass references to application specific methods
            // Assign the MIME type for mp3 which is audio/mpeg3
            using (Stream input = inputBlob.OpenRead())
            using (Stream output = outputBlob.OpenWrite())
            {
                CreateSample(input, output, 20);
                outputBlob.Properties.ContentType = "audio/mpeg3";
            }         
            
            sampleInTable.SampleDate = DateTime.Now;
            sampleInTable.SampleMp3Blob = sampleBlobName;     

            // Create the TableOperation that inserts the sample entity.
            var updateOperation = TableOperation.InsertOrReplace(sampleInTable);

            // Execute the insert operation.
            tableBinding.Execute(updateOperation);
            // Use log.WriteLine() for trace output
            logger.WriteLine("GenerateSample() completed...");
        }

        // The Method createSample to create MP3 samples - which takes three parameters, input, output, and the duration of the mp3 file.
        private static void CreateSample(Stream input, Stream output, int duration)
        {
            using (var reader = new Mp3FileReader(input, wave => new NLayer.NAudioSupport.Mp3FrameDecompressor(wave)))
            {
                // Declare an mp3 frame - initialize it to the next reader
                // Instantiate frameTimeLength, framesRequired
                // Initialize the frameNumber to zero
                Mp3Frame frame;
                frame = reader.ReadNextFrame();
                int frameTimeLength = (int)(frame.SampleCount / (double)frame.SampleRate * 1000.0);
                int framesRequired = (int)(duration / (double)frameTimeLength * 1000.0);

                int frameNumber = 0;
                // While the next frame is not equal to null, increment the frame number 
                while ((frame = reader.ReadNextFrame()) != null)
                {
                    frameNumber++;
                    // If the frameNumber is less or equal the framesRequired, take the output, else break it
                    if (frameNumber <= framesRequired)
                    {
                        output.Write(frame.RawData, 0, frame.RawData.Length);
                    }
                    else break;
                }
            }
        }


    }
}
