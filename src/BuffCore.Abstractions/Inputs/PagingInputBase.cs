using BuffCore.Abstractions.Inputs.Interfaces;

namespace BuffCore.Abstractions.Inputs
{
    /// <summary>
    /// A base input model for paginated requests.
    /// Provides standard pagination properties such as page number and page size.
    /// </summary>
    public class PagingInputBase : IPagingInput
    {
        /// <summary>The page number to retrieve. Starts from 1.</summary>
        public int Page { get; set; }

        /// <summary>The number of items to return per page.</summary>
        public int PageSize { get; set; }
    }
}
