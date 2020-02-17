using BankApp.Models;
using System;
using System.Globalization;

namespace BankApp.Events
{
    public class AccountCreated 
    {
        public string Id { get; set; }
        public string PartitionKey { get; set; }
        public int Version { get; set; }
        public string EventType { get; set; } = nameof(AccountCreated);
        public DateTime Timestamp { get; set; }
        public Account Payload { get; set; }

        public static AccountCreated Create(Account account, int version)
        {
            return new AccountCreated()
            {
                Payload = account,
                EventType = nameof(AccountCreated),
                Version = version,
                Timestamp = DateTime.UtcNow,
                Id = version.ToString(CultureInfo.InvariantCulture),
                PartitionKey = account?.AccountNumber
            };
        }
    }
}
