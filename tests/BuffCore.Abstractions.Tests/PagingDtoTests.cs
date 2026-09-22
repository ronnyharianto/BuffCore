using BuffCore.Abstractions.Dtos;
using Xunit;

namespace BuffCore.Abstractions.Tests
{
    /// <summary>
    /// Tests for PagingDto page math on in-memory (sync) queryables — no database required.
    /// </summary>
    public class PagingDtoTests
    {
        private static IQueryable<string> BuildSource(int count)
            => Enumerable.Range(1, count).Select(i => $"item-{i}").ToList().AsQueryable();

        [Fact]
        public async Task ApplyPagination_MiddlePage_ReturnsCorrectMetadata()
        {
            var paging = new PagingDto<string>();

            await paging.ApplyPagination(page: 2, pageSize: 3, BuildSource(10), CancellationToken.None);

            Assert.Equal(2, paging.Page);
            Assert.Equal(3, paging.PageSize);
            Assert.Equal(10, paging.RecordsFiltered);
            Assert.Equal(4, paging.TotalPage);
            Assert.Equal(3, paging.PageRecordCount);
            Assert.True(paging.HasPrevious);
            Assert.True(paging.HasNext);
            Assert.Equal(["item-4", "item-5", "item-6"], paging.Obj);
        }

        [Fact]
        public async Task ApplyPagination_NonPositivePage_IsClampedToOne()
        {
            var paging = new PagingDto<string>();

            await paging.ApplyPagination(page: 0, pageSize: 5, BuildSource(10), CancellationToken.None);

            Assert.Equal(1, paging.Page);
        }

        [Fact]
        public async Task ApplyPagination_NullSource_IsEmptySuccess()
        {
            var paging = new PagingDto<string>();

            await paging.ApplyPagination(1, 10, null, CancellationToken.None);

            Assert.True(paging.Succeeded);
            Assert.Empty(paging.Obj!);
            Assert.Equal(0, paging.RecordsFiltered);
            Assert.Equal(0, paging.TotalPage);
        }

        [Fact]
        public async Task ApplyPagination_ZeroPageSize_TotalPageIsZero()
        {
            var paging = new PagingDto<string>();

            await paging.ApplyPagination(1, 0, BuildSource(10), CancellationToken.None);

            Assert.Equal(0, paging.PageSize);
            Assert.Equal(0, paging.TotalPage);
        }

        [Fact]
        public async Task ApplyPagination_LastPartialPage_CountsOnlyRemainingRecords()
        {
            var paging = new PagingDto<string>();

            await paging.ApplyPagination(4, 3, BuildSource(10), CancellationToken.None);

            Assert.Equal(4, paging.TotalPage);
            Assert.Equal(1, paging.PageRecordCount);
            Assert.False(paging.HasNext);
        }

        [Fact]
        public async Task CopyPagination_CarriesMetadataAndReplacesData()
        {
            var source = new PagingDto<string>();
            await source.ApplyPagination(3, 4, BuildSource(20), CancellationToken.None, "copied");

            var target = new PagingDto<string>();
            target.CopyPagination(source, ["mapped-1", "mapped-2"]);

            Assert.Equal(3, target.Page);
            Assert.Equal(4, target.PageSize);
            Assert.Equal(20, target.RecordsFiltered);
            Assert.Equal(5, target.TotalPage);
            Assert.Equal("copied", target.Message);
            Assert.True(target.Succeeded);
            Assert.Equal(["mapped-1", "mapped-2"], target.Obj);
        }
    }
}
