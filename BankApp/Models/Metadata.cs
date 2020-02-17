using System;
using System.Collections.Generic;
using System.Text;

namespace BankApp.Models
{
    public class Metada
    {
        public string AccountNumber { get; set; }
        public int LastSnapshot { get; set; }
        public int LastSequence { get; set; }

    }
}
