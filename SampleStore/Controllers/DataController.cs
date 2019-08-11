using SampleStore.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.Http.Description;

namespace SampleStore.Controllers
{
    public class DataController : ApiController
    {
        private const String partitionName = "Samples_Partition_1";
        private CloudStorageAccount storageAccount;
        private BlobStorageService blobService = new BlobStorageService();
        private CloudTableClient tableClient;
        private CloudTable table;

        public DataController()
        {
            storageAccount = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["AzureWebJobsStorage"].ToString());
            tableClient = storageAccount.CreateCloudTableClient();
            table = tableClient.GetTableReference("Samples");
        }

        // GET: api/Data/5
        [ResponseType(typeof(string))]
        public IHttpActionResult Get(string id)
        {
            // Create a retrieve operation that takes a sample entity.
            TableOperation getOperation = TableOperation.Retrieve<SampleEntity>(partitionName, id);

            // Execute the retrieve operation.
            TableResult getOperationResult = table.Execute(getOperation);

            // Construct response
            if (getOperationResult.Result == null) return NotFound();
            else
            {
                SampleEntity sampleEntity = (SampleEntity)getOperationResult.Result;
                if (sampleEntity.Mp3Blob == null)
                {
                    return NotFound();
                }

                var blob = BlobStorageService.getCloudBlobContainer().GetBlockBlobReference("audio/" + sampleEntity.SampleMp3Blob);
                Stream blobStream = blob.OpenRead();
                HttpResponseMessage message = new HttpResponseMessage(HttpStatusCode.OK);
                message.Content = new StreamContent(blobStream);
                message.Content.Headers.ContentLength = blob.Properties.Length;
                message.Content.Headers.ContentType = new
                System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg3");
                message.Content.Headers.ContentDisposition = new
                System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                {
                    FileName = blob.Name,
                    Size = blob.Properties.Length
                };
                return ResponseMessage(message);
            }
        }
        // PUT: api/Data/5
        [ResponseType(typeof(Sample))]
        public IHttpActionResult Put(string id)
        {

            // Create a retrieve operation that takes a sample entity.
            TableOperation getOperation = TableOperation.Retrieve<SampleEntity>(partitionName, id);

            // Execute the retrieve operation.
            TableResult getOperationResult = table.Execute(getOperation);

            // Assign the result to a SampleEntity object.
            SampleEntity updateEntity = (SampleEntity)getOperationResult.Result;

            Sample sample = new Sample()
            {
                SampleID = updateEntity.RowKey,
                Title = updateEntity.Title,
                Artist = updateEntity.Artist
            };

            if (updateEntity != null)
            {
                deleteOldBlobs(updateEntity);
            }

            // CREATE NAME FOR THE NEW SAMPLE. make the filename unique in the blob container
            string mp3BlobName = string.Format("{0}{1}", Guid.NewGuid(), ".mp3");

            //Create the path for the new blob
            string path = "audio/" + mp3BlobName;


            //Uploading the blob content from the Http stream
            var blob = BlobStorageService.getCloudBlobContainer().GetBlockBlobReference(path);
            var request = HttpContext.Current.Request;
            //var mp3Blob = BlobStorageService.getCloudBlobContainer().GetBlockBlobReference("originalAudio/" + mp3BlobName);
            blob.Properties.ContentType = "audio/mpeg3";

            // save the uploaded blob
            blob.UploadFromStream(request.InputStream);
            blob.SetMetadata();

            //update the relevant entity with the new blob name
            updateEntity.Mp3Blob = mp3BlobName;

            var baseUrl = Request.RequestUri.GetLeftPart(UriPartial.Authority);
            string sampleURL = baseUrl.ToString() + "/api/data/" + id;

            //Also update the URL and SampleBlobURL and SampleSate
            updateEntity.SampleMp3URL = sampleURL;
            //updateEntity.SampleMp3Blob = null;
            updateEntity.SampleDate = DateTime.Now;

            // Execute the insert operation
            TableOperation updatesOperation = TableOperation.InsertOrReplace(updateEntity);
            table.Execute(updatesOperation);

            //Add message in the queue to pick UP THE NEW BLOB
            //var sampleQueue = cloudQueue.getCloudQueue();
            //var queueMessageSample = new SampleEntity(partitionName, id);
            CloudQueueService.getCloudQueue().AddMessage(new CloudQueueMessage(JsonConvert.SerializeObject(updateEntity)));

            return Ok(sample);
        }

        
        /// <summary>
        /// Delete any existing blob
        /// </summary>
        /// <param name="updateEntity"></param>
        /// <returns></returns>

        private IHttpActionResult deleteOldBlobs(SampleEntity updateEntity)
        {

            var updateOperation = TableOperation.InsertOrReplace(updateEntity);
            //TableOperation insertOperation = TableOperation.InsertOrReplace(updateEntity);

            if (updateEntity.Mp3Blob != null || updateEntity.SampleMp3Blob != null || updateEntity.SampleDate != null)
            {
                //var blob = blobService.getCloudBlobContainer().GetBlockBlobReference("audio/" + updateEntity.Mp3Blob);
                //blob.DeleteIfExists();
                //blob = null;
                updateEntity.Mp3Blob = null;
                updateEntity.SampleMp3Blob = null;
                updateEntity.SampleMp3URL = null;
                updateEntity.SampleDate = null;

                // Execute the insert operation
                table.Execute(updateOperation);

                // GET BLOB REFERENCE AND DELETE
                BlobStorageService blobService = new BlobStorageService();
                var mp3Blob = BlobStorageService.getCloudBlobContainer().GetBlockBlobReference("originalAudio/" + updateEntity.Mp3Blob);
                var sampleMp3Blob = BlobStorageService.getCloudBlobContainer().GetBlockBlobReference("audio/" + updateEntity.Mp3Blob);

                // DELETE ANY EXSITING BLOB
                mp3Blob.DeleteIfExists();
                sampleMp3Blob.DeleteIfExists();
            }

            return StatusCode(HttpStatusCode.NoContent);
        }
    }
}