﻿using System;
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
        public static void GenerateAudioSample([QueueTrigger("audiosamplemaker")] SampleEntity sampleInQueue,
                                    [Table("Samples", "{PartitionKey}", "{RowKey}")] SampleEntity sampleInTable,
                                    [Table("Samples")] CloudTable tableBinding, TextWriter logger)
        {
            // use log.WriteLine 
            //logger.WriteLine("GeneratedAudioSample started...");
            //logger.WriteLine("Input blob is " + sampleInQueue);
            // GETTING THE BLOB STORAGE using the RowKey
            CloudStorageAccount storageAccount;
            CloudTableClient tableClient;
            storageAccount = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["AzureWebJobsStorage"].ToString());
            //tableClient = storageAccount.CreateCloudTableClient();
            //tableBinding = tableClient.GetTableReference("Samples");

            // Create a retrieve operation that takes a sample entity
            TableOperation getOperation = TableOperation.Retrieve<SampleEntity>("Samples_Partition_1", sampleInQueue.RowKey);

            // Execute the retrieve operation
            TableResult getOperationResult = tableBinding.Execute(getOperation);
            sampleInTable = (SampleEntity)getOperationResult.Result;

            // use log.WriteLine 
            //logger.WriteLine("Mp3Blob " + sampleInTable.Mp3Blob);
            //logger.WriteLine("GeneratedAudioSample started...");
            var inputBlob = BlobStorageService.getCloudBlobContainer().GetBlockBlobReference("audio/" + sampleInTable.Mp3Blob);

            String sampleBlobName = String.Format("{0}{1}", Guid.NewGuid(), ".mp3");

            var outputBlob = BlobStorageService.getCloudBlobContainer().GetBlockBlobReference("audio/" + sampleBlobName);

            // Open streams to blobs for reading and writing as appropriate.
            // Pass references to application specific methods
            using (Stream input = inputBlob.OpenRead())
            using (Stream output = outputBlob.OpenWrite())
            {
                CreateSample(input, output, 20);
                outputBlob.Properties.ContentType = "audio/mpeg3";
            }         
            
            sampleInTable.SampleDate = DateTime.Now;
            sampleInTable.SampleMp3Blob = sampleBlobName;
                //Guid.NewGuid().ToString() + ".mp3";          

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
