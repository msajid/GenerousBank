using BankApp.Models;
using System;

namespace BankApp.Events
{
    public class SnapshotCreated
    {
        public string Id { get; set; }
        public int Version { get; set; }
        public string PartitionKey { get; set; }
        public string EventType { get; set; } = nameof(SnapshotCreated);
        public DateTime Timestamp { get; set; }
        public Snapshot Payload { get; set; }
    }
}
