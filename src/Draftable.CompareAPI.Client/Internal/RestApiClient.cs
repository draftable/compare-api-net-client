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
    ///     A simplified interface to the Draftable API using an <see cref="HttpClient" />.
    /// </summary>
    /// <remarks>
    ///     Disposal of a <see cref="RestApiClient" /> will also dispose the underlying <see cref="HttpClient" /> and
    ///     associated <see cref="HttpClientHandler" />.
    /// </remarks>
    internal class RestApiClient : IDisposable
    {
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        [NotNull] private readonly string _authToken;

        // HttpClient is thread-safe and permits making multiple requests
        [NotNull] private readonly HttpClient _httpClient;

        /// <summary>
        ///     Builds a new <see cref="RestApiClient" />.
        /// </summary>
        /// <param name="authToken">
        ///     The token to use for authorization.
        /// </param>
        /// <param name="httpClientHandlerConfigurator">
        ///     A callback that can configure the <see cref="HttpClientHandler" /> underlying this <see cref="RestApiClient" />.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="authToken" /> cannot be <see langword="null" />.
        /// </exception>
        /// <exception cref="Exception">
        ///     <paramref name="httpClientHandlerConfigurator" /> threw an exception or misconfigured the
        ///     <see cref="HttpClientHandler" />.
        /// </exception>
        public RestApiClient([NotNull] string authToken,
                             [CanBeNull] [InstantHandle] Action<HttpClientHandler> httpClientHandlerConfigurator)
        {
            _authToken = authToken ?? throw new ArgumentNullException(nameof(authToken));
            _httpClient = PrepareHTTPClient(_authToken, httpClientHandlerConfigurator);
        }

        #region Public methods

        /// <exception cref="UnexpectedResponseException">
        ///     The response had an unexpected status code.
        /// </exception>
        /// <exception cref="HttpRequestException">
        ///     Unable to perform the request.
        /// </exception>
        [NotNull]
        public string Get([NotNull] string endpoint,
                          [CanBeNull] [InstantHandle] IEnumerable<KeyValuePair<string, string>> queryParameters = null,
                          HttpStatusCode expectedStatusCode = HttpStatusCode.OK)
        {
            return GetAsync(endpoint, queryParameters, expectedStatusCode)
                  .ConfigureAwait(false).GetAwaiter().GetResult()
                  .AssertNotNull();
        }

        /// <exception cref="UnexpectedResponseException">
        ///     The response had an unexpected status code.
        /// </exception>
        /// <exception cref="HttpRequestException">
        ///     Unable to perform the request.
        /// </exception>
        [NotNull]
        [ItemNotNull]
        public Task<string> GetAsync([NotNull] string endpoint,
                                     [CanBeNull] [InstantHandle]
                                     IEnumerable<KeyValuePair<string, string>> queryParameters = null,
                                     HttpStatusCode expectedStatusCode = HttpStatusCode.OK)
        {
            return GetAsync(endpoint, CancellationToken.None, queryParameters, expectedStatusCode);
        }

        /// <exception cref="OperationCanceledException">
        ///     <paramref name="cancellationToken" /> has had cancellation requested.
        /// </exception>
        /// <exception cref="UnexpectedResponseException">
        ///     The response had an unexpected status code.
        /// </exception>
        /// <exception cref="HttpRequestException">
        ///     Unable to perform the request.
        /// </exception>
        [NotNull]
        [ItemNotNull]
        public async Task<string> GetAsync([NotNull] string endpoint,
                                           CancellationToken cancellationToken,
                                           [CanBeNull] [InstantHandle]
                                           IEnumerable<KeyValuePair<string, string>> queryParameters = null,
                                           HttpStatusCode expectedStatusCode = HttpStatusCode.OK)
        {
            using (var response = await _httpClient
                                       .GetAsync(PrepareURI(endpoint, queryParameters),
                                                 HttpCompletionOption.ResponseContentRead, cancellationToken)
                                       .AssertNotNull().ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await CheckStatusIsAsExpected(response.AssertNotNull(), expectedStatusCode).ConfigureAwait(false);
                return (await response.Content.AssertNotNull().ReadAsStringAsync().ConfigureAwait(false))
                   .AssertNotNull();
            }
        }

        /// <exception cref="UnexpectedResponseException">
        ///     The response had an unexpected status code.
        /// </exception>
        /// <exception cref="HttpRequestException">
        ///     Unable to perform the request.
        /// </exception>
        public void Delete([NotNull] string endpoint,
                           [CanBeNull] [InstantHandle] IEnumerable<KeyValuePair<string, string>> queryParameters = null,
                           HttpStatusCode expectedStatusCode = HttpStatusCode.NoContent)
        {
            DeleteAsync(endpoint, queryParameters, expectedStatusCode).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <exception cref="UnexpectedResponseException">
        ///     The response had an unexpected status code.
        /// </exception>
        /// <exception cref="HttpRequestException">
        ///     Unable to perform the request.
        /// </exception>
        [NotNull]
        public Task DeleteAsync([NotNull] string endpoint,
                                [CanBeNull] [InstantHandle] IEnumerable<KeyValuePair<string, string>> queryParameters =
                                    null,
                                HttpStatusCode expectedStatusCode = HttpStatusCode.NoContent)
        {
            return DeleteAsync(endpoint, CancellationToken.None, queryParameters, expectedStatusCode);
        }

        /// <exception cref="OperationCanceledException">
        ///     <paramref name="cancellationToken" /> has had cancellation requested.
        /// </exception>
        /// <exception cref="UnexpectedResponseException">
        ///     The response had an unexpected status code.
        /// </exception>
        /// <exception cref="HttpRequestException">
        ///     Unable to perform the request.
        /// </exception>
        [NotNull]
        public async Task DeleteAsync([NotNull] string endpoint,
                                      CancellationToken cancellationToken,
                                      [CanBeNull] [InstantHandle]
                                      IEnumerable<KeyValuePair<string, string>> queryParameters = null,
                                      HttpStatusCode expectedStatusCode = HttpStatusCode.NoContent)
        {
            using (var response = await _httpClient
                                       .DeleteAsync(PrepareURI(endpoint, queryParameters), cancellationToken)
                                       .AssertNotNull().ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await CheckStatusIsAsExpected(response.AssertNotNull(), expectedStatusCode).ConfigureAwait(false);
            }
        }

        /// <exception cref="UnexpectedResponseException">
        ///     The response had an unexpected status code.
        /// </exception>
        /// <exception cref="HttpRequestException">
        ///     Unable to perform the request.
        /// </exception>
        [NotNull]
        public string Post([NotNull] string endpoint,
                           [CanBeNull] [InstantHandle] IEnumerable<KeyValuePair<string, string>> queryParameters = null,
                           [CanBeNull] [InstantHandle] IEnumerable<KeyValuePair<string, string>> data = null,
                           [CanBeNull] [InstantHandle] IEnumerable<KeyValuePair<string, Stream>> files = null,
                           HttpStatusCode expectedStatusCode = HttpStatusCode.Created)
        {
            return PostAsync(endpoint, queryParameters, data, files, expectedStatusCode).ConfigureAwait(false)
               .GetAwaiter()
               .GetResult().AssertNotNull();
        }

        /// <exception cref="UnexpectedResponseException">
        ///     The response had an unexpected status code.
        /// </exception>
        /// <exception cref="HttpRequestException">
        ///     Unable to perform the request.
        /// </exception>
        [NotNull]
        [ItemNotNull]
        public Task<string> PostAsync([NotNull] string endpoint,
                                      [CanBeNull] [InstantHandle]
                                      IEnumerable<KeyValuePair<string, string>> queryParameters = null,
                                      [CanBeNull] [InstantHandle] IEnumerable<KeyValuePair<string, string>> data = null,
                                      [CanBeNull] [InstantHandle] IEnumerable<KeyValuePair<string, Stream>> files =
                                          null,
                                      HttpStatusCode expectedStatusCode = HttpStatusCode.Created)
        {
            return PostAsync(endpoint, CancellationToken.None, queryParameters, data, files, expectedStatusCode);
        }

        /// <exception cref="OperationCanceledException">
        ///     <paramref name="cancellationToken" /> has had cancellation requested.
        /// </exception>
        /// <exception cref="UnexpectedResponseException">
        ///     The response had an unexpected status code.
        /// </exception>
        /// <exception cref="HttpRequestException">
        ///     Unable to perform the request.
        /// </exception>
        [NotNull]
        [ItemNotNull]
        public async Task<string> PostAsync([NotNull] string endpoint,
                                            CancellationToken cancellationToken,
                                            [CanBeNull] [InstantHandle]
                                            IEnumerable<KeyValuePair<string, string>> queryParameters = null,
                                            [CanBeNull] [InstantHandle] IEnumerable<KeyValuePair<string, string>> data =
                                                null,
                                            [CanBeNull] [InstantHandle]
                                            IEnumerable<KeyValuePair<string, Stream>> files = null,
                                            HttpStatusCode expectedStatusCode = HttpStatusCode.Created)
        {
            try
            {
                using (var response = await _httpClient
                                           .PostAsync(PrepareURI(endpoint, queryParameters),
                                                      PreparePostContent(data, files), cancellationToken)
                                           .AssertNotNull().ConfigureAwait(false))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await CheckStatusIsAsExpected(response.AssertNotNull(), expectedStatusCode).ConfigureAwait(false);
                    return (await response.Content.AssertNotNull().ReadAsStringAsync().ConfigureAwait(false))
                       .AssertNotNull();
                }
            }
            catch (HttpRequestException ex)
            {
                // If we upload files but have bad credentials, then the server seems to terminate the connection prematurely.
                // This gives us a particular exception that we can check for, and then rethrow a more appropriate exception.
                if (files == null)
                {
                    throw;
                }

                if (ex.InnerException?.Message ==
                    "The underlying connection was closed: The connection was closed unexpectedly.")
                {
                    throw new UnexpectedResponseException(expectedStatusCode, HttpStatusCode.Unauthorized,
                                                          "Connection terminated early due to authentication failure - check that your auth token is valid.",
                                                          ex);
                }

                throw;
            }
        }

        public void Dispose() { _httpClient.Dispose(); }

        #endregion Public methods

        #region Private methods

        /// <exception cref="UnexpectedResponseException">
        ///     The response had an unexpected status code.
        /// </exception>
        [NotNull]
        private static async Task CheckStatusIsAsExpected([NotNull] HttpResponseMessage response,
                                                          HttpStatusCode expectedStatusCode)
        {
            if (response.StatusCode != expectedStatusCode)
            {
                var responseContent = await response.Content.AssertNotNull().ReadAsStringAsync().ConfigureAwait(false);
                throw new UnexpectedResponseException(expectedStatusCode,
                                                      response.StatusCode,
                                                      response.ReasonPhrase ?? response.StatusCode.ToString(),
                                                      responseContent.AssertNotNull());
            }
        }

        /// <exception cref="Exception">
        ///     <paramref name="httpClientHandlerConfigurator" /> threw an exception or misconfigured the
        ///     <see cref="HttpClientHandler" />.
        /// </exception>
        [Pure]
        [NotNull]
        private static HttpClient PrepareHTTPClient([NotNull] string authToken,
                                                    [CanBeNull] [InstantHandle]
                                                    Action<HttpClientHandler> httpClientHandlerConfigurator)
        {
#pragma warning disable CA2000
            var handler = new HttpClientHandler(); // Disposal is performed by HttpClient
#pragma warning restore CA2000

            HttpClient httpClient;
            try
            {
                httpClientHandlerConfigurator?.Invoke(handler);
                httpClient = new HttpClient(handler, true);
            }
            catch
            {
                handler.Dispose();
                throw;
            }

            try
            {
                httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {authToken}");
            }
            catch
            {
                httpClient.Dispose();
                throw;
            }

            return httpClient;
        }

        [Pure]
        [NotNull]
        private static HttpContent PreparePostContent(
            [CanBeNull] [InstantHandle] IEnumerable<KeyValuePair<string, string>> data,
            [CanBeNull] [InstantHandle] IEnumerable<KeyValuePair<string, Stream>> files)
        {
            var dataList = data?.Where(datum => datum.Value != null).ToList();
            var filesList = files?.ToList();

            var anyData = dataList != null && dataList.Count > 0;
            var anyFiles = filesList != null && filesList.Count > 0;

            if (!anyFiles)
            {
                // Remove suppression once we target .NET Framework 4.6+
                // ReSharper disable once UseArrayEmptyMethod
                return anyData ? new FormUrlEncodedContent(dataList) : new ByteArrayContent(new byte[0]);
            }

            var content = new MultipartFormDataContent();
            if (anyData)
            {
                foreach (var datum in dataList)
                {
                    content.Add(name: datum.Key, content: new StringContent(datum.Value));
                }
            }

            foreach (var file in filesList)
            {
                content.Add(name: file.Key, fileName: file.Key, content: new StreamContent(file.Value));
            }

            return content;
        }

        [Pure]
        [NotNull]
        private static Uri PrepareURI([NotNull] string endpoint,
                                      [CanBeNull] [InstantHandle]
                                      IEnumerable<KeyValuePair<string, string>> queryParameters = null)
        {
            Uri endpointUri;
            try
            {
                endpointUri = new Uri(endpoint, UriKind.Absolute);
            }
            catch (UriFormatException)
            {
                // TODO: Do something sensible with this
                throw new NotImplementedException();
            }

            var builder = new UriBuilder(endpointUri);

            if (queryParameters == null)
            {
                return builder.Uri;
            }

            var queryBuilder = builder.Query.Length > 0
                ? new StringBuilder(builder.Query.TrimStart('?'))
                : new StringBuilder();

            foreach (var parameterAndValue in queryParameters)
            {
                if (queryBuilder.Length > 0)
                {
                    queryBuilder.Append('&');
                }

                queryBuilder.Append(WebUtility.UrlEncode(parameterAndValue.Key));
                queryBuilder.Append('=');
                queryBuilder.Append(WebUtility.UrlEncode(parameterAndValue.Value));
            }

            builder.Query = queryBuilder.ToString();

            return builder.Uri;
        }

        #endregion Private methods

        #region Exceptions

        public class UnexpectedResponseException : Exception
        {
            public HttpStatusCode ExpectedHttpStatusCode { get; }
            public HttpStatusCode ResponseHttpStatusCode { get; }
            [NotNull] public string ResponseReason { get; }
            [NotNull] public string ResponseContent { get; }

            internal UnexpectedResponseException(HttpStatusCode expectedHttpStatusCode,
                                                 HttpStatusCode responseHttpStatusCode,
                                                 [NotNull] string responseReason,
                                                 [NotNull] string responseContent)
                : base(
                    $"Expected a HTTP {expectedHttpStatusCode} response but received {responseHttpStatusCode} ({responseReason}). Response:\n{responseContent}")
            {
                ExpectedHttpStatusCode = expectedHttpStatusCode;
                ResponseHttpStatusCode = responseHttpStatusCode;
                ResponseReason = responseReason;
                ResponseContent = responseContent;
            }

            internal UnexpectedResponseException(HttpStatusCode expectedHttpStatusCode,
                                                 HttpStatusCode responseHttpStatusCode,
                                                 [NotNull] string errorReason,
                                                 [NotNull] HttpRequestException exception)
                : base(
                    $"An error occurred instead of the expected HTTP {expectedHttpStatusCode} response:\n{errorReason}",
                    exception)
            {
                ExpectedHttpStatusCode = expectedHttpStatusCode;
                ResponseHttpStatusCode = responseHttpStatusCode;
                ResponseReason = errorReason;
                ResponseContent = "";
            }
        }

        #endregion
    }
}
