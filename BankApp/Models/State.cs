using BankApp.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace BankApp.Models
{
    public class State
    {
        public int Balance { get; set; }
        public AccountCreated Account { get; set; }
        public MetadataCreated Metadata { get; set; }
    }
}
