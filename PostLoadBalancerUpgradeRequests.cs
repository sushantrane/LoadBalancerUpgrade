using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Azure.Network.LoadBalancer.Models;
using Azure.Network.LoadBalancer.Utils;

namespace Azure.Network.LoadBalancer
{
    public static class PostLoadBalancerUpgradeRequests
    {
        //[FunctionName("PostLoadBalancerUpgradeRequests")]
        public static async Task Run([TimerTrigger("0 */1 * * * *")]TimerInfo myTimer, ILogger log,[Table("dselbtable")] CloudTable loadbalancerconfigTable, [Queue("upgraderequests")] ICollector<UpdateLoadBalancerEntity> myQueue)
        {
           
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            var lbs = new List<UpdateLoadBalancerEntity>();
            TableContinuationToken tabletoken = null;
            do
            {
                var queryResult = await loadbalancerconfigTable.ExecuteQuerySegmentedAsync(new TableQuery<UpdateLoadBalancerEntity>(), tabletoken);
                lbs.AddRange(queryResult.Results);
                tabletoken = queryResult.ContinuationToken;

            } while (tabletoken != null);

            // REMOVE AFTER TEST SCENARIOS
            List<string> subscriptionsIds = new List<string>
            {
                Environment.GetEnvironmentVariable("TestSubscriptionID", EnvironmentVariableTarget.Process),
            };

            List<UpdateLoadBalancerEntity> basicLbs = lbs.Where(e => (subscriptionsIds.Contains(e.PartitionKey))).ToList();
            foreach (UpdateLoadBalancerEntity lb in basicLbs)
            {
                myQueue.Add(lb);
            }
        }
    }
}
