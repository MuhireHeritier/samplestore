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
        //private SamplesController sampleController = new SamplesController();
        private CloudTableClient tableClient;
        private CloudTable table;
        //private CloudQueueService cloudQueue = new CloudQueueService();

        public DataController()
        {
            storageAccount = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["AzureWebJobsStorage"].ToString());
            tableClient = storageAccount.CreateCloudTableClient();
            table = tableClient.GetTableReference("Samples");
        }
        //private CloudBlobContainer getSampleContainer()
        //{
        //    return blobService.getCloudBlobContainer();
        //}
        //private CloudQueue getThumbnailMakerQueue()
        //{
        //    return cloudQueue.getCloudQueue();
        //}

        // GET: api/Data/5
        public HttpResponseMessage Get(string id)
        {

            //create a retrieve
            TableOperation getOperation = TableOperation.Retrieve<SampleEntity>(partitionName, id);

            //execute retrieve
            TableResult getOperationResult = table.Execute(getOperation);
            if (getOperationResult.Result == null) return Request.CreateErrorResponse(HttpStatusCode.NotFound, "no blob");
            SampleEntity sample = (SampleEntity)getOperationResult.Result;

            HttpResponseMessage message;
            try
            {

                CloudBlobContainer blobContainer = blobService.getCloudBlobContainer();
                CloudBlockBlob blob = blobContainer.GetBlockBlobReference(sample.SampleMp3Blob);

                if (!blob.Exists()) return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "no blob at DataController(GET): " + sample.SampleBlobURL);

                Stream blobStream = blob.OpenRead();

                message = new HttpResponseMessage(HttpStatusCode.OK);
                message.Content = new StreamContent(blobStream);
                message.Content.Headers.ContentLength = blob.Properties.Length;
                message.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg3");
                message.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                {
                    FileName = blob.Name,
                    Size = blob.Properties.Length
                };

            }
            catch (Exception e) { return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "likely no blob at DataController (GET):" + sample.SampleMp3Blob); }
            return message;

        }



    //public IHttpActionResult Get(string id)
    //{
    //    // Create a retrieve operation that takes a sample entity.
    //    TableOperation getOperation = TableOperation.Retrieve<SampleEntity>(partitionName, id);

    //    // Execute the retrieve operation.
    //    TableResult getOperationResult = table.Execute(getOperation);

    //    // Construct response
    //    if (getOperationResult.Result == null) return NotFound();
    //    else
    //    {
    //        SampleEntity sampleEntity = (SampleEntity)getOperationResult.Result;
    //        if (sampleEntity.Mp3Blob == null)
    //        {
    //            return NotFound();
    //        }

    //        var blob = blobService.getCloudBlobContainer().GetBlockBlobReference("audio/" + sampleEntity.SampleMp3Blob);
    //        Stream blobStream = blob.OpenRead();
    //        HttpResponseMessage message = new HttpResponseMessage(HttpStatusCode.OK);
    //        message.Content = new StreamContent(blobStream);
    //        message.Content.Headers.ContentLength = blob.Properties.Length;
    //        message.Content.Headers.ContentType = new
    //        System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg3");
    //        message.Content.Headers.ContentDisposition = new
    //        System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
    //        {
    //            FileName = blob.Name,
    //            Size = blob.Properties.Length
    //        };
    //        return ResponseMessage(message);
    //    }
    //}
    // PUT: api/Data/5
    public IHttpActionResult Put(string id)
        {

            // Create a retrieve operation that takes a sample entity.
            TableOperation getOperation = TableOperation.Retrieve<SampleEntity>(partitionName, id);

            // Execute the retrieve operation.
            tableClient = storageAccount.CreateCloudTableClient();
            table = tableClient.GetTableReference("Samples");
            TableResult getOperationResult = table.Execute(getOperation);

            // Construct response including A NEW DTO AS APPROPRIATE  
            if (getOperationResult.Result == null) return NotFound();
            // else GET THE SAMPLE
            else
            {
                // Assign the result to a SampleEntity object.
                SampleEntity updateEntity = (SampleEntity)getOperationResult.Result;

                //Code to delete any existing blobs
                deleteOldBlobs(updateEntity);

                //make the filename unique in the blob container
                //var fileName = updateEntity.Title;

                //Create the path for the new blob
                //String path = "audio/" + fileName + ".mp3";

                // CREATE NAME FOR THE NEW SAMPLE
                String mp3BlobName = string.Format("{0}{1}", Guid.NewGuid(), ".mp3");


                //Uploading the blob content from the Http stream
                //var blob = blobService.getCloudBlobContainer().GetBlockBlobReference(path);
                var request = HttpContext.Current.Request;
                var mp3Blob = blobService.getCloudBlobContainer().GetBlockBlobReference("originalAudio/" + mp3BlobName);
                mp3Blob.Properties.ContentType = "audio/mpeg3";

                // save the uploaded blob
                mp3Blob.UploadFromStream(request.InputStream);
                var baseUrl = Request.RequestUri.GetLeftPart(UriPartial.Authority);
                String sampleURL = baseUrl.ToString() + "/api/data/" + id;
                
                //update the relevant entity with the new blob name
                updateEntity.Mp3Blob = mp3BlobName;              

                //Also update the URL and SampleBlobURL and SampleSate
                updateEntity.SampleMp3URL = sampleURL;
                updateEntity.SampleMp3Blob = null;
                updateEntity.SampleDate = null;

                // Execute the insert operation
                TableOperation updatesOperation = TableOperation.InsertOrReplace(updateEntity);
                table.Execute(updatesOperation);

                //Add message in the queue to pick UP THE NEW BLOB
                //var sampleQueue = cloudQueue.getCloudQueue();
                var queueMessageSample = new SampleEntity(partitionName, id);
                CloudQueueService.getCloudQueue().AddMessage(new CloudQueueMessage(JsonConvert.SerializeObject(queueMessageSample)));

                return StatusCode(HttpStatusCode.NoContent);
            }
        }

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
                var mp3Blob = blobService.getCloudBlobContainer().GetBlockBlobReference("originalAudio/" + updateEntity.Mp3Blob);
                var sampleMp3Blob = blobService.getCloudBlobContainer().GetBlockBlobReference("audio/" + updateEntity.Mp3Blob);

                // DELETE ANY EXSITING BLOB
                mp3Blob.DeleteIfExists();
                sampleMp3Blob.DeleteIfExists();
            }

            return StatusCode(HttpStatusCode.NoContent);
        }
    }
}