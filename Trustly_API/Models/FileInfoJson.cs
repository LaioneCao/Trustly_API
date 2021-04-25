using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Trustly_API.Models
{
    public class FileInfoJson
    {
        public string Extension { get; set; }
        public long Count { get; set; }
        public long Lines { get; set; } 
        public double Bytes { get; set; }
    }
}
