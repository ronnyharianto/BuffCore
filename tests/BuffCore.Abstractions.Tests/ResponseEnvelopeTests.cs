using System.Net;
using System.Text.Json;
using BuffCore.Abstractions.Dtos;
using Xunit;

namespace BuffCore.Abstractions.Tests
{
    /// <summary>
    /// Tests for the response envelope family: BaseDto, ObjectDto, StructDto.
    /// </summary>
    public class ResponseEnvelopeTests
    {
        [Fact]
        public void BaseDto_DefaultConstructor_IsNotSuccess()
        {
            var dto = new BaseDto();

            Assert.Equal((int)HttpStatusCode.BadRequest, dto.Code);
            Assert.False(dto.Succeeded);
            Assert.False(dto.CommitTransaction);
        }

        [Fact]
        public void BaseDto_WithOkStatusCode_IsSuccess()
        {
            var dto = new BaseDto(httpStatusCode: HttpStatusCode.OK);

            Assert.Equal((int)HttpStatusCode.OK, dto.Code);
            Assert.True(dto.Succeeded);
            Assert.True(dto.CommitTransaction);
            Assert.Equal("OK", dto.Message);
        }

        [Fact]
        public void BaseDto_WithCustomFailureStatusCode_KeepsCommitTransactionFalse()
        {
            var dto = new BaseDto("not allowed", HttpStatusCode.Forbidden);

            Assert.Equal((int)HttpStatusCode.Forbidden, dto.Code);
            Assert.False(dto.Succeeded);
            Assert.False(dto.CommitTransaction);
            Assert.Equal("not allowed", dto.Message);
        }

        [Fact]
        public void BaseDto_DefaultsIdToEmptyString()
        {
            var dto = new BaseDto(httpStatusCode: HttpStatusCode.OK);

            Assert.Equal(string.Empty, dto.Id);
        }

        [Fact]
        public void ObjectDto_CarriesPayloadAndStatus()
        {
            var payload = new List<string> { "a", "b" };
            var dto = new ObjectDto<IEnumerable<string>>(httpStatusCode: HttpStatusCode.OK)
            {
                Obj = payload
            };

            Assert.True(dto.Succeeded);
            Assert.Same(payload, dto.Obj);
            Assert.IsType<BaseDto>(dto, exactMatch: false);
        }

        [Fact]
        public void ObjectDto_DefaultState_IsFailureWithNullPayload()
        {
            var dto = new ObjectDto<string>();

            Assert.Null(dto.Obj);
            Assert.False(dto.Succeeded);
        }

        [Fact]
        public void StructDto_CarriesValuePayload()
        {
            var dto = new StructDto<int>(httpStatusCode: HttpStatusCode.OK)
            {
                Obj = 42
            };

            Assert.True(dto.Succeeded);
            Assert.Equal(42, dto.Obj);
        }

        [Fact]
        public void StructDto_DefaultState_IsFailureWithDefaultValue()
        {
            var dto = new StructDto<DateTime>();

            Assert.Equal(default, dto.Obj);
            Assert.False(dto.Succeeded);
        }
    }

    /// <summary>
    /// Verifies that server-side control flags are not serialized by System.Text.Json.
    /// </summary>
    public class SerializationTests
    {
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

        [Fact]
        public void BaseDto_CommitTransaction_IsNotSerialized()
        {
            var dto = new BaseDto(httpStatusCode: HttpStatusCode.OK)
            {
                CommitTransaction = true
            };

            var json = JsonSerializer.Serialize(dto, Options);

            Assert.DoesNotContain("CommitTransaction", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ObjectDto_SerializesStandardEnvelopeFields()
        {
            var dto = new ObjectDto<string>("done", HttpStatusCode.OK) { Obj = "value", Id = "trace-1" };

            var json = JsonSerializer.Serialize(dto, Options);

            Assert.Contains("\"code\":200", json);
            Assert.Contains("\"succeeded\":true", json);
            Assert.Contains("\"message\":\"done\"", json);
            Assert.Contains("\"id\":\"trace-1\"", json);
            Assert.Contains("\"obj\":\"value\"", json);
            Assert.DoesNotContain("CommitTransaction", json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
