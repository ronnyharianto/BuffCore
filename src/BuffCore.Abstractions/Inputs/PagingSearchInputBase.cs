using BuffCore.Abstractions.Inputs.Interfaces;

namespace BuffCore.Abstractions.Inputs
{
    /// <summary>
    /// Represents a base input model that combines pagination and search functionality.
    /// Useful for queries that require both paginated results and keyword-based filtering.
    /// </summary>
    /// <remarks>
    /// This class can be inherited by specific input models used in data retrieval endpoints.
    /// </remarks>
    public class PagingSearchInputBase : IPagingInput, ISearchInput
    {
        /// <summary>The page number to retrieve. Starts from 1.</summary>
        public int Page { get; set; }

        /// <summary>The number of items to return per page.</summary>
        public int PageSize { get; set; }

        /// <summary>The keyword used to filter or search data.</summary>
        public string? SearchKey { get; set; }
    }
}
