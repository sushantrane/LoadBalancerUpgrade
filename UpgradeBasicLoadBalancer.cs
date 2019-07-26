using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Azure.Network.LoadBalancer.Models;
using Azure.Network.LoadBalancer.Utils;

namespace Azure.Network.LoadBalancer
{
    public static class UpgradeBasicLoadBalancer
    {
        [FunctionName("UpgradeBasicLoadBalancer")]
        public static void Run([QueueTrigger("upgraderequests", Connection = "AzureWebJobsStorage")]UpdateLoadBalancerEntity myQueueItem, ILogger log)
        {
            log.LogInformation($"C# Queue trigger function processed: {myQueueItem.RowKey}");
            //TO DO
            //STORE THE CONFIG IN THE JSON FORMAT (EITHER BLOB OR COSMOS)
            // string runbookUri = Environment.GetEnvironmentVariable("WebHookUri", EnvironmentVariableTarget.Process);
            // dynamic response = ResilientRestClient.PostAsync<dynamic>(runbookUri, null, myQueueItem).Result;
            // log.LogInformation($"Job Id = {response.JobIds}");
        }
    }
}
