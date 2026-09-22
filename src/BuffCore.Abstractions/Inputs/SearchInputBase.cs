using BuffCore.Abstractions.Inputs.Interfaces;

namespace BuffCore.Abstractions.Inputs
{
    /// <summary>
    /// Base model for inputs that support keyword-based search.
    /// </summary>
    public class SearchInputBase : ISearchInput
    {
        /// <summary>The keyword used to filter or search data.</summary>
        public string? SearchKey { get; set; }
    }
}
