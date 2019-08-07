using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Configuration;
using SampleStore.Models;

namespace SampleStore.Migrations
{
    public static class InitialiseSamples
    {
        public static void go()
        {
            const String partitionName = "Samples_Partition_1";

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["AzureWebJobsStorage"].ToString());

            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

            CloudTable table = tableClient.GetTableReference("Samples");

            // If table doesn't already exist in storage then create and populate it with some initial values, otherwise do nothing
            if (!table.Exists())
            {
                // Create table if it doesn't exist already
                table.CreateIfNotExists();

                // Create the batch operation.
                TableBatchOperation batchOperation = new TableBatchOperation();

                // Create a sample entity and add it to the table.
                SampleEntity product1 = new SampleEntity(partitionName, "1");
                product1.Artist = "Bob Marley";
                product1.Title = "Reggae";
                product1.Price = 22.31;

                // Create another sample entity and add it to the table.
                SampleEntity product2 = new SampleEntity(partitionName, "2");
                product2.Artist = "Michael Jackson";
                product2.Title = "Pop";
                product2.Price = 9.91;

                // Create another sample entity and add it to the table.
                SampleEntity product3 = new SampleEntity(partitionName, "3");
                product3.Artist = "Eminem";
                product3.Title = "HipHop";
                product3.Price = 4.99;

                // Create another sample entity and add it to the table.
                SampleEntity product4 = new SampleEntity(partitionName, "4");
                product4.Artist = "Martins";
                product4.Title = "AfroBeat";
                product4.Price = 4.99;

                // Add sample entities to the batch insert operation.
                batchOperation.Insert(product1);
                batchOperation.Insert(product2);
                batchOperation.Insert(product3);
                batchOperation.Insert(product4);

                // Execute the batch operation.
                table.ExecuteBatch(batchOperation);
            }

        }
    }
}