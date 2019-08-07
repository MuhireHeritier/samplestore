// This is a Data Transfer Object (DTO) class. This is sent/received in REST requests/responses.
// Read about DTOS here: https://docs.microsoft.com/en-us/aspnet/web-api/overview/data/using-web-api-with-entity-framework/part-5

using System.ComponentModel.DataAnnotations;

namespace SampleStore.Models
{
    public class Sample
    {
        /// <summary>
        /// Sample ID
        /// </summary>
        [Key]
        public string SampleID { get; set; }

        /// <summary>
        /// Artist of the product
        /// </summary>
        public string Artist { get; set; }

        /// <summary>
        /// SampleMp3URL of the product
        /// </summary>
        public string SampleMp3URL { get; set; }

        /// <summary>
        /// Title of the product
        /// </summary>
        public string Title { get; set; }
    }
}