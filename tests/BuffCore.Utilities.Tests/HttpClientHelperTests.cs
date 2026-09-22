using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BuffCore.Utilities.Tests
{
    public class HttpClientHelperTests
    {
        private sealed class StubHandler(HttpStatusCode statusCode, string jsonResponse) : HttpMessageHandler
        {
            public HttpRequestMessage? LastRequest { get; private set; }
            public string? LastRequestBody { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;

                if (request.Content != null)
                    LastRequestBody = request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();

                return Task.FromResult(new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                });
            }
        }

        private sealed record EchoRequest(string Name);

        private sealed record EchoResponse(string Name, int Count);

        private static (HttpClientHelper Helper, StubHandler Handler) CreateHelper(HttpStatusCode statusCode, string jsonResponse)
        {
            var handler = new StubHandler(statusCode, jsonResponse);
            var client = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
            var helper = new HttpClientHelper(
                client,
                NullLogger<HttpClientHelper>.Instance,
                new JsonHelper());
            return (helper, handler);
        }

        [Fact]
        public async Task PostAsync_DeserializesJsonResponse()
        {
            var (helper, _) = CreateHelper(HttpStatusCode.OK, "{\"Name\":\"ok\",\"Count\":3}");

            var result = await helper.PostAsync<object, EchoResponse>("echo", new { Payload = 1 }, null, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("ok", result!.Name);
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task PostAsync_SendsJsonRequestBody()
        {
            var (helper, handler) = CreateHelper(HttpStatusCode.OK, "{}");

            await helper.PostAsync<EchoRequest, EchoResponse>("echo", new EchoRequest("body"), null, CancellationToken.None);

            Assert.NotNull(handler.LastRequestBody);
            Assert.Contains("\"Name\":\"body\"", handler.LastRequestBody);
        }

        [Fact]
        public async Task PostAsync_WithBasicAuthSetsAuthorizationHeader()
        {
            var (helper, handler) = CreateHelper(HttpStatusCode.OK, "{}");

            await helper.PostAsync<EchoRequest, EchoResponse>("echo", new EchoRequest("x"), Convert.ToBase64String("u:p"u8), CancellationToken.None);

            Assert.Equal("Basic", handler.LastRequest!.Headers.Authorization!.Scheme);
        }

        [Fact]
        public async Task PostAsync_SendsFormUrlEncodedContentType()
        {
            var (helper, handler) = CreateHelper(HttpStatusCode.OK, "{}");

            await helper.PostAsync<EchoResponse>("echo", new Dictionary<string, string> { ["grant_type"] = "client_credentials" }, CancellationToken.None);

            Assert.NotNull(handler.LastRequest!.Content);
            Assert.Contains("application/x-www-form-urlencoded", handler.LastRequest.Content!.Headers.ContentType!.ToString());
        }

        [Fact]
        public async Task GetAsync_WithoutTokenSendsNoAuthorizationHeader()
        {
            var (helper, handler) = CreateHelper(HttpStatusCode.OK, "{\"Name\":\"ok\",\"Count\":1}");

            var result = await helper.GetAsync<EchoResponse>("items", null, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("ok", result!.Name);
            Assert.Null(handler.LastRequest!.Headers.Authorization);
        }

        [Fact]
        public async Task GetAsync_WithTokenSetsBearerHeader()
        {
            var (helper, handler) = CreateHelper(HttpStatusCode.OK, "{}");

            await helper.GetAsync<EchoResponse>("items", "token-123", CancellationToken.None);

            Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
            Assert.Equal("token-123", handler.LastRequest.Headers.Authorization!.Parameter);
        }

        [Fact]
        public async Task GetAsync_MalformedJsonYieldsNullWithoutThrowing()
        {
            var (helper, _) = CreateHelper(HttpStatusCode.OK, "{ broken");

            var result = await helper.GetAsync<EchoResponse>("items", null, CancellationToken.None);

            Assert.Null(result);
        }
    }
}
