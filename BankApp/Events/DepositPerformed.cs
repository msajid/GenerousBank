using BankApp.Models;
using System;
using System.Globalization;

namespace BankApp.Events
{
    public class DepositPerformed
    {
        public string Id { get; set; }
        public int Version { get; set; }
        public string PartitionKey { get; set; }
        public string EventType { get; set; } = nameof(DepositPerformed);
        public DateTime Timestamp { get; set; }
        public Deposit Payload { get; set; }

        public static DepositPerformed Create(Deposit deposit, int version)
        {
            return new DepositPerformed()
            {
                Payload = deposit,
                EventType = nameof(DepositPerformed),
                Version = version,
                Timestamp = DateTime.UtcNow,
                Id = version.ToString(CultureInfo.InvariantCulture),
                PartitionKey = deposit?.AccountNumber
            };
        }
    }
}
