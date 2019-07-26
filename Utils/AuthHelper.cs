using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Azure.Network.LoadBalancer.Utils
{
    class AuthHelper
    {
        public static async Task<string> GetTokenAsync()
        {
            //Use Environment Variable
            string clientId =  Environment.GetEnvironmentVariable("DSEClientId", EnvironmentVariableTarget.Process);
            string clientSecret =  Environment.GetEnvironmentVariable("DSEClientSecret", EnvironmentVariableTarget.Process);
            string tenantId =  Environment.GetEnvironmentVariable("DSETenantId", EnvironmentVariableTarget.Process);
            ClientCredential cc = new ClientCredential(clientId, clientSecret);
            var context = new AuthenticationContext("https://login.windows.net/" + tenantId);
            var result = await context.AcquireTokenAsync("https://management.azure.com/", cc);
            if (result == null)
            {
                throw new InvalidOperationException("Failed to obtain the JWT token");
            }
            return result.AccessToken;
        }
    }
}
