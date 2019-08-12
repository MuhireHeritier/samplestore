using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using SampleStore.Models;
//using Swashbuckle.Swagger.Annotations;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Web.Http;
using System.Web.Http.Description;

namespace SampleStore.Controllers
{
    // class SamplesController extends ApiController
    public class SamplesController : ApiController
    {
        // Declaring the fields - partitionName, storageAccount, tableClient, and table
        // Instantiating a new blobStorageService, queueStorageService
        private const String partitionName = "Samples_Partition_1";

        private CloudStorageAccount storageAccount;
        private CloudTableClient tableClient;
        private CloudTable table;

        private BlobStorageService blobStorageService = new BlobStorageService();
        private CloudQueueService queueStorageService = new CloudQueueService();

        // Constructor 
        public SamplesController()
        {
            storageAccount = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["AzureWebJobsStorage"].ToString());
            tableClient = storageAccount.CreateCloudTableClient();
            table = tableClient.GetTableReference("Samples");
        }

        private CloudBlobContainer getSampleContainer()
        {
            return BlobStorageService.getCloudBlobContainer();
        }


        /// <summary>
        /// Get all samples
        /// </summary>
        /// <returns></returns>
        // GET: api/Samples
        public IEnumerable<Sample> Get()
        {
            TableQuery<SampleEntity> query = new TableQuery<SampleEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionName));
            List<SampleEntity> entityList = new List<SampleEntity>(table.ExecuteQuery(query));

            // Create a list of Sample from the list of SampleEntity with a 1:1 object relationship, filtering data as needed
            IEnumerable<Sample> sampleList = from e in entityList
                                             select new Sample()
                                             {
                                                 SampleID = e.RowKey,
                                                 Title = e.Title,
                                                 Artist = e.Artist,
                                                 CreatedDate = e.CreatedDate,                                                 
                                                 Mp3Blob = e.Mp3Blob,
                                                 SampleMp3Blob = e.SampleBlobURL,
                                                 SampleMp3URL = e.SampleMp3URL,
                                                 SampleDate = e.SampleDate

                                             };
            return sampleList;
        }

        // GET: api/Samples/5
        /// <summary>
        /// Get a sample
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [ResponseType(typeof(Sample))]
        public IHttpActionResult GetSample(string id)
        {
            // Create a retrieve operation that takes a sample entity, and takes the partitionName and Id.
            TableOperation getOperation = TableOperation.Retrieve<SampleEntity>(partitionName, id);

            // Execute the retrieve operation.
            TableResult getOperationResult = table.Execute(getOperation);

            // Construct a response including a new DTO as appropriate
            if (getOperationResult.Result == null) return NotFound();
            else
            {
                SampleEntity sampleEntity = (SampleEntity)getOperationResult.Result;
                // Create a new sample
                Sample p = new Sample()
                {
                    SampleID = sampleEntity.RowKey,
                    Title = sampleEntity.Title,
                    Artist = sampleEntity.Artist,
                    Mp3Blob = sampleEntity.Mp3Blob,
                    SampleMp3Blob = sampleEntity.SampleMp3Blob,
                    SampleDate = sampleEntity.SampleDate,
                    SampleMp3URL = sampleEntity.SampleMp3URL                    
                };

                return Ok(p);
            }
        }

        // POST: api/Samples
        /// <summary>
        /// Create a new sample
        /// </summary>
        /// <param name="sample"></param>
        /// <returns></returns>
        //[SwaggerResponse(HttpStatusCode.Created)]
        [ResponseType(typeof(Sample))]
        public IHttpActionResult PostSample(Sample sample)
        {
            // create a new sample entity
            SampleEntity sampleEntity = new SampleEntity()
            {
                RowKey = getNewMaxRowKeyValue(),
                PartitionKey = partitionName,
                Title = sample.Title,
                Artist = sample.Artist,
                CreatedDate = DateTime.Now,
                Mp3Blob = null,
                SampleMp3Blob = null,
                SampleMp3URL = null,
                SampleDate = null

            };

            // Create the TableOperation that inserts the sample entity.
            var insertOperation = TableOperation.Insert(sampleEntity);

            // Execute the insert operation.
            table.Execute(insertOperation);

            return CreatedAtRoute("DefaultApi", new { id = sampleEntity.RowKey }, sampleEntity);
        }

        // PUT: api/Samples/5
        /// <summary>
        /// Update a sample
        /// </summary>
        /// <param name="id"></param>
        /// <param name="sample"></param>
        /// <returns></returns>
        //[SwaggerResponse(HttpStatusCode.NoContent)]
        [ResponseType(typeof(void))]
        public IHttpActionResult PutSample(string id, Sample sample)
        {

            // Create a retrieve operation that takes a sample entity.
            TableOperation retrieveOperation = TableOperation.Retrieve<SampleEntity>(partitionName, id);

            // Execute the operation.
            TableResult retrievedResult = table.Execute(retrieveOperation);

            // Assign the result to a SampleEntity object.
            SampleEntity updateEntity = (SampleEntity)retrievedResult.Result;

            // if the id is not equal to the sampleId, return a BadRequest
            //if (id != updateEntity.RowKey)
            //{
            //    return BadRequest();
            //}

            // Delete any old blob
            deleteOldBlobs(updateEntity);

            // Update the old entity properties
            updateEntity.Title = sample.Title;
            updateEntity.Artist = sample.Artist;
            updateEntity.CreatedDate = sample.CreatedDate;
            updateEntity.Mp3Blob = null;
            updateEntity.SampleMp3Blob = null;            
            updateEntity.SampleMp3URL = null;
            updateEntity.SampleDate = null;


            // Create the TableOperation that inserts the sample entity.
            // The InsertOrReplace() is consistent with PUT
            var updateOperation = TableOperation.InsertOrReplace(updateEntity);

            // Execute the insert operation.
            table.Execute(updateOperation);

            // return the status code
            return Ok(updateEntity);
        }


        // Delete any old blobs
        /// <summary>
        /// Delete any old blob
        /// </summary>
        /// <param name="updateEntity"></param>
        private void deleteOldBlobs(SampleEntity updateEntity)
        {
            // if the Mp3Blob entity is not equal to null, get its reference and update the entity
            if (updateEntity.Mp3Blob != null)
            {
                // Declare an Mp3Blob and assign it to a reference from the sample container
                var Mp3blob = getSampleContainer().GetBlockBlobReference("audio/" + updateEntity.Mp3Blob);
                Mp3blob.DeleteIfExists();
                updateEntity.Mp3Blob = null;
                updateEntity.SampleMp3URL = null;
                updateEntity.SampleDate = null;
                updateEntity.SampleMp3Blob = null;

                // If the SampleMp3Blob entity is not equal to nulll, get the reference - delete if it exists
                if (updateEntity.SampleMp3Blob != null)
                {
                    var SampleMp3Blob = getSampleContainer().GetBlockBlobReference(updateEntity.SampleMp3Blob);
                    SampleMp3Blob.DeleteIfExists();
                }

            }
        }


        // DELETE: api/Samples/5
        /// <summary>
        /// Delete a sample
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [ResponseType(typeof(Sample))]
        public IHttpActionResult DeleteSample(string id)
        {
            // Create a retrieve operation that takes a sample entity.
            TableOperation retrieveOperation = TableOperation.Retrieve<SampleEntity>(partitionName, id);

            // Execute the retrieve operation.
            TableResult retrievedResult = table.Execute(retrieveOperation);
            // Check if the target is null or does exist  
            if (retrievedResult.Result == null) return NotFound();
            else
            {
                // Assign the retrieved result to a SampleEntity object 
                SampleEntity deleteEntity = (SampleEntity)retrievedResult.Result;
                
                // Delete a table operation  
                TableOperation deleteOperation = TableOperation.Delete(deleteEntity);

                // Get rid of any old blobs associated to the target entity - pass the entity as a parameter
                deleteOldBlobs(deleteEntity);

                // Execute the delete operation.
                table.Execute(deleteOperation);              

                // return ok - result
                return Ok(retrievedResult.Result);
            }
        }


        // Method getnewMaxRowKeyValue() generates the rowkey automatically
        private String getNewMaxRowKeyValue()
        {
            TableQuery<SampleEntity> query = new TableQuery<SampleEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionName));

            int maxRowKeyValue = 0;
            // Loop through the records to get the maximum RowKey value
            foreach (SampleEntity entity in table.ExecuteQuery(query))
            {
                int entityRowKeyValue = Int32.Parse(entity.RowKey);
                if (entityRowKeyValue > maxRowKeyValue) maxRowKeyValue = entityRowKeyValue;
            }
            // increment the rowkey that will be used when creating a new record
            maxRowKeyValue++;
            return maxRowKeyValue.ToString();
        }


    }
}
