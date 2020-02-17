using BankApp.Models;
using System;
using System.Globalization;

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

        public static SnapshotCreated Create(Snapshot snapshot, int version)
        {
            return new SnapshotCreated()
            {
                Payload = snapshot,
                EventType = nameof(SnapshotCreated),
                Version = version,
                Timestamp = DateTime.UtcNow,
                Id = version.ToString(CultureInfo.InvariantCulture),
                PartitionKey = snapshot?.AccountNumber
            };
        }
    }
}
