using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global


namespace Draftable.CompareAPI.Client.Internal
{
    /// <summary>
    /// Provides a simplified interface to the Draftable API's REST endpoints, using an underlying <see cref="HttpClient"/>.
    /// </summary>
    /// <remarks>
    /// Disposing a <see cref="RestApiClient"/> will dispose the underlying <see cref="HttpClient"/> and associated <see cref="HttpClientHandler"/>.
    /// </remarks>
    internal class RestApiClient : IDisposable
    {
        #region Private fields

        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        [NotNull] private readonly string _authToken;

        // Note: Apparently making multiple requests on a HttpClient should be thread safe, as it's designed to be reused.
        [NotNull] private readonly HttpClient _httpClient;

        #endregion Private fields

        #region Public constructor

        /// <summary>Builds a new <see cref="RestApiClient"/>.</summary>
        /// <param name="authToken">The token to use for authorization.</param>
        /// <param name="httpClientHandlerConfigurator">A callback that can configure the <see cref="HttpClientHandler"/> underlying this <see cref="RestApiClient"/>.</param>
        // ReSharper disable once ExceptionNotThrown
        /// <exception cref="ArgumentNullException"><paramref name="authToken"/> cannot be null.</exception>
        /// <exception cref="Exception"><paramref name="httpClientHandlerConfigurator"/> threw an exception or misconfigured the <see cref="HttpClientHandler"/>.</exception>
        public RestApiClient([NotNull] string authToken, [CanBeNull, InstantHandle] Action<HttpClientHandler> httpClientHandlerConfigurator)
        {
            _authToken = authToken ?? throw new ArgumentNullException(nameof(authToken));
            _httpClient = PrepareHTTPClient(_authToken, httpClientHandlerConfigurator);
        }

        #endregion Public constructor

        #region Private static helper methods

        #region PrepareHTTPClient

        /// <exception cref="Exception"><paramref name="httpClientHandlerConfigurator"/> threw an exception or misconfigured the <see cref="HttpClientHandler"/>.</exception>
        [Pure, NotNull]
        private HttpClient PrepareHTTPClient([NotNull] string authToken, [CanBeNull, InstantHandle] Action<HttpClientHandler> httpClientHandlerConfigurator)
        {
            var handler = new HttpClientHandler();
            HttpClient httpClient;
            try {
                httpClientHandlerConfigurator?.Invoke(handler);
                httpClient = new HttpClient(handler, disposeHandler: true);
            } catch {
                handler.Dispose();
                throw;
            }
            try {
                // ReSharper disable once PossibleNullReferenceException
                httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {authToken}");

                return httpClient;
            } catch {
                httpClient.Dispose();
                throw;
            }
        }

        #endregion PrepareHTTPClient

        #region CheckStatusIsAsExpected

        /// <exception cref="UnexpectedResponseException">The response had an unexpected status code.</exception>
        [NotNull]
        private static async Task CheckStatusIsAsExpected([NotNull] HttpResponseMessage response, HttpStatusCode expectedStatusCode)
        {
            if (response.StatusCode != expectedStatusCode) {
                var responseContent = await response.Content.AssertNotNull().ReadAsStringAsync().ConfigureAwait(false);
                throw new UnexpectedResponseException(expectedHttpStatusCode: expectedStatusCode,
                                                      responseHttpStatusCode: response.StatusCode,
                                                      responseReason: response.ReasonPhrase ?? response.StatusCode.ToString(),
                                                      responseContent: responseContent.AssertNotNull());
            }
        }

        #endregion CheckStatusIsAsExpected

        #region PrepareURI, PreparePostContent

        [Pure, NotNull]
        private static Uri PrepareURI([NotNull] string endpoint, [CanBeNull, InstantHandle] IEnumerable<KeyValuePair<string, string>> queryParameters = null)
        {
            Uri endpointUri;
            try {
                endpointUri = new Uri(endpoint, UriKind.Absolute);
            } catch (UriFormatException) {
                // TODO do something with this
                throw new NotImplementedException();
            }

            var builder = new UriBuilder(endpointUri);

            if (queryParameters != null) {
                // TODO test this
                var queryBuilder = builder.Query.Length > 0 ? new StringBuilder(builder.Query.TrimStart('?')) : new StringBuilder();
                foreach (var parameterAndValue in queryParameters) {
                    if (queryBuilder.Length > 0) {
                        queryBuilder.Append('&');
                    }
                    queryBuilder.Append(WebUtility.UrlEncode(parameterAndValue.Key));
                    queryBuilder.Append('=');
                    queryBuilder.Append(WebUtility.UrlEncode(parameterAndValue.Value));
                }
                builder.Query = queryBuilder.ToString();
            }

            return builder.Uri;
        }

        [Pure, NotNull]
        private static HttpContent PreparePostContent([CanBeNull, InstantHandle] IEnumerable<KeyValuePair<string, string>> data, [CanBeNull, InstantHandle] IEnumerable<KeyValuePair<string, Stream>> files)
        {
            var dataList = data?.Where(datum => datum.Value != null).ToList();
            var filesList = files?.ToList();

            bool anyData = dataList != null && dataList.Count > 0;
            bool anyFiles = filesList != null && filesList.Count > 0;

            if (anyFiles) {
                var content = new MultipartFormDataContent();
                if (anyData) {
                    foreach (var datum in dataList) {
                        content.Add(name: datum.Key, content: new StringContent(datum.Value));
                    }
                }
                foreach (var file in filesList) {
                    content.Add(name: file.Key, fileName: file.Key, content: new StreamContent(file.Value));
                }
                return content;
            } else if (anyData) {
                return new FormUrlEncodedContent(dataList);
            } else {
                return new ByteArrayContent(new byte[0]);
            }
        }

        #endregion PrepareURI, PreparePostContent

        #endregion Private static helper methods

        #region UnexpectedResponseException

        public class UnexpectedResponseException : Exception
        {
            public HttpStatusCode ExpectedHttpStatusCode { get; }
            public HttpStatusCode ResponseHttpStatusCode { get; }
            [NotNull] public string ResponseReason { get; }
            [NotNull] public string ResponseContent { get; }

            public UnexpectedResponseException(HttpStatusCode expectedHttpStatusCode, HttpStatusCode responseHttpStatusCode, [NotNull] string responseReason, [NotNull] string responseContent)
                : base($"Expected a response with status code `HttpStatusCode.{expectedHttpStatusCode}` but instead received status `HttpStatusCode.{responseHttpStatusCode}` (\"{responseReason}\"). Response:\n{responseContent}")
            {
                ExpectedHttpStatusCode = expectedHttpStatusCode;
                ResponseHttpStatusCode = responseHttpStatusCode;
                ResponseReason = responseReason;
                ResponseContent = responseContent;
            }

            internal UnexpectedResponseException(HttpStatusCode expectedHttpStatusCode, HttpStatusCode responseHttpStatusCode, [NotNull] string errorReason, [NotNull] HttpRequestException exception) :
                base($"Expected a response with status code `HttpStatusCode.{expectedHttpStatusCode}` but instead an error occurred:\n{errorReason}", exception)
            {
                ExpectedHttpStatusCode = expectedHttpStatusCode;
                ResponseHttpStatusCode = responseHttpStatusCode;
                ResponseReason = errorReason;
                ResponseContent = "";
            }
        }

        #endregion UnexpectedResponseException

        #region Public request methods: Get[Async], Delete[Async], Post[Async]

        #region Get, GetAsync

        /// <exception cref="UnexpectedResponseException">The response had an unexpected status code.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        [NotNull]
        public string Get([NotNull] string endpoint, [CanBeNull, InstantHandle] IEnumerable<KeyValuePair<string, string>> queryParameters = null, HttpStatusCode expectedStatusCode = HttpStatusCode.OK)
            // ReSharper disable once ExceptionNotDocumented
            => GetAsync(endpoint, queryParameters: queryParameters, expectedStatusCode: expectedStatusCode).ConfigureAwait(false).GetAwaiter().GetResult().AssertNotNull();

        /// <exception cref="UnexpectedResponseException">The response had an unexpected status code.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        [NotNull, ItemNotNull]
        public Task<string> GetAsync([NotNull] string endpoint, [CanBeNull, InstantHandle] IEnumerable<KeyValuePair<string, string>> queryParameters = null, HttpStatusCode expectedStatusCode = HttpStatusCode.OK)
            // ReSharper disable once ExceptionNotDocumented
            => GetAsync(endpoint, cancellationToken: CancellationToken.None, queryParameters: queryParameters, expectedStatusCode: expectedStatusCode);

        // ReSharper disable ExceptionNotThrown
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> has had cancellation requested.</exception>
        /// <exception cref="UnexpectedResponseException">The response had an unexpected status code.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        // ReSharper restore ExceptionNotThrown
        [NotNull, ItemNotNull]
        public async Task<string> GetAsync([NotNull] string endpoint, CancellationToken cancellationToken, [CanBeNull, InstantHandle] IEnumerable<KeyValuePair<string, string>> queryParameters = null, HttpStatusCode expectedStatusCode = HttpStatusCode.OK)
        {
            using (var response = await _httpClient.GetAsync(PrepareURI(endpoint, queryParameters), HttpCompletionOption.ResponseContentRead, cancellationToken).AssertNotNull().ConfigureAwait(false)) {
                cancellationToken.ThrowIfCancellationRequested();
                await CheckStatusIsAsExpected(response.AssertNotNull(), expectedStatusCode).ConfigureAwait(false);
                return (await response.Content.AssertNotNull().ReadAsStringAsync().ConfigureAwait(false)).AssertNotNull();
            }
        }

        #endregion Get, GetAsync

        #region Delete, DeleteAsync

        /// <exception cref="UnexpectedResponseException">The response had an unexpected status code.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        public void Delete([NotNull] string endpoint, [CanBeNull, InstantHandle] IEnumerable<KeyValuePair<string, string>> queryParameters = null, HttpStatusCode expectedStatusCode = HttpStatusCode.NoContent)
            // ReSharper disable once ExceptionNotDocumented
            => DeleteAsync(endpoint, queryParameters: queryParameters, expectedStatusCode: expectedStatusCode).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <exception cref="UnexpectedResponseException">The response had an unexpected status code.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        [NotNull]
        public Task DeleteAsync([NotNull] string endpoint, [CanBeNull, InstantHandle] IEnumerable<KeyValuePair<string, string>> queryParameters = null, HttpStatusCode expectedStatusCode = HttpStatusCode.NoContent)
            // ReSharper disable once ExceptionNotDocumented
            => DeleteAsync(endpoint, cancellationToken: CancellationToken.None, queryParameters: queryParameters, expectedStatusCode: expectedStatusCode);

        // ReSharper disable ExceptionNotThrown
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> has had cancellation requested.</exception>
        /// <exception cref="UnexpectedResponseException">The response had an unexpected status code.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        // ReSharper restore ExceptionNotThrown
        [NotNull]
        public async Task DeleteAsync([NotNull] string endpoint, CancellationToken cancellationToken, [CanBeNull, InstantHandle] IEnumerable<KeyValuePair<string, string>> queryParameters = null, HttpStatusCode expectedStatusCode = HttpStatusCode.NoContent)
        {
            using (var response = await _httpClient.DeleteAsync(PrepareURI(endpoint, queryParameters), cancellationToken).AssertNotNull().ConfigureAwait(false)) {
                cancellationToken.ThrowIfCancellationRequested();
                await CheckStatusIsAsExpected(response.AssertNotNull(), expectedStatusCode).ConfigureAwait(false);
            }
        }

        #endregion Delete, DeleteAsync

        #region Post, PostAsync

        /// <exception cref="UnexpectedResponseException">The response had an unexpected status code.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        [NotNull]
        public string Post([NotNull] string endpoint,
                           [CanBeNull, InstantHandle] IEnumerable<KeyValuePair<string, string>> queryParameters = null,
                           [CanBeNull, InstantHandle] IEnumerable<KeyValuePair<string, string>> data = null,
                           [CanBeNull, InstantHandle] IEnumerable<KeyValuePair<string, Stream>> files = null,
                           HttpStatusCode expectedStatusCode = HttpStatusCode.Created)
            // ReSharper disable once ExceptionNotDocumented
            => PostAsync(endpoint, queryParameters: queryParameters, data: data, files: files, expectedStatusCode: expectedStatusCode).ConfigureAwait(false).GetAwaiter().GetResult().AssertNotNull();

        /// <exception cref="UnexpectedResponseException">The response had an unexpected status code.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        [NotNull, ItemNotNull]
        public Task<string> PostAsync([NotNull] string endpoint,
                                      [CanBeNull, InstantHandle] IEnumerable<KeyValuePair<string, string>> queryParameters = null,
                                      [CanBeNull, InstantHandle] IEnumerable<KeyValuePair<string, string>> data = null,
                                      [CanBeNull, InstantHandle] IEnumerable<KeyValuePair<string, Stream>> files = null,
                                      HttpStatusCode expectedStatusCode = HttpStatusCode.Created)
            // ReSharper disable once ExceptionNotDocumented
            => PostAsync(endpoint, cancellationToken: CancellationToken.None, queryParameters: queryParameters, data: data, files: files, expectedStatusCode: expectedStatusCode);

        // ReSharper disable ExceptionNotThrown
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> has had cancellation requested.</exception>
        /// <exception cref="UnexpectedResponseException">The response had an unexpected status code.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        // ReSharper restore ExceptionNotThrown
        [NotNull, ItemNotNull]
        public async Task<string> PostAsync([NotNull] string endpoint,
                                            CancellationToken cancellationToken,
                                            [CanBeNull, InstantHandle] IEnumerable<KeyValuePair<string, string>> queryParameters = null,
                                            [CanBeNull, InstantHandle] IEnumerable<KeyValuePair<string, string>> data = null,
                                            [CanBeNull, InstantHandle] IEnumerable<KeyValuePair<string, Stream>> files = null,
                                            HttpStatusCode expectedStatusCode = HttpStatusCode.Created)
        {
            try {
                using (var response = await _httpClient.PostAsync(PrepareURI(endpoint, queryParameters), PreparePostContent(data, files), cancellationToken).AssertNotNull().ConfigureAwait(false)) {
                    cancellationToken.ThrowIfCancellationRequested();
                    await CheckStatusIsAsExpected(response.AssertNotNull(), expectedStatusCode).ConfigureAwait(false);
                    return (await response.Content.AssertNotNull().ReadAsStringAsync().ConfigureAwait(false)).AssertNotNull();
                }
            } catch (HttpRequestException ex) {
                // If we upload files but have bad credentials, then the server seems to terminate the connection prematurely.
                // This gives us a particular exception that we can check for, and then rethrow a more appropriate exception.
                if (files != null) {
                    if (ex.InnerException?.Message == "The underlying connection was closed: The connection was closed unexpectedly.") {
                        // ReSharper disable once ThrowFromCatchWithNoInnerException
                        throw new UnexpectedResponseException(expectedStatusCode, HttpStatusCode.Unauthorized, "Connection terminated early due to authentication failure - check that your auth token is valid.", ex);
                    }
                }
                throw;
            }
        }

        #endregion Post, PostAsync

        #endregion Public request methods: Get[Async], Delete[Async], Post[Async]

        #region Dispose

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        #endregion Dispose
    }
}
