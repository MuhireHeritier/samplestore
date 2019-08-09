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

namespace Sample_WebJob
{
    public class Functions
    {
        // This function will get triggered/executed when a new message is written 
        // on an Azure Queue called queue.
        public static void ProcessQueueMessage([QueueTrigger("queue")] string message, TextWriter log)
        {
            log.WriteLine(message);
        }

        public static void GenerateAudioSample([QueueTrigger("audiosamplemaker")] SampleEntity sampleInQueue,
                                    [Table("Samples", "{PartitionKey}", "{RowKey}")] SampleEntity sampleInTable,
                                    [Table("Samples")] CloudTable tableBinding, TextWriter logger)
        {
            // use log.WriteLine 
            logger.WriteLine("GeneratedAudioSample started...");
            logger.WriteLine("Input blob is " + sampleInQueue);


            // GETTING THE BLOB STORAGE using the RowKey
            BlobStorageService blobService = new BlobStorageService();
            CloudBlobContainer blobContainer = blobService.getCloudBlobContainer();
            CloudBlockBlob inputBlob;
            CloudBlockBlob outputBlob;

            sampleInTable.SampleDate = DateTime.Now;
            sampleInTable.SampleMp3Blob = Guid.NewGuid().ToString() + ".mp3";

            inputBlob = blobContainer.GetBlockBlobReference(sampleInQueue.Mp3Blob);
            outputBlob = blobContainer.GetBlockBlobReference(sampleInTable.SampleMp3Blob);

            // Open streams to blobs for reading and writing as appropriate.
            // Pass references to application specific methods
            using (Stream input = inputBlob.OpenRead())
            using (Stream output = outputBlob.OpenWrite())
            {
                CreateSample(input, output, 20);
                outputBlob.Properties.ContentType = "audio/mpeg3";
            }

            // Create the TableOperation that inserts the sample entity.
            var updateOperation = TableOperation.InsertOrReplace(sampleInTable);

            // Execute the insert operation.
            tableBinding.Execute(updateOperation);
            logger.WriteLine("GenerateSample() completed...");
        }

        private static void CreateSample(Stream input, Stream output, int duration)
        {
            using (var reader = new Mp3FileReader(input, wave => new NLayer.NAudioSupport.Mp3FrameDecompressor(wave)))
            {
                Mp3Frame frame;
                frame = reader.ReadNextFrame();
                int frameTimeLength = (int)(frame.SampleCount / (double)frame.SampleRate * 1000.0);
                int framesRequired = (int)(duration / (double)frameTimeLength * 1000.0);

                int frameNumber = 0;
                while ((frame = reader.ReadNextFrame()) != null)
                {
                    frameNumber++;

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
