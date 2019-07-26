using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Network.LoadBalancer.Models
{
    public class UpdateLoadBalancerEntity : TableEntity
    {
        public UpdateLoadBalancerEntity(string subscriptionId, string lbname)
        {
            this.PartitionKey = subscriptionId;
            this.RowKey = lbname;
        }

        public UpdateLoadBalancerEntity() {}
        public string ResourceGroup { get; set; }
        public string Location { get; set; }
        public string ResourceId { get; set; }
    }
}