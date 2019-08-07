// Entity class for Azure table
using Microsoft.WindowsAzure.Storage.Table;

namespace SampleStore.Models
{

    public class SampleEntity : TableEntity
    {
        public string Artist { get; set; }
        public double Price { get; set; }
        public string Title { get; set; }

        public SampleEntity(string partitionKey, string productID)
        {
            PartitionKey = partitionKey;
            RowKey = productID;
        }

        public SampleEntity() { }

    }
}
