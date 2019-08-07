// Entity class for Azure table
using Microsoft.WindowsAzure.Storage.Table;

namespace SampleStore.Models
{

    public class SampleEntity : TableEntity
    {
        public string Artist { get; set; }
        public string SampleMp3URL { get; set; }
        public string Title { get; set; }

        public SampleEntity(string partitionKey, string sampleID)
        {
            PartitionKey = partitionKey;
            RowKey = sampleID;
        }

        public SampleEntity() { }

    }
}
