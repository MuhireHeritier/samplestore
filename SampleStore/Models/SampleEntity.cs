// Entity class for Azure table
using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace SampleStore.Models
{
    // class SampleEntity extends TableEntity
    public class SampleEntity : TableEntity
    {
        // Getters and setters 
        public string Title { get; set; }
        public string Artist { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string Mp3Blob { get; set; }        
        public string SampleMp3Blob { get; set; }
        public string SampleMp3URL { get; set; }
        public DateTime? SampleDate { get; set; }
        public string SampleBlobURL { get; set; }

        // Constructor - two parameters partitionKey and sampleID
        public SampleEntity(string partitionKey, string sampleID)
        {
            PartitionKey = partitionKey;
            RowKey = sampleID;
        }

        public SampleEntity() { }

    }
}
