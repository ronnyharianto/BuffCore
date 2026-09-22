using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;

namespace BuffCore.Utilities
{
    /// <summary>
    /// Provides helper methods for making HTTP requests with JSON payloads.
    /// Register via <see cref="ServiceCollectionExtensions.AddBuffCoreUtilities"/>
    /// (or <c>AddHttpClient</c> directly) and inject where needed.
    /// </summary>
    public class HttpClientHelper(HttpClient client, ILogger<HttpClientHelper> logger, JsonHelper jsonHelper)
    {
        private readonly HttpClient _client = client;
        private readonly ILogger<HttpClientHelper> _logger = logger;
        private readonly JsonHelper _jsonHelper = jsonHelper;

        /// <summary>
        /// Sends a POST request with form URL-encoded data to the specified URL.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response content into.</typeparam>
        /// <param name="url">The endpoint URL to send the request to.</param>
        /// <param name="formUrlEncodedData">The form data as key-value pairs.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The deserialized response object, or null if deserialization fails.</returns>
        public async Task<T?> PostAsync<T>(string url, Dictionary<string, string> formUrlEncodedData, CancellationToken cancellationToken)
        {
            var urlEncodeData = string.Join(Environment.NewLine, formUrlEncodedData, cancellationToken);
            _logger.LogInformation("POST Url => {Url}, Form URL-encoded data => {FormData}", url, urlEncodeData);

            var formUrlEncodedContent = new FormUrlEncodedContent(formUrlEncodedData);

            var response = await _client.PostAsync(url, formUrlEncodedContent, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("Response Text => {responseText}", responseText);

            return _jsonHelper.DeserializeObject<T>(responseText);
        }

        /// <summary>
        /// Sends a POST request with a JSON body and optional Basic Authentication header.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request body object.</typeparam>
        /// <typeparam name="TResponse">The type to deserialize the response content into.</typeparam>
        /// <param name="url">The endpoint URL to send the request to.</param>
        /// <param name="requestBody">The object to serialize as JSON in the request body.</param>
        /// <param name="basicAuth">Optional Basic Authentication value in Base64 format.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The deserialized response object, or null if deserialization fails.</returns>
        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest requestBody, string? basicAuth, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(basicAuth))
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

            var requestBodyJson = _jsonHelper.SerializeObject(requestBody);
            _logger.LogInformation("POST Url => {Url}, Basic Auth => {BasicAuth}, Request Body JSON => {RequestBodyJson}",
                url, basicAuth, requestBodyJson);

            var content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(url, content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("Response Text => {responseText}", responseText);

            return _jsonHelper.DeserializeObject<TResponse>(responseText);
        }

        /// <summary>
        /// Sends a GET request to the specified URL with an optional Bearer token for authorization.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response content into.</typeparam>
        /// <param name="url">The endpoint URL to send the request to.</param>
        /// <param name="token">Optional Bearer token for authorization.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The deserialized response object, or null if deserialization fails.</returns>
        public async Task<T?> GetAsync<T>(string url, string? token, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(token))
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            _logger.LogInformation("GET Url => {Url}, Bearer token => {Token}", url, token);

            var response = await _client.GetAsync(url, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("Response Text => {ResponseText}", responseText);

            return _jsonHelper.DeserializeObject<T>(responseText);
        }
    }
}
