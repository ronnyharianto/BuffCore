using System.ComponentModel;
using System.Runtime.Serialization;
using BuffCore.Utilities.Attributes;
using Xunit;

namespace BuffCore.Utilities.Tests
{
    public enum TestStatus
    {
        [Description("Awaiting payment")]
        Pending,

        [Description("Shipped")]
        [EnumMember(Value = "shipped")]
        Shipped,

        Plain,
    }

    public enum TestDietary
    {
        Herbivore,
        Carnivore,
    }

    public enum TestAnimal
    {
        [MapFrom<TestDietary>(TestDietary.Herbivore)]
        Rabbit,

        [MapFrom<TestDietary>(TestDietary.Carnivore)]
        Tiger,

        Unmapped,
    }

    public class EnumHelperTests
    {
        [Fact]
        public void GetDescription_ReturnsDescriptionAttributeText()
        {
            Assert.Equal("Awaiting payment", TestStatus.Pending.GetDescription());
        }

        [Fact]
        public void GetDescription_FallsBackToEnumMemberName()
        {
            Assert.Equal("Plain", TestStatus.Plain.GetDescription());
        }

        [Theory]
        [InlineData("ship")]
        [InlineData("SHIP")]
        [InlineData("Shipped")]
        public void FilterEnumList_FiltersByDescriptionCaseInsensitively(string filterKey)
        {
            var result = EnumHelper.FilterEnumList<TestStatus>(filterKey);

            var member = Assert.Single(result);
            Assert.Equal(TestStatus.Shipped, member);
        }

        [Fact]
        public void FilterEnumList_EmptyFilterReturnsAllValues()
        {
            var result = EnumHelper.FilterEnumList<TestStatus>(null);

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void RetrieveEnumList_ReturnsAllValuesWithMetadata()
        {
            var result = EnumHelper.RetrieveEnumList<TestStatus>();

            Assert.Equal(3, result.Count);

            var shipped = result.Single(x => x.EnumValue == (int)TestStatus.Shipped);
            Assert.Equal("shipped", shipped.EnumMemberValue);
            Assert.Equal("Shipped", shipped.EnumDescription);

            var plain = result.Single(x => x.EnumValue == (int)TestStatus.Plain);
            Assert.Equal(string.Empty, plain.EnumMemberValue);
            Assert.Null(plain.EnumValueMapFrom);
        }

        [Fact]
        public void RetrieveEnumList_WithMapFrom_PopulatesMappedValue()
        {
            var result = EnumHelper.RetrieveEnumList<TestAnimal, TestDietary>();

            Assert.Equal(3, result.Count);

            var rabbit = result.Single(x => x.EnumValue == (int)TestAnimal.Rabbit);
            Assert.Equal((int)TestDietary.Herbivore, rabbit.EnumValueMapFrom);

            var tiger = result.Single(x => x.EnumValue == (int)TestAnimal.Tiger);
            Assert.Equal((int)TestDietary.Carnivore, tiger.EnumValueMapFrom);

            var unmapped = result.Single(x => x.EnumValue == (int)TestAnimal.Unmapped);
            Assert.Null(unmapped.EnumValueMapFrom);
        }
    }
}
