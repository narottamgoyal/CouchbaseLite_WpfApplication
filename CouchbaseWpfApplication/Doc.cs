using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Lite;

namespace CouchbaseWpfApplication
{
    public class Doc
    {
        public string ID { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public override string ToString()
        {
            return Name;
        }
    }
}
