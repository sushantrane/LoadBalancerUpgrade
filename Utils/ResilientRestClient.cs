using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Polly;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Azure.Network.LoadBalancer.Utils
{
    public class ResilientRestClient
    {
        private static readonly HttpClient _client = new HttpClient();

        private static readonly Policy _circuitBreakerPolicy;
        // Number of retries
        private static readonly int _retryCount = 4;
        // Number of exceptions before opening circuit breaker
        private static readonly int _exceptionsAllowedBeforeOpeningCircuit = 4;
        // Timespan before checking circuit
        private static readonly TimeSpan _durationOfBreak = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan _httpTimeOut = TimeSpan.FromSeconds(240);
        private static readonly Policy<HttpResponseMessage> _retryPolly;


        static ResilientRestClient()
        {

            _client.Timeout = _httpTimeOut;

            HttpStatusCode[] httpStatusCodesToRetry =
            {
                HttpStatusCode.RequestTimeout, // 408
                HttpStatusCode.InternalServerError, // 500
                HttpStatusCode.BadGateway, // 502
                HttpStatusCode.ServiceUnavailable, // 503
                HttpStatusCode.GatewayTimeout
                //HttpStatusCode.NotFound // 504
            };

            // Configure Retry Policy
            _retryPolly = Policy
                // Specifiy exception types that will be handled. Be careful not to 
                // retry exception rasied by circuit breaker 
                //.Handle<Exception>(e => !(e is BrokenCircuitException))
                // Don't retry if circuit breaker has broken the circuit
                .Handle<TimeoutException>()
                .Or<HttpRequestException>()
                .OrResult<HttpResponseMessage>(x => httpStatusCodesToRetry.Contains(x.StatusCode))
                .WaitAndRetryAsync(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        //_logger.LogWarning($"Retry #{retryCount} after a {timeSpan.Seconds} second delay due to error: {exception.Exception.Message}");
                        Console.WriteLine("Retrying");
                    });

            // Configure Circuit Breaker Pattern
            _circuitBreakerPolicy = Policy.Handle<Exception>().CircuitBreakerAsync(
                _exceptionsAllowedBeforeOpeningCircuit,
                _durationOfBreak,
                (ex, breakDelay) =>
                {
                    //_logger.LogWarning($"Circuit is 'Open' for {breakDelay.TotalMilliseconds} seconds due to error: {ex.Message}");
                },
                () =>
                {
                    //_logger.LogWarning($"Call ok - closing the circuit again");
                },
                () =>
                {
                    //_logger.LogWarning($"Circuit is half-open. The next call is a trial");
                });
        }

        private static async Task<HttpResponseMessage> HttpInvoker(Func<Task<HttpResponseMessage>> operation)
        {
            return await _retryPolly.ExecuteAsync(() => _circuitBreakerPolicy.ExecuteAsync(operation));
        }

        public static async Task<TReturnMessage> GetAsync<TReturnMessage>(string path, string token) where TReturnMessage : class, new()
        {
            // Configure call
            HttpResponseMessage response;
            var result = string.Empty;
            var uri = new Uri(path);
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Execute delegatge. In the event of a retry, this block will re-execute.
            // var httpResponse = await HttpInvoker(async () =>
            // {
            //     // Here is actual call to target service              
            response = await _client.GetAsync(uri);

            if (!response.IsSuccessStatusCode)
            {
                var ex = new HttpRequestException($"{response.StatusCode} -- {response.ReasonPhrase}");
                // Stuff the Http StatusCode in the Data collection with key 'StatusCode'
                ex.Data.Add("StatusCode", response.StatusCode);
                throw ex;
            }

            result = await response.Content.ReadAsStringAsync();

            // return response;
            // });

            return JsonConvert.DeserializeObject<TReturnMessage>(result);
        }

        public static async Task<TReturnMessage> PostAsync<TReturnMessage>(string path, string token = null, object dataObject = null) where TReturnMessage : class, new()
        {
            var result = string.Empty;

            var uri = new Uri(path);
            _client.DefaultRequestHeaders.Accept.Clear();
            if (token != null)
            {
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                //_client.DefaultRequestHeaders.Add("Prefer", "v1-response=true");
            }


            var content = dataObject != null ? JsonConvert.SerializeObject(dataObject) : "{}";

            // Execute delegatge. In the event of a retry, this block will re-execute.
            // var httpResponse = await HttpInvoker(async () =>
            // {
            var response =
                await _client.PostAsync(uri, new StringContent(content, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();

            if (!response.IsSuccessStatusCode)
            {
                var ex = new HttpRequestException($"{response.StatusCode} -- {response.ReasonPhrase}");
                // Stuff the Http StatusCode in the Data collection with key 'StatusCode'
                ex.Data.Add("StatusCode", response.StatusCode);
                throw ex;
            }

            result = await response.Content.ReadAsStringAsync();
            //     return response;
            // });

            return JsonConvert.DeserializeObject<TReturnMessage>(result);
        }


        public static async Task<TReturnMessage> PatchAsync<TReturnMessage>(string path, string token = null, object dataObject = null) where TReturnMessage : class, new()
        {
            var result = string.Empty;

            var uri = new Uri(path);
            _client.DefaultRequestHeaders.Accept.Clear();
            if (token != null)
            {
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                //_client.DefaultRequestHeaders.Add("Prefer", "v1-response=true");
            }


            var content = dataObject != null ? JsonConvert.SerializeObject(dataObject) : "{}";

            // Execute delegatge. In the event of a retry, this block will re-execute.
            // var httpResponse = await HttpInvoker(async () =>
            // {
            var response =
                await _client.PatchAsync(uri, new StringContent(content, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();

            if (!response.IsSuccessStatusCode)
            {
                var ex = new HttpRequestException($"{response.StatusCode} -- {response.ReasonPhrase}");
                // Stuff the Http StatusCode in the Data collection with key 'StatusCode'
                ex.Data.Add("StatusCode", response.StatusCode);
                throw ex;
            }

            result = await response.Content.ReadAsStringAsync();
            //     return response;
            // });

            return JsonConvert.DeserializeObject<TReturnMessage>(result);
        }
        public static async Task<TReturnMessage> PutAsync<TReturnMessage>(string path, string token = null, object dataObject = null) where TReturnMessage : class, new()
        {
            var result = string.Empty;

            var uri = new Uri(path);
            _client.DefaultRequestHeaders.Accept.Clear();
            if (token != null)
            {
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                //_client.DefaultRequestHeaders.Add("Prefer", "v1-response=true");
            }
            var content = dataObject != null ? JsonConvert.SerializeObject(dataObject) : "{}";

            // Execute delegatge. In the event of a retry, this block will re-execute.
            // var httpResponse = await HttpInvoker(async () =>
            // {
                var response =
                    await _client.PutAsync(uri, new StringContent(content, Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();

                if (!response.IsSuccessStatusCode)
                {
                    var ex = new HttpRequestException($"{response.StatusCode} -- {response.ReasonPhrase}");
                    // Stuff the Http StatusCode in the Data collection with key 'StatusCode'
                    ex.Data.Add("StatusCode", response.StatusCode);
                    throw ex;
                }

                result = await response.Content.ReadAsStringAsync();
            //     return response;
            // });

            return JsonConvert.DeserializeObject<TReturnMessage>(result);
        }

        public async Task<bool> DeleteAsync(string path, string token = null)
        {
            HttpResponseMessage response = null;


            var uri = new Uri(path);
            _client.DefaultRequestHeaders.Accept.Clear();
            if (token != null)
            {
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                //_client.DefaultRequestHeaders.Add("Prefer", "v1-response=true");
            }
            //_logger.LogInformation("[INFO] DELETE Uri:" + uri);

            // Execute delegatge. In the event of a retry, this block will re-execute.
            var httpResponse = await HttpInvoker(async () =>
            {
                response = await _client.DeleteAsync(uri);
                if (!response.IsSuccessStatusCode)
                {
                    var ex = new HttpRequestException($"{response.StatusCode} -- {response.ReasonPhrase}");
                    // Stuff the Http StatusCode in the Data collection with key 'StatusCode'
                    ex.Data.Add("StatusCode", response.StatusCode);
                    throw ex;
                }
                return response;
            });

            return response.IsSuccessStatusCode;
        }
    }
}
