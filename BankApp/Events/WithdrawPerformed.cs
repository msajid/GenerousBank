using BankApp.Models;
using System;
using System.Globalization;

namespace BankApp.Events
{
    public class WithdrawPerformed
    {
        public string Id { get; set; }
        public int Version { get; set; }
        public string PartitionKey { get; set; }
        public string EventType { get; set; } = nameof(WithdrawPerformed);
        public DateTime Timestamp { get; set; }
        public Withdraw Payload { get; set; }

        public static WithdrawPerformed Create(Withdraw withdraw, int version)
        {
            return new WithdrawPerformed()
            {
                Payload = withdraw,
                EventType = nameof(WithdrawPerformed),
                Version = version,
                Timestamp = DateTime.UtcNow,
                Id = version.ToString(CultureInfo.InvariantCulture),
                PartitionKey = withdraw?.AccountNumber

            };
        }
    }
}
