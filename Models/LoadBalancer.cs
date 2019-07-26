using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Azure.Network.LoadBalancer.Models
{
    public class LoadBalancerObj
    {

        public string SubscriptionId { get; set; }
        public string Name { get; set; }
        public string ResourceGroup { get; set; }
        public string Location { get; set; }
        public string ResourceId {get;set;}
    }
}