using BuffCore.Abstractions;
using BuffCore.Abstractions.Inputs;
using BuffCore.Abstractions.Inputs.Interfaces;
using Xunit;

namespace BuffCore.Abstractions.Tests
{
    /// <summary>
    /// Tests for the paging/search input base family.
    /// </summary>
    public class InputBaseTests
    {
        [Fact]
        public void PagingInputBase_ImplementsIPagingInput()
        {
            var input = new PagingInputBase { Page = 2, PageSize = 25 };

            Assert.IsType<IPagingInput>(input, exactMatch: false);
            Assert.Equal(2, input.Page);
            Assert.Equal(25, input.PageSize);
        }

        [Fact]
        public void SearchInputBase_ImplementsISearchInput()
        {
            var input = new SearchInputBase { SearchKey = "abc" };

            Assert.IsType<ISearchInput>(input, exactMatch: false);
            Assert.Equal("abc", input.SearchKey);
        }

        [Fact]
        public void PagingSearchInputBase_ImplementsBothInterfaces()
        {
            var input = new PagingSearchInputBase { Page = 1, PageSize = 10, SearchKey = null };

            Assert.IsType<IPagingInput>(input, exactMatch: false);
            Assert.IsType<ISearchInput>(input, exactMatch: false);
            Assert.Null(input.SearchKey);
        }

        [Fact]
        public void IPagingInput_Page_HasRangeValidation()
        {
            var property = typeof(IPagingInput).GetProperty(nameof(IPagingInput.Page));
            var attribute = property!.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RangeAttribute), false)
                .Cast<System.ComponentModel.DataAnnotations.RangeAttribute>()
                .SingleOrDefault();

            Assert.NotNull(attribute);
            Assert.Equal(1, attribute!.Minimum);
        }

        [Fact]
        public void CurrentUserAccessor_DefaultsAreEmpty()
        {
            var accessor = new CurrentUserAccessor();

            Assert.Equal(Guid.Empty, accessor.UserId);
            Assert.Equal(Guid.Empty, accessor.CompanyId);
            Assert.Equal(string.Empty, accessor.FullName);
            Assert.Equal(string.Empty, accessor.EmailAddress);
            Assert.Null(accessor.Permissions);
        }
    }
}
