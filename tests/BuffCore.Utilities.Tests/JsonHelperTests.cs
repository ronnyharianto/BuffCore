using Microsoft.Extensions.Logging;
using Xunit;

namespace BuffCore.Utilities.Tests
{
    public class JsonHelperTests
    {
        private sealed class SamplePayload
        {
            public string Name { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public string? Optional { get; set; }
        }

        private sealed class Circular
        {
            public string Name { get; set; } = "root";
            public Circular? Next { get; set; }
        }

        [Fact]
        public void SerializeObject_KeepsPascalCaseAndCompactOutput()
        {
            var helper = new JsonHelper();

            var json = helper.SerializeObject(new SamplePayload { Name = "device-01", Quantity = 5 });

            Assert.Contains("\"Name\":\"device-01\"", json);
            Assert.Contains("\"Quantity\":5", json);
        }

        [Fact]
        public void SerializeObject_OmitsNullPropertiesByDefault()
        {
            var helper = new JsonHelper();

            var json = helper.SerializeObject(new SamplePayload { Name = "x", Quantity = 1, Optional = null });

            Assert.DoesNotContain("Optional", json);
        }

        [Fact]
        public void SerializeObject_IndentedProducesFormattedOutput()
        {
            var helper = new JsonHelper();

            var json = helper.SerializeObject(new SamplePayload { Name = "x", Quantity = 1 }, indented: true);

            Assert.Contains("\n", json);
        }

        [Fact]
        public void SerializeObject_HandlesCircularReferences()
        {
            var helper = new JsonHelper();
            var node = new Circular();
            node.Next = node;

            var json = helper.SerializeObject(node);

            Assert.Contains("\"Name\":\"root\"", json);
        }

        [Fact]
        public void DeserializeObject_RoundTripsPayload()
        {
            var helper = new JsonHelper();

            var payload = helper.DeserializeObject<SamplePayload>("{\"Name\":\"a\",\"Quantity\":7}");

            Assert.NotNull(payload);
            Assert.Equal("a", payload!.Name);
            Assert.Equal(7, payload.Quantity);
        }

        [Fact]
        public void DeserializeObject_CaseInsensitivePropertyNameMatching()
        {
            var helper = new JsonHelper();

            var payload = helper.DeserializeObject<SamplePayload>("{\"name\":\"a\",\"quantity\":2}");

            Assert.NotNull(payload);
            Assert.Equal("a", payload!.Name);
        }

        [Fact]
        public void DeserializeObject_ReturnsDefaultForMalformedJson()
        {
            var helper = new JsonHelper();

            Assert.Null(helper.DeserializeObject<SamplePayload>("{ not valid json"));
        }

        [Fact]
        public void DeserializeObject_ReturnsDefaultForTypeMismatch()
        {
            var helper = new JsonHelper();

            Assert.Null(helper.DeserializeObject<SamplePayload>("\"just a string\""));
        }

        [Fact]
        public void DeserializeObject_LogsWarningWhenLoggerProvided()
        {
            var logger = new FakeLogger();
            var helper = new JsonHelper(logger);

            helper.DeserializeObject<SamplePayload>("{ not valid json");

            var warning = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, warning.Level);
        }

        [Fact]
        public void DeserializeObject_DoesNotThrowWithoutLogger()
        {
            var helper = new JsonHelper();

            var result = helper.DeserializeObject<SamplePayload>("{ not valid json");

            Assert.Null(result);
        }

        private sealed class FakeLogger : ILogger
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
