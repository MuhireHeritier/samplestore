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

namespace SampleStore.Controllers
{
    public class DataController : ApiController
    {
        private const String partitionName = "Samples_Partition_1";
        private CloudStorageAccount storageAccount;
        private BlobStorageService blobService = new BlobStorageService();
        private SamplesController sampleController = new SamplesController();
        private CloudTableClient tableClient;
        private CloudTable table;
        private CloudQueueService cloudQueue = new CloudQueueService();

        public DataController()
        {
            storageAccount = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["AzureWebJobsStorage"].ToString());
            tableClient = storageAccount.CreateCloudTableClient();
            table = tableClient.GetTableReference("Samples");
        }
        private CloudBlobContainer getMusicContainer()
        {
            return blobService.getCloudBlobContainer();
        }
        private CloudQueue getThumbnailMakerQueue()
        {
            return cloudQueue.getCloudQueue();
        }

        // GET: api/Data/5
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

                var blob = blobService.getCloudBlobContainer().GetBlockBlobReference("audio/" + sampleEntity.Mp3Blob);
                Stream blobStream = blob.OpenRead();
                HttpResponseMessage message = new HttpResponseMessage(HttpStatusCode.OK);
                message.Content = new StreamContent(blobStream);
                message.Content.Headers.ContentLength = blob.Properties.Length;
                message.Content.Headers.ContentType = new
                System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg3");
                message.Content.Headers.ContentDisposition = new
                System.Net.Http.Headers.ContentDispositionHeaderValue("audio")
                {
                    FileName = blob.Name,
                    Size = blob.Properties.Length
                };
                return Ok(message);
            }
        }
        // PUT: api/Data/5
        public IHttpActionResult Put(string id)
        {

            // Create a retrieve operation that takes a sample entity.
            TableOperation getOperation = TableOperation.Retrieve<SampleEntity>(partitionName, id);

            // Execute the retrieve operation.
            TableResult getOperationResult = table.Execute(getOperation);

            // Construct response
            if (getOperationResult.Result == null) return NotFound();
            else
            {
                // Assign the result to a SampleEntity object.
                SampleEntity updateEntity = (SampleEntity)getOperationResult.Result;

                //Code to delete any existing blobs
                deleteOldBlobs(updateEntity);

                //make the filename unique in the blob container
                var fileName = updateEntity.Title;

                //Create the path for the blob
                String path = "audio/" + fileName + ".mp3";


                //Uploading the blob content from the Http stream
                var blob = blobService.getCloudBlobContainer().GetBlockBlobReference(path);
                var request = HttpContext.Current.Request;
                blob.Properties.ContentType = "audio/mpeg3";
                blob.UploadFromStream(request.InputStream);

                //update the relevant entity with the new blob name
                updateEntity.Mp3Blob = blob.Name;

                var baseUrl = Request.RequestUri.GetLeftPart(UriPartial.Authority);
                String sampleURL = baseUrl.ToString() + "/api/data/" + id;

                //Also update the URL and SampleBlobURL and SampleSate
                updateEntity.SampleMp3URL = sampleURL;
                updateEntity.SampleMp3Blob = null;
                updateEntity.SampleDate = null;

                var sampleQueue = cloudQueue.getCloudQueue();
                var queueMessageSample = new SampleEntity(partitionName, id);
                sampleQueue.AddMessage(new CloudQueueMessage(JsonConvert.SerializeObject(queueMessageSample)));

                return StatusCode(HttpStatusCode.NoContent);
            }
        }

        private IHttpActionResult deleteOldBlobs(SampleEntity updateEntity)
        {

            TableOperation insertOperation = TableOperation.InsertOrReplace(updateEntity);

            if (updateEntity.Mp3Blob != null)
            {
                var blob = blobService.getCloudBlobContainer().GetBlockBlobReference("audio/" + updateEntity.Mp3Blob);
                blob.DeleteIfExists();
                blob = null;
                updateEntity.SampleBlobURL = null;
                updateEntity.SampleMp3Blob = null;
                updateEntity.SampleMp3URL = null;
            }
            if (updateEntity.SampleMp3Blob != null)
            {
                var sampleMp3Blob = blobService.getCloudBlobContainer().GetBlockBlobReference("audio/" + updateEntity.Mp3Blob);
                sampleMp3Blob.DeleteIfExists();
                sampleMp3Blob = null;
                updateEntity.SampleBlobURL = null;
                updateEntity.SampleMp3Blob = null;
                updateEntity.SampleMp3URL = null;
            }

            return StatusCode(HttpStatusCode.NoContent);

        }
    }
}