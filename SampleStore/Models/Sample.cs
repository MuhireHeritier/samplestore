// This is a Data Transfer Object (DTO) class. This is sent/received in REST requests/responses.
// Read about DTOS here: https://docs.microsoft.com/en-us/aspnet/web-api/overview/data/using-web-api-with-entity-framework/part-5

using System;
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
        /// Title of the sample
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Artist of the sample
        /// </summary>
        public string Artist { get; set; }

         /// <summary>
        /// CreatedDate for sample Creation date/time of entity
        /// </summary>
        public DateTime? CreatedDate { get; set; }

        /// <summary>
        /// Mp3Blob of the sample
        /// </summary>
        public string Mp3Blob { get; set; }

        /// <summary>
        /// SampleMp3Blob of the sample
        /// </summary>
        public string SampleMp3Blob { get; set; }

        /// <summary>
        /// SampleMp3URL of the sample
        /// </summary>
        public string SampleMp3URL { get; set; }

        /// <summary>
        /// SampleDate of the sample
        /// </summary>
        public DateTime? SampleDate { get; set; }


    }
}