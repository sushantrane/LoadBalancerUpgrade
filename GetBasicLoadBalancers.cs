using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Azure.Network.LoadBalancer.Utils;
using Azure.Network.LoadBalancer.Models;

namespace Azure.Network.LoadBalancer
{
    public static class GetBasicLoadBalancers
    {
       // [FunctionName("GetBasicLoadBalancers")]
        public static async Task Run([TimerTrigger("0 */1 * * * *")]TimerInfo myTimer, ILogger log, [Table("loadbalancerconfig")] CloudTable loadbalancerconfigTable)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            string token = AuthHelper.GetTokenAsync().Result;
            log.LogInformation($"Token Received: {token}");

            string subscriptionsUri = "https://management.azure.com/subscriptions?api-version=2016-06-01";
            Subscriptions subscriptions = await ResilientRestClient.GetAsync<Subscriptions>(subscriptionsUri, token);
            log.LogInformation($"Subs Received");

            string query = "where type =~ 'microsoft.network/connections'| distinct subscriptionId ";
            Dictionary<string, int> options = new Dictionary<string, int>();
            options["$skip"] = 0;
            Dictionary<string, object> requestBodyObj = new Dictionary<string, object>();
            List<string> subscriptionIds = subscriptions.value.Select(subs => subs.subscriptionId).ToList();
            requestBodyObj.Add("subscriptions", subscriptionIds);
            requestBodyObj.Add("query", query);
            requestBodyObj.Add("options", options);
            string resourceGraphUri = "https://management.azure.com/providers/Microsoft.ResourceGraph/resources?api-version=2018-09-01-preview";
            List<string> expressRouteConnectedSubscriptions = new List<string>();

            ResourceGraphResponse resourcesubs = ResilientRestClient.PostAsync<ResourceGraphResponse>(resourceGraphUri, token, requestBodyObj).Result;
            foreach (List<string> row in resourcesubs.data.rows)
            {
                expressRouteConnectedSubscriptions.Add(row[0]);
            }
            log.LogInformation($"The number of subs are {expressRouteConnectedSubscriptions.Count}");


            //NOW for Load Balancers
            string lbquery = "where type =~ 'Microsoft.Network/loadbalancers'| where tostring(sku.name) =='Basic' | project id, subscriptionId, resourceGroup, name, location";
            Dictionary<string, int> lboptions = new Dictionary<string, int>();
            options["$skip"] = 0;
            Dictionary<string, object> lbrequestBodyObj = new Dictionary<string, object>();
            //List<string> subscriptionIds = subscriptions.value.Select(subs => subs.subscriptionId).ToList();
            lbrequestBodyObj.Add("subscriptions", expressRouteConnectedSubscriptions);
            lbrequestBodyObj.Add("query", lbquery);
            lbrequestBodyObj.Add("options", lboptions);

            List<LoadBalancerObj> loadBalancers = new List<LoadBalancerObj>();

            ResourceGraphResponse lbs = ResilientRestClient.PostAsync<ResourceGraphResponse>(resourceGraphUri, token, lbrequestBodyObj).Result;
            int i = 1;
            foreach (List<string> row in lbs.data.rows)
            {
                LoadBalancerObj lb = new LoadBalancerObj
                {
                    ResourceId = row[0],
                    SubscriptionId = row[1],
                    ResourceGroup = row[2],
                    Name = row[3],
                    Location = row[4]
                };
                loadBalancers.Add(lb);
            }
            while (lbs.skipToken != null)
            {
                log.LogInformation(i.ToString());
                Dictionary<string, int> nextSkip = new Dictionary<string, int>();
                nextSkip["$skip"] = i * 1000;
                Dictionary<string, object> updatedrequestBodyObj = new Dictionary<string, object>();
                updatedrequestBodyObj["subscriptions"] = expressRouteConnectedSubscriptions;
                updatedrequestBodyObj["query"] = lbquery;
                updatedrequestBodyObj["$skipToken"] = lbs.skipToken;
                updatedrequestBodyObj["options"] = nextSkip;
                lbs = ResilientRestClient.PostAsync<ResourceGraphResponse>(resourceGraphUri, token, updatedrequestBodyObj).Result;
                foreach (List<string> row in lbs.data.rows)
                {
                    LoadBalancerObj lb = new LoadBalancerObj
                    {
                        ResourceId = row[0],
                        SubscriptionId = row[1],
                        ResourceGroup = row[2],
                        Name = row[3],
                        Location = row[4]
                    };
                    loadBalancers.Add(lb);
                }
                i++;
            }

            Parallel.ForEach(loadBalancers, async lb =>
            {
                UpdateLoadBalancerEntity loadBalancerEntity = new UpdateLoadBalancerEntity
                {
                    PartitionKey = lb.SubscriptionId,
                    RowKey = lb.Name,
                    Location = lb.Location,
                    ResourceGroup = lb.ResourceGroup,
                    ResourceId = lb.ResourceId
                };
                TableOperation insertOperation = TableOperation.InsertOrMerge(loadBalancerEntity);
                await loadbalancerconfigTable.ExecuteAsync(insertOperation);
            });
        }
    }
}
