using BankApp.Models;
using Newtonsoft.Json;
using System;

namespace BankApp.Events
{
    public class MetadataCreated 
    {
        public string Id { get; set; } = "_metadata";
        public string PartitionKey { get; set; }
        public int Version { get; set; }
        public string EventType { get; set; } = nameof(MetadataCreated);
        public DateTime Timestamp { get; set; }
        public Metada Payload { get; set; }

        [JsonProperty("_etag")]
        public string Etag { get; set; }
    }
}
