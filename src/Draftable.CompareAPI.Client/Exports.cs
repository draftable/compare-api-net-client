using System;
using System.Collections.Generic;
using System.Net.Http;

using Draftable.CompareAPI.Client.Internal;

using JetBrains.Annotations;

using Newtonsoft.Json;


namespace Draftable.CompareAPI.Client
{
    public class Exports : IDisposable
    {
        private readonly RestApiClient _client;
        private readonly URLs _urls;

        public Exports([NotNull] string authToken,
                       [NotNull] string baseUrl,
                       [CanBeNull] [InstantHandle] Action<HttpClientHandler> httpClientHandlerConfigurator)
        {
            _urls = new URLs(baseUrl);
            _client = new RestApiClient(authToken, httpClientHandlerConfigurator);
        }

        [PublicAPI]
        [Pure]
        [NotNull]
        public Export Create(string comparisonId, string mode, bool includeCoverPage = true)
        {
            try
            {
                var response = _client.Post(_urls.Exports,
                                            data: new Dictionary<string, string>
                                            {
                                                {"comparison", comparisonId},
                                                {"kind", mode},
                                                {"include_cover_page", includeCoverPage.ToString()}
                                            });
                return DeserializeExport(response);
            }
            catch (RestApiClient.UnexpectedResponseException ex)
            {
                throw Comparisons.BadRequestException.For(ex) ?? Comparisons.InvalidCredentialsException.For(ex) ??
                    new UnknownResponseException(ex);
            }
        }

        [PublicAPI]
        [Pure]
        [NotNull]
        public Export Get(string exportId)
        {
            var resp = _client.Get(_urls.Export(exportId));
            return DeserializeExport(resp);
        }

        [Pure]
        [NotNull]
        private static Export DeserializeExport([NotNull] string jsonExport)
        {
            try
            {
                return JsonConvert.DeserializeObject<Export>(jsonExport).AssertNotNull();
            }
            catch (JsonException ex)
            {
                throw new UnknownResponseException(jsonExport, "Unable to parse the response as an export.", ex);
            }
            catch (NullReferenceException ex)
            {
                throw new UnknownResponseException(jsonExport, "Unable to parse the response as an export.", ex);
            }
        }

        public void Dispose() { _client.Dispose(); }
    }
}
