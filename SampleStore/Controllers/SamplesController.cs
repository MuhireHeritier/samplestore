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
    public class SamplesController : ApiController
    {
        private const String partitionName = "Samples_Partition_1";

        private CloudStorageAccount storageAccount;
        private CloudTableClient tableClient;
        private CloudTable table;

        private BlobStorageService blobStorageService = new BlobStorageService();
        private CloudQueueService queueStorageService = new CloudQueueService();

        public SamplesController()
        {
            storageAccount = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["AzureWebJobsStorage"].ToString());
            tableClient = storageAccount.CreateCloudTableClient();
            table = tableClient.GetTableReference("Samples");
        }

        private CloudBlobContainer getMusicContainer()
        {
            return blobStorageService.getCloudBlobContainer();
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

            // Basically create a list of Sample from the list of SampleEntity with a 1:1 object relationship, filtering data as needed
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
            // Create a retrieve operation that takes a sample entity.
            TableOperation getOperation = TableOperation.Retrieve<SampleEntity>(partitionName, id);

            // Execute the retrieve operation.
            TableResult getOperationResult = table.Execute(getOperation);

            // Construct response including a new DTO as appropriate
            if (getOperationResult.Result == null) return NotFound();
            else
            {
                SampleEntity sampleEntity = (SampleEntity)getOperationResult.Result;
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
            if (id != sample.SampleID)
            {
                return BadRequest();
            }

            // Create a retrieve operation that takes a sample entity.
            TableOperation retrieveOperation = TableOperation.Retrieve<SampleEntity>(partitionName, id);

            // Execute the operation.
            TableResult retrievedResult = table.Execute(retrieveOperation);

            // Assign the result to a SampleEntity object.
            SampleEntity updateEntity = (SampleEntity)retrievedResult.Result;

            // get rid of ANY OLD BLOBs
            deleteOldBlobs(updateEntity);

            // Updating the OLD ENTITY
            updateEntity.Title = sample.Title;
            updateEntity.Artist = sample.Artist;
            updateEntity.CreatedDate = sample.CreatedDate;
            updateEntity.Mp3Blob = null;
            updateEntity.SampleMp3Blob = null;            
            updateEntity.SampleMp3URL = null;
            updateEntity.SampleDate = null;


            // Create the TableOperation that inserts the sample entity.
            // Note semantics of InsertOrReplace() which are consistent with PUT
            var updateOperation = TableOperation.InsertOrReplace(updateEntity);

            // Execute the insert operation.
            table.Execute(updateOperation);

            return StatusCode(HttpStatusCode.NoContent);
        }


        // DELETE OLD BLOBS
        /// <summary>
        /// DELETE OLD BLOB
        /// </summary>
        /// <param name="updateEntity"></param>
        private void deleteOldBlobs(SampleEntity updateEntity)
        {
            if (updateEntity.Mp3Blob != null)
            {
                
                var Mp3blob = getMusicContainer().GetBlockBlobReference("audio/" + updateEntity.Mp3Blob);
                Mp3blob.DeleteIfExists();
                updateEntity.Mp3Blob = null;
                updateEntity.SampleMp3URL = null;
                updateEntity.SampleDate = null;
                updateEntity.SampleMp3Blob = null;

                if (updateEntity.SampleMp3Blob != null)
                {
                    var SampleMp3Blob = getMusicContainer().GetBlockBlobReference(updateEntity.SampleMp3Blob);
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
            // check if the TARGET ENTITY EXIST 
            if (retrievedResult.Result == null) return NotFound();
            else
            {
                // ASSIGN RETRIEVED RESULT to a SAMPLE ENTITY
                SampleEntity deleteEntity = (SampleEntity)retrievedResult.Result;
                
                // DELETE OPERATION 
                TableOperation deleteOperation = TableOperation.Delete(deleteEntity);

                // DELETE OLD BLOBs ASSOCIATED TO THE TARGET ENTITY
                deleteOldBlobs(deleteEntity);

                // Execute the operation.
                table.Execute(deleteOperation);

              

                return Ok(retrievedResult.Result);
            }
        }


        
        private String getNewMaxRowKeyValue()
        {
            TableQuery<SampleEntity> query = new TableQuery<SampleEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionName));

            int maxRowKeyValue = 0;
            foreach (SampleEntity entity in table.ExecuteQuery(query))
            {
                int entityRowKeyValue = Int32.Parse(entity.RowKey);
                if (entityRowKeyValue > maxRowKeyValue) maxRowKeyValue = entityRowKeyValue;
            }
            maxRowKeyValue++;
            return maxRowKeyValue.ToString();
        }


    }
}
