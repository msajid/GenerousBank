using BankApp.Models;
using Newtonsoft.Json;
using System;
using System.Globalization;

namespace BankApp.Events
{
    public class MetadataCreated 
    {
        public string Id { get; set; } = "_metadata";
        public string PartitionKey { get; set; }
        public int Version { get; set; }
        public string EventType { get; set; } = nameof(MetadataCreated);
        public DateTime Timestamp { get; set; }
        public Metadata Payload { get; set; }

        [JsonProperty("_etag")]
        public string Etag { get; set; }

        public static MetadataCreated Create(Metadata metadata, int version)
        {
            return new MetadataCreated()
            {
                Payload = metadata,
                EventType = nameof(MetadataCreated),
                Version = version,
                Timestamp = DateTime.UtcNow,
                PartitionKey = metadata?.AccountNumber
            };
        }

        public static MetadataCreated Clone(MetadataCreated original)
        {
            return new MetadataCreated()
            {
                Id = original.Id,
                Payload = original.Payload,
                EventType = nameof(MetadataCreated),
                Version = original.Version,
                Timestamp = DateTime.UtcNow,
                PartitionKey = original.PartitionKey,
            };
        }
    }
}
