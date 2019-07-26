using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Network.LoadBalancer.Models
{
    public class Column
    {
        public string name { get; set; }
        public string type { get; set; }
    }

    public class Data
    {
        public List<Column> columns { get; set; }
        public List<List<string>> rows { get; set; }
    }

    public class ResourceGraphResponse
    {
        public int totalRecords { get; set; }
        public int count { get; set; }
        public Data data { get; set; }
        public List<object> facets { get; set; }
        public string resultTruncated { get; set; }
        [JsonProperty("$skipToken")]
        public string skipToken { get; set; }
    }
}