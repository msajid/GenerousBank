using BankApp.Models;
using System;

namespace BankApp.Events
{
    public interface ITransaction<T>
    {
        string EventType { get; set; }
        string Id { get; set; }
        string PartitionKey { get; set; }
        T Payload { get; set; }
        DateTime Timestamp { get; set; }
        int Version { get; set; }
    }
}