using BankApp.Models;
using Newtonsoft.Json;
using System;
using System.Globalization;

namespace BankApp.Events
{
    public class MarkerCreated 
    {
        public string Id { get; set; } = "_metadata";
        public string PartitionKey { get; set; }
        public int Version { get; set; }
        public string EventType { get; set; } = nameof(MarkerCreated);
        public DateTime Timestamp { get; set; }
        public Marker Payload { get; set; }

        [JsonProperty("_etag")]
        public string Etag { get; set; }

        public static MarkerCreated Create(Marker metadata, int version, string partitionKey)
        {
            return new MarkerCreated()
            {
                Payload = metadata,
                EventType = nameof(MarkerCreated),
                Version = version,
                Timestamp = DateTime.UtcNow,
                PartitionKey = partitionKey
            };
        }

        
    }
}
