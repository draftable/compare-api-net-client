using JetBrains.Annotations;


namespace Draftable.CompareAPI.Client
{
    public class URLs
    {
        [NotNull] private readonly string _baseUrl;

        public URLs([NotNull] string baseURL) { _baseUrl = baseURL.EndsWith(@"/") ? baseURL.TrimEnd('/') : baseURL; }

        [NotNull] public string Comparisons => _baseUrl + "/comparisons";
        [NotNull] public string Exports => _baseUrl + "/exports";

        [NotNull]
        public string Comparison([NotNull] string identifier) { return $"{Comparisons}/{identifier}"; }

        [NotNull]
        public string Export([NotNull] string identifier) { return $"{Exports}/{identifier}"; }

        [NotNull]
        public string ComparisonViewer([NotNull] string accountId, [NotNull] string identifier)
        {
            return $"{Comparisons}/viewer/{accountId}/{identifier}";
        }
    }
}
