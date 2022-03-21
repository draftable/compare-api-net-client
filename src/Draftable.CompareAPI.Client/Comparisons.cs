using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Draftable.CompareAPI.Client.Internal;

using JetBrains.Annotations;

using Newtonsoft.Json;


// ReSharper disable InconsistentNaming
// ReSharper disable once IntroduceOptionalParameters.Global
// ReSharper disable MethodOverloadWithOptionalParameter
// ReSharper disable SuggestBaseTypeForParameter
// ReSharper disable ExceptionNotThrown
// ReSharper disable ExceptionNotDocumented

namespace Draftable.CompareAPI.Client
{
    /// <summary>
    ///     An API client for creating and managing comparisons, via "comparisons" REST endpoint in the Draftable Comparison
    ///     API.
    /// </summary>
    /// <remarks>
    ///     Disposing the client will interrupt all ongoing communication and close any open connections by disposing the
    ///     underlying <see cref="HttpClient" /> and <see cref="HttpClientHandler" />.
    /// </remarks>
    [PublicAPI]
    public class Comparisons : IDisposable
    {
        #region Public interface

        #region Read-only properties

        /// <summary>
        ///     The unique identifier for the API user's testing or live account.
        /// </summary>
        [PublicAPI]
        [NotNull]
        public string AccountId { get; }

        /// <summary>
        ///     The private authorization token associated with the API user's testing or live account.
        /// </summary>
        [PublicAPI]
        [NotNull]
        public string AuthToken { get; }

        #endregion Read-only properties

        #region Constructors

        /// <summary>
        ///     Construct a new <see cref="Comparisons" /> API client for the given credentials, connecting to Draftable cloud
        ///     compare service.
        /// </summary>
        /// <param name="accountId">
        ///     The unique identifier for the API user's testing or live account.
        /// </param>
        /// <param name="authToken">
        ///     The corresponding private authorization token, associated with the API user's testing or live
        ///     account.
        /// </param>
        [PublicAPI]
        public Comparisons([NotNull] string accountId, [NotNull] string authToken)
            // ReSharper disable once ExceptionNotDocumented
            : this(accountId, authToken, KnownURLs.CloudBaseURL, null) { }

        /// <summary>
        ///     Construct a new <see cref="Comparisons" /> API client for the given credentials, connecting to Draftable instance
        ///     pointed to by the base URL.
        /// </summary>
        /// <param name="accountId">The unique identifier for the API user's testing or live account.</param>
        /// <param name="authToken">
        ///     The corresponding private authorization token, associated with the API user's testing or live
        ///     account.
        /// </param>
        /// <param name="baseURL">Base API URL</param>
        /// ///
        /// <remarks>
        ///     Use this overload if you need to connect to non-cloud instance of Draftable (for example, the local self-hosted).
        /// </remarks>
        [PublicAPI]
        public Comparisons([NotNull] string accountId, [NotNull] string authToken, [NotNull] string baseURL)
            // ReSharper disable once ExceptionNotDocumented
            : this(accountId, authToken, baseURL, null) { }

        /// <summary>
        ///     Construct a new <see cref="Comparisons" /> API client for the given credentials,  connecting to Draftable instance
        ///     pointed to by the base URL, with custom configuration for the underlying <see cref="HttpClientHandler" />.
        /// </summary>
        /// <param name="accountId">The unique identifier for the API user's testing or live account.</param>
        /// <param name="authToken">
        ///     The corresponding private authorization token, associated with the API user's testing or live
        ///     account.
        /// </param>
        /// <param name="baseURL">Base API URL</param>
        /// <param name="httpClientHandlerConfigurator">
        ///     A callback that will be immediately invoked to configure the
        ///     <see cref="HttpClientHandler" /> that will be used to make requests in the underlying <see cref="HttpClient" />.
        /// </param>
        /// <remarks>
        ///     Use this overload if you need to configure the <see cref="HttpClientHandler" /> to e.g. use a proxy server.
        /// </remarks>
        /// <exception cref="Exception">
        ///     The given <paramref name="httpClientHandlerConfigurator" /> callback threw an exception or
        ///     misconfigured the <see cref="HttpClientHandler" />.
        /// </exception>
        [PublicAPI]
        public Comparisons([NotNull] string accountId,
                           [NotNull] string authToken,
                           [NotNull] string baseURL,
                           [CanBeNull] [InstantHandle] Action<HttpClientHandler> httpClientHandlerConfigurator)
        {
            _urls = new URLs(baseURL);
            AccountId = accountId ?? throw new ArgumentNullException(nameof(accountId));
            AuthToken = authToken ?? throw new ArgumentNullException(nameof(authToken));

            _client = new RestApiClient(AuthToken, httpClientHandlerConfigurator);
        }

        #endregion Constructors

        #region Exceptions

        /// <summary>
        ///     Base class for exceptions raised by <see cref="Comparisons" /> following API requests.
        /// </summary>
        [PublicAPI]
        public abstract class RequestExceptionBase : Exception
        {
            protected RequestExceptionBase([NotNull] string message, [CanBeNull] Exception innerException) : base(
                message, innerException) { }

            protected RequestExceptionBase([NotNull] string message) : base(message) { }
        }


        /// <summary>
        ///     Thrown for HTTP 400 ("bad request"). Indicates that one or more parameters were invalid.
        /// </summary>
        [PublicAPI]
        public class BadRequestException : RequestExceptionBase
        {
            /// <summary>
            ///     The content of the response, which will include the validation errors.
            /// </summary>
            [PublicAPI]
            [NotNull]
            public string ResponseContent { get; }

            private BadRequestException([NotNull] RestApiClient.UnexpectedResponseException ex) : base(
                MessageFor(ex), ex)
            {
                Debug.Assert(ex.ResponseHttpStatusCode == HttpStatusCode.BadRequest);
                ResponseContent = ex.ResponseContent;
            }

            [Pure]
            [NotNull]
            private static string MessageFor([NotNull] RestApiClient.UnexpectedResponseException ex)
            {
                if (string.IsNullOrEmpty(ex.ResponseContent))
                {
                    return "Bad request - ensure that the parameters are valid.";
                }

                string errorDetails;
                try
                {
                    errorDetails =
                        JsonConvert.SerializeObject(JsonConvert.DeserializeObject(ex.ResponseContent),
                                                    Formatting.Indented);
                }
                catch
                {
                    errorDetails = ex.ResponseContent;
                }

                return $"Bad request - invalid parameters were provided. Details:\n{errorDetails}";
            }

            [Pure]
            [CanBeNull]
            internal static RequestExceptionBase For([NotNull] RestApiClient.UnexpectedResponseException ex)
            {
                return ex.ResponseHttpStatusCode == HttpStatusCode.BadRequest ? new BadRequestException(ex) : null;
            }
        }


        /// <summary>
        ///     Thrown for HTTP 401 ("unauthorized") and HTTP 403 ("forbidden"). Indicates that the credentials you provided are
        ///     invalid.
        /// </summary>
        [PublicAPI]
        public class InvalidCredentialsException : RequestExceptionBase
        {
            private InvalidCredentialsException([NotNull] RestApiClient.UnexpectedResponseException ex) :
                base(MessageFor(ex), ex)
            {
                Debug.Assert(ex.ResponseHttpStatusCode == HttpStatusCode.Forbidden ||
                             ex.ResponseHttpStatusCode == HttpStatusCode.Unauthorized);
            }

            [Pure]
            [NotNull]
            private static string MessageFor([NotNull] RestApiClient.UnexpectedResponseException ex)
            {
                if (string.IsNullOrEmpty(ex.ResponseContent))
                {
                    return
                        "Invalid authorization credentials. Please check your account ID and auth token were provided correctly.";
                }

                string errorDetails;
                try
                {
                    errorDetails =
                        JsonConvert.SerializeObject(JsonConvert.DeserializeObject(ex.ResponseContent),
                                                    Formatting.Indented);
                }
                catch
                {
                    errorDetails = ex.ResponseContent;
                }

                return $"Invalid authorization credentials. Details:\n{errorDetails}";
            }

            [Pure]
            [CanBeNull]
            internal static RequestExceptionBase For([NotNull] RestApiClient.UnexpectedResponseException ex)
            {
                return ex.ResponseHttpStatusCode == HttpStatusCode.Forbidden ||
                       ex.ResponseHttpStatusCode == HttpStatusCode.Unauthorized
                    ? new InvalidCredentialsException(ex)
                    : null;
            }
        }


        /// <summary>
        ///     Thrown for HTTP 404 ("not found"). Indicates that no comparison with the given identifier exists.
        /// </summary>
        [PublicAPI]
        public class NotFoundException : RequestExceptionBase
        {
            private NotFoundException([NotNull] RestApiClient.UnexpectedResponseException ex) :
                base("Comparison not found.", ex)
            {
                Debug.Assert(ex.ResponseHttpStatusCode == HttpStatusCode.NotFound);
            }

            [Pure]
            [CanBeNull]
            internal static RequestExceptionBase For([NotNull] RestApiClient.UnexpectedResponseException ex)
            {
                return ex.ResponseHttpStatusCode == HttpStatusCode.NotFound ? new NotFoundException(ex) : null;
            }
        }


        /// <summary>
        ///     Thrown when the response received from the server is unrecognized. This should not occur in regular use of the API,
        ///     but is possible if for example you have a misconfigured proxy server.
        /// </summary>
        /// <remarks>
        ///     If requests made to the API are not being intercepted, an <see cref="UnknownResponseException" /> indicates an
        ///     error in this client library. Please contact support@draftable.com if this is the case.
        /// </remarks>
        [PublicAPI]
        public class UnknownResponseException : RequestExceptionBase
        {
            /// <summary>
            ///     The content of the response.
            /// </summary>
            [PublicAPI]
            [NotNull]
            public string ResponseContent { get; }

            internal UnknownResponseException([NotNull] RestApiClient.UnexpectedResponseException ex)
                : base(
                    "An unknown response was received. Contact support@draftable.com for assistance, or open an issue on GitHub.",
                    ex)
            {
                ResponseContent = ex.ResponseContent;
            }

            internal UnknownResponseException([NotNull] string responseContent,
                                              [NotNull] string message,
                                              [NotNull] Exception ex)
                : base(
                    $"{message}\nA deserialization error indicates an issue in this client library, or the comparison API. Contact support@draftable.com for assistance, or open an issue on GitHub.",
                    ex)
            {
                ResponseContent = responseContent;
            }
        }

        #endregion Exceptions

        #region Get[Async], GetAll[Async]

        /// <summary>
        ///     Retrieves information for the comparison with the given <paramref name="identifier" />.
        /// </summary>
        /// <param name="identifier">The unique identifier of the comparison.</param>
        /// <returns>A <see cref="Comparison" /> object giving metadata for the comparison.</returns>
        /// <exception cref="NotFoundException">No comparison with the given <paramref name="identifier" /> exists.</exception>
        /// <exception cref="InvalidCredentialsException">You have provided invalid credentials.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        [PublicAPI]
        [Pure]
        [NotNull]
        public Comparison Get([NotNull] string identifier)
        {
            ValidateIdentifier(identifier ?? throw new ArgumentNullException(nameof(identifier)));
            try
            {
                return DeserializeComparison(_client.Get(_urls.Comparison(identifier)));
            }
            catch (RestApiClient.UnexpectedResponseException ex)
            {
                throw NotFoundException.For(ex) ??
                      InvalidCredentialsException.For(ex) ?? new UnknownResponseException(ex);
            }
        }

        /// <summary>
        ///     Retrieves information for the comparison with the given <paramref name="identifier" />.
        /// </summary>
        /// <param name="identifier">The unique identifier of the comparison.</param>
        /// <returns>A <see cref="Comparison" /> object giving metadata for the comparison.</returns>
        /// <exception cref="NotFoundException">No comparison with the given <paramref name="identifier" /> exists.</exception>
        /// <exception cref="InvalidCredentialsException">You have provided invalid credentials.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        [PublicAPI]
        [Pure]
        [NotNull]
        [ItemNotNull]
        public Task<Comparison> GetAsync([NotNull] string identifier)
        {
            return GetAsync(identifier, CancellationToken.None);
        }

        /// <summary>
        ///     Retrieves information for the comparison with the given <paramref name="identifier" />.
        /// </summary>
        /// <param name="identifier">The unique identifier of the comparison.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> for cancelling the operation.</param>
        /// <returns>A <see cref="Comparison" /> object giving metadata for the comparison.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> had cancellation requested.</exception>
        /// <exception cref="NotFoundException">No comparison with the given <paramref name="identifier" /> exists.</exception>
        /// <exception cref="InvalidCredentialsException">You have provided invalid credentials.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        [PublicAPI]
        [Pure]
        [NotNull]
        [ItemNotNull]
        public async Task<Comparison> GetAsync([NotNull] string identifier, CancellationToken cancellationToken)
        {
            ValidateIdentifier(identifier ?? throw new ArgumentNullException(nameof(identifier)));
            try
            {
                return DeserializeComparison(await _client.GetAsync(_urls.Comparison(identifier), cancellationToken)
                                                          .ConfigureAwait(false));
            }
            catch (RestApiClient.UnexpectedResponseException ex)
            {
                throw NotFoundException.For(ex) ??
                      InvalidCredentialsException.For(ex) ?? new UnknownResponseException(ex);
            }
        }

        /// <summary>
        ///     Retrieves information about all of your account's comparisons.
        /// </summary>
        /// <remarks>
        ///     Be warned that this can be an expensive operation. It is preferred to work with individually identified
        ///     comparisons.
        /// </remarks>
        /// <returns>A list of <see cref="Comparison" /> objects giving metadata for all of your account's comparisons.</returns>
        /// <exception cref="InvalidCredentialsException">You have provided invalid credentials.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        [PublicAPI]
        [Pure]
        [NotNull]
        [ItemNotNull]
        public List<Comparison> GetAll()
        {
            try
            {
                return DeserializeAllComparisons(_client.Get(_urls.Comparisons));
            }
            catch (RestApiClient.UnexpectedResponseException ex)
            {
                throw InvalidCredentialsException.For(ex) ?? new UnknownResponseException(ex);
            }
        }

        /// <summary>
        ///     Retrieves information about all of your account's comparisons.
        /// </summary>
        /// <remarks>
        ///     Be warned that this can be an expensive operation. It is preferred to work with individually identified
        ///     comparisons.
        /// </remarks>
        /// <returns>A list of <see cref="Comparison" /> objects giving metadata for all of your account's comparisons.</returns>
        /// <exception cref="InvalidCredentialsException">You have provided invalid credentials.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        [PublicAPI]
        [Pure]
        [NotNull]
        [ItemNotNull]
        public Task<List<Comparison>> GetAllAsync() { return GetAllAsync(CancellationToken.None); }

        /// <summary>
        ///     Retrieves information about all of your account's comparisons.
        /// </summary>
        /// <remarks>
        ///     Be warned that this can be an expensive operation. It is preferred to work with individually identified
        ///     comparisons.
        /// </remarks>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> for cancelling the operation.</param>
        /// <returns>A list of <see cref="Comparison" /> objects giving metadata for all of your account's comparisons.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> had cancellation requested.</exception>
        /// <exception cref="InvalidCredentialsException">You have provided invalid credentials.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        [PublicAPI]
        [Pure]
        [NotNull]
        [ItemNotNull]
        public async Task<List<Comparison>> GetAllAsync(CancellationToken cancellationToken)
        {
            try
            {
                return DeserializeAllComparisons(await _client.GetAsync(_urls.Comparisons, cancellationToken)
                                                              .ConfigureAwait(false));
            }
            catch (RestApiClient.UnexpectedResponseException ex)
            {
                throw InvalidCredentialsException.For(ex) ?? new UnknownResponseException(ex);
            }
        }

        #endregion Get[Async], GetAll[Async]

        #region Delete, Delete[Async]

        /// <summary>
        ///     Permanently deletes a comparison, removing its files and making it inaccessible.
        /// </summary>
        /// <remarks>
        ///     Note that after you delete a comparison, you can reuse its identifier (as it is no longer in use).
        /// </remarks>
        /// <param name="identifier">The unique identifier of the comparison to delete.</param>
        /// <exception cref="NotFoundException">No comparison with the given <paramref name="identifier" /> exists.</exception>
        /// <exception cref="InvalidCredentialsException">You have provided invalid credentials.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        [PublicAPI]
        public void Delete([NotNull] string identifier)
        {
            ValidateIdentifier(identifier ?? throw new ArgumentNullException(nameof(identifier)));
            try
            {
                _client.Delete(_urls.Comparison(identifier));
            }
            catch (RestApiClient.UnexpectedResponseException ex)
            {
                throw NotFoundException.For(ex) ??
                      InvalidCredentialsException.For(ex) ?? new UnknownResponseException(ex);
            }
        }

        /// <summary>
        ///     Permanently deletes a comparison, removing its files and making it inaccessible.
        /// </summary>
        /// <remarks>
        ///     Note that after you delete a comparison, you can reuse its identifier (as it is no longer in use).
        /// </remarks>
        /// <param name="identifier">The unique identifier of the comparison to delete.</param>
        /// <exception cref="NotFoundException">No comparison with the given <paramref name="identifier" /> exists.</exception>
        /// <exception cref="InvalidCredentialsException">You have provided invalid credentials.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        [PublicAPI]
        public Task DeleteAsync([NotNull] string identifier) { return DeleteAsync(identifier, CancellationToken.None); }

        /// <summary>
        ///     Permanently deletes a comparison, removing its files and making it inaccessible.
        /// </summary>
        /// <remarks>
        ///     Note that after you delete a comparison, you can reuse its identifier (as it is no longer in use).
        /// </remarks>
        /// <param name="identifier">The unique identifier of the comparison to delete.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> for cancelling the operation.</param>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> had cancellation requested.</exception>
        /// <exception cref="NotFoundException">No comparison with the given <paramref name="identifier" /> exists.</exception>
        /// <exception cref="InvalidCredentialsException">You have provided invalid credentials.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        [PublicAPI]
        public async Task DeleteAsync([NotNull] string identifier, CancellationToken cancellationToken)
        {
            ValidateIdentifier(identifier ?? throw new ArgumentNullException(nameof(identifier)));
            try
            {
                await _client.DeleteAsync(_urls.Comparison(identifier), cancellationToken).ConfigureAwait(false);
            }
            catch (RestApiClient.UnexpectedResponseException ex)
            {
                throw NotFoundException.For(ex) ??
                      InvalidCredentialsException.For(ex) ?? new UnknownResponseException(ex);
            }
        }

        #endregion Delete, Delete[Async]

        #region Side, Create[Async]

        #region Side

        /// <summary>
        ///     Provides information about one side of a new comparison, including the file and file type.
        /// </summary>
        /// <remarks>
        ///     Use <see cref="FromFile(System.IO.Stream,string,string)" />, <see cref="FromURL(System.Uri,string,string)" />, and
        ///     other overloads to create <see cref="Side" /> objects.
        /// </remarks>
        [PublicAPI]
        public abstract class Side
        {
            internal Side() { }

            [NotNull] protected abstract IEnumerable<KeyValuePair<string, string>> FormData { get; }

            [NotNull] protected abstract IEnumerable<KeyValuePair<string, Stream>> FileContent { get; }

            /// <exception cref="ArgumentOutOfRangeException"><paramref name="sideName" /> must be one of "left" or "right".</exception>
            [Pure]
            [NotNull]
            internal IEnumerable<KeyValuePair<string, string>> GetFormData([NotNull] string sideName)
            {
                if (sideName != "left" && sideName != "right")
                {
                    throw new ArgumentOutOfRangeException(nameof(sideName), sideName,
                                                          "`sideName` must be one of \"left\" or \"right\"");
                }

                foreach (var kvp in FormData)
                {
                    yield return new KeyValuePair<string, string>($"{sideName}.{kvp.Key}", kvp.Value);
                }
            }

            /// <exception cref="ArgumentOutOfRangeException"><paramref name="sideName" /> must be one of "left" or "right".</exception>
            [Pure]
            [NotNull]
            internal IEnumerable<KeyValuePair<string, Stream>> GetFileContent([NotNull] string sideName)
            {
                if (sideName != "left" && sideName != "right")
                {
                    throw new ArgumentOutOfRangeException(nameof(sideName), sideName,
                                                          "`sideName` must be one of \"left\" or \"right\"");
                }

                foreach (var kvp in FileContent)
                {
                    yield return new KeyValuePair<string, Stream>($"{sideName}.{kvp.Key}", kvp.Value);
                }
            }

            /// <summary>Construct a <see cref="Side" /> for a file available at a given <paramref name="sourceURL" />.</summary>
            /// <param name="sourceURL">The HTTP or HTTPS URL at which the file is available for download.</param>
            /// <param name="fileType">
            ///     The file type, given as the extension (e.g. "docx"). Must be one of the supported file
            ///     extensions.
            /// </param>
            /// <param name="displayName">An optional name to show for the file in the comparison viewer (e.g. "Report (New).docx").</param>
            /// <returns>A <see cref="Side" /> object representing the file information.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            ///     <paramref name="sourceURL" /> could not be parsed as an absolute HTTP or
            ///     HTTPS URL.
            /// </exception>
            [Pure]
            [NotNull]
            public static Side FromURL([NotNull] string sourceURL,
                                       [NotNull] string fileType,
                                       [CanBeNull] string displayName = null)
            {
                return new URLSide(sourceURL, fileType, displayName);
            }

            /// <summary>Construct a <see cref="Side" /> for a file available at a given <paramref name="sourceURI" />.</summary>
            /// <param name="sourceURI">An absolute HTTP or HTTPS URI at which the file is available for download.</param>
            /// <param name="fileType">
            ///     The file type, given as the extension (e.g. "docx"). Must be one of the supported file
            ///     extensions.
            /// </param>
            /// <param name="displayName">An optional name to show for the file in the comparison viewer (e.g. "Report (New).docx").</param>
            /// <returns>A <see cref="Side" /> object representing the file information.</returns>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceURI" /> is not an absolute HTTP or HTTPS URL.</exception>
            [Pure]
            [NotNull]
            public static Side FromURL([NotNull] Uri sourceURI,
                                       [NotNull] string fileType,
                                       [CanBeNull] string displayName = null)
            {
                return new URLSide(sourceURI.ToString(), fileType, displayName);
            }

            /// <summary>Construct a <see cref="Side" /> for a file with content given by <paramref name="fileStream" />.</summary>
            /// <param name="fileStream">A <see cref="Stream" /> providing the file content.</param>
            /// <param name="fileType">
            ///     The file type, given as the extension (e.g. "docx"). Must be one of the supported file
            ///     extensions.
            /// </param>
            /// <param name="displayName">An optional name to show for the file in the comparison viewer (e.g. "Report (New).docx").</param>
            /// <returns>A <see cref="Side" /> object representing the file and information.</returns>
            [Pure]
            [NotNull]
            public static Side FromFile([NotNull] Stream fileStream,
                                        [NotNull] string fileType,
                                        [CanBeNull] string displayName = null)
            {
                return new FileSide(fileStream, fileType, displayName);
            }

            /// <summary>Construct a <see cref="Side" /> for a file with content given by <paramref name="fileBytes" />.</summary>
            /// <param name="fileBytes">A byte array providing the file content.</param>
            /// <param name="fileType">
            ///     The file type, given as the extension (e.g. "docx"). Must be one of the supported file
            ///     extensions.
            /// </param>
            /// <param name="displayName">An optional name to show for the file in the comparison viewer (e.g. "Report (New).docx").</param>
            /// <returns>A <see cref="Side" /> object representing the file and information.</returns>
            [Pure]
            [NotNull]
            [CollectionAccess(CollectionAccessType.Read)]
            public static Side FromFile([NotNull] byte[] fileBytes,
                                        [NotNull] string fileType,
                                        [CanBeNull] string displayName = null)
            {
                return new FileSide(new MemoryStream(fileBytes), fileType, displayName);
            }

            /// <summary>
            ///     Construct a <see cref="Side" /> for a local file at the given <see cref="filePath" /> with extension inferred
            ///     from the path.
            /// </summary>
            /// <param name="filePath">The location of the file.</param>
            /// <param name="displayName">An optional name to show for the file in the comparison viewer (e.g. "Report (New).docx").</param>
            /// <returns>A <see cref="Side" /> object representing the file and information.</returns>
            /// <exception cref="InvalidOperationException">
            ///     Could not infer the file extension from the given path. Please provide the
            ///     extension explicitly.
            /// </exception>
            /// <exception cref="IOException">Unable to read the file (it might not exist, or may be in use).</exception>
            /// <exception cref="SystemException">Unable to access the file, due to e.g. having insufficient permissions.</exception>
            [Pure]
            [NotNull]
            public static Side FromFile([NotNull] string filePath, [CanBeNull] string displayName = null)
            {
                var extension = Path.GetExtension(filePath);
                if (string.IsNullOrEmpty(extension))
                {
                    throw new InvalidOperationException(
                        "Could not infer the file extension from the given path. Please provide the extension explicitly.");
                }

                return FromFile(filePath, extension, displayName);
            }

            /// <summary>Construct a <see cref="Side" /> for a local file at the given <see cref="filePath" />.</summary>
            /// <param name="filePath">The location of the file.</param>
            /// <param name="fileType">
            ///     The file type, given as the extension (e.g. "docx"). Must be one of the supported file
            ///     extensions.
            /// </param>
            /// <param name="displayName">An optional name to show for the file in the comparison viewer (e.g. "Report (New).docx").</param>
            /// <returns>A <see cref="Side" /> object representing the file and information.</returns>
            /// <exception cref="IOException">Unable to read the file (it might not exist, or may be in use).</exception>
            /// <exception cref="SystemException">Unable to access the file, due to e.g. having insufficient permissions.</exception>
            [Pure]
            [NotNull]
            public static Side FromFile([NotNull] string filePath,
                                        [NotNull] string fileType,
                                        [CanBeNull] string displayName = null)
            {
                return new FileSide(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
                                    fileType, displayName);
            }
        }


        private class FileSide : Side
        {
            [NotNull] private readonly Stream _file;
            [NotNull] private readonly string _fileType;
            [CanBeNull] private readonly string _displayName;

            internal FileSide([NotNull] Stream file, [NotNull] string fileType, [CanBeNull] string displayName)
            {
                _file = file ?? throw new ArgumentNullException(nameof(file));
                _fileType = fileType ?? throw new ArgumentNullException(nameof(fileType));
                _fileType = _fileType.TrimStart('.');
                ValidateFileType(_fileType);
                _displayName = displayName;
            }

            protected override IEnumerable<KeyValuePair<string, string>> FormData =>
                new[]
                {
                    new KeyValuePair<string, string>("file_type", _fileType),
                    new KeyValuePair<string, string>("display_name", _displayName)
                };

            protected override IEnumerable<KeyValuePair<string, Stream>> FileContent =>
                new[] {new KeyValuePair<string, Stream>("file", _file)};
        }


        private class URLSide : Side
        {
            [NotNull] private readonly string _sourceURL;
            [NotNull] private readonly string _fileType;
            [CanBeNull] private readonly string _displayName;

            /// <exception cref="ArgumentOutOfRangeException">
            ///     <paramref name="sourceURL" /> could not be parsed as an absolute HTTP or
            ///     HTTPS URL.
            /// </exception>
            internal URLSide([NotNull] string sourceURL, [NotNull] string fileType, [CanBeNull] string displayName)
            {
                _sourceURL = sourceURL ?? throw new ArgumentNullException(nameof(sourceURL));
                _fileType = fileType ?? throw new ArgumentNullException(nameof(fileType));
                _fileType = _fileType.TrimStart('.');
                ValidateFileType(_fileType);
                _displayName = displayName;

                if (!ValidateSourceUrlScheme(_sourceURL))
                {
                    throw new ArgumentOutOfRangeException(nameof(sourceURL), sourceURL,
                                                          "`sourceURL` could not be parsed as an absolute HTTP or HTTPS URL.");
                }
            }

            protected override IEnumerable<KeyValuePair<string, string>> FormData =>
                new[]
                {
                    new KeyValuePair<string, string>("source_url", _sourceURL),
                    new KeyValuePair<string, string>("file_type", _fileType),
                    new KeyValuePair<string, string>("display_name", _displayName)
                };

            protected override IEnumerable<KeyValuePair<string, Stream>> FileContent =>
                Enumerable.Empty<KeyValuePair<string, Stream>>();
        }

        #endregion Side

        /// <summary>
        ///     Creates a new comparison, submitting the files for processing.
        /// </summary>
        /// <remarks>
        ///     The comparison will be immediately accessible, but will display a loading screen until the files have been
        ///     processed and compared.
        /// </remarks>
        /// <param name="left">A <see cref="Side" /> object giving information about the left file.</param>
        /// <param name="right">A <see cref="Side" /> object giving information about the right file.</param>
        /// <param name="identifier">The unique identifier of the comparison to delete.</param>
        /// <param name="isPublic">
        ///     Whether the comparison is publicly accessible, or requires authentication to view. Defaults to
        ///     false, meaning the comparison is private.
        /// </param>
        /// <param name="expires">
        ///     When the comparison should automatically expire. Defaults to <langword>null</langword> for no
        ///     automatic expiry.
        /// </param>
        /// <returns>A <see cref="Comparison" /> object giving metadata about the newly created comparison.</returns>
        /// <exception cref="BadRequestException">One or more given parameters were invalid.</exception>
        /// <exception cref="InvalidCredentialsException">You have provided invalid credentials.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        [PublicAPI]
        [Pure]
        [NotNull]
        public Comparison Create([NotNull] Side left,
                                 [NotNull] Side right,
                                 [CanBeNull] string identifier = null,
                                 bool isPublic = false,
                                 [CanBeNull] TimeSpan? expires = null)
        {
            if (identifier != null)
            {
                ValidateIdentifier(identifier);
            }

            ValidateExpires(expires);
            try
            {
                return DeserializeComparison(_client.Post(_urls.Comparisons,
                                                          data: new Dictionary<string, string>
                                                              {
                                                                  {"identifier", identifier},
                                                                  {"public", isPublic ? "true" : null},
                                                                  {
                                                                      "expiry_time",
                                                                      SerializeDateTime(DateTime.UtcNow + expires)
                                                                  }
                                                              }.Concat(left.GetFormData("left"))
                                                               .Concat(right.GetFormData("right")),
                                                          files: left.GetFileContent("left")
                                                                     .Concat(right.GetFileContent("right"))));
            }
            catch (RestApiClient.UnexpectedResponseException ex)
            {
                throw BadRequestException.For(ex) ??
                      InvalidCredentialsException.For(ex) ?? new UnknownResponseException(ex);
            }
        }

        /// <summary>
        ///     Creates a new comparison, submitting the files for processing.
        /// </summary>
        /// <remarks>
        ///     The comparison will be immediately accessible, but will display a loading screen until the files have been
        ///     processed and compared.
        /// </remarks>
        /// <param name="left">A <see cref="Side" /> object giving information about the left file.</param>
        /// <param name="right">A <see cref="Side" /> object giving information about the right file.</param>
        /// <param name="identifier">The unique identifier of the comparison to delete.</param>
        /// <param name="isPublic">
        ///     Whether the comparison is publicly accessible, or requires authentication to view. Defaults to
        ///     false, meaning the comparison is private.
        /// </param>
        /// <param name="expires">
        ///     When the comparison should automatically expire. Defaults to <langword>null</langword> for no
        ///     automatic expiry.
        /// </param>
        /// <returns>A <see cref="Comparison" /> object giving metadata about the newly created comparison.</returns>
        /// <exception cref="BadRequestException">One or more given parameters were invalid.</exception>
        /// <exception cref="InvalidCredentialsException">You have provided invalid credentials.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        [PublicAPI]
        [Pure]
        [NotNull]
        [ItemNotNull]
        public Task<Comparison> CreateAsync([NotNull] Side left,
                                            [NotNull] Side right,
                                            [CanBeNull] string identifier = null,
                                            bool isPublic = false,
                                            [CanBeNull] TimeSpan? expires = null)
        {
            return CreateAsync(left, right, CancellationToken.None, identifier, isPublic, expires);
        }

        /// <summary>
        ///     Creates a new comparison, submitting the files for processing.
        /// </summary>
        /// <remarks>
        ///     The comparison will be immediately accessible, but will display a loading screen until the files have been
        ///     processed and compared.
        /// </remarks>
        /// <param name="left">A <see cref="Side" /> object giving information about the left file.</param>
        /// <param name="right">A <see cref="Side" /> object giving information about the right file.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> for cancelling the operation.</param>
        /// <param name="identifier">The unique identifier of the comparison to delete.</param>
        /// <param name="isPublic">
        ///     Whether the comparison is publicly accessible, or requires authentication to view. Defaults to
        ///     false, meaning the comparison is private.
        /// </param>
        /// <param name="expires">
        ///     When the comparison should automatically expire. Defaults to <langword>null</langword> for no
        ///     automatic expiry.
        /// </param>
        /// <returns>A <see cref="Comparison" /> object giving metadata about the newly created comparison.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> had cancellation requested.</exception>
        /// <exception cref="BadRequestException">One or more given parameters were invalid.</exception>
        /// <exception cref="InvalidCredentialsException">You have provided invalid credentials.</exception>
        /// <exception cref="HttpRequestException">Unable to perform the request.</exception>
        [PublicAPI]
        [Pure]
        [NotNull]
        [ItemNotNull]
        public async Task<Comparison> CreateAsync([NotNull] Side left,
                                                  [NotNull] Side right,
                                                  CancellationToken cancellationToken,
                                                  [CanBeNull] string identifier = null,
                                                  bool isPublic = false,
                                                  [CanBeNull] TimeSpan? expires = null)
        {
            if (identifier != null)
            {
                ValidateIdentifier(identifier);
            }

            ValidateExpires(expires);
            try
            {
                return DeserializeComparison(await _client.PostAsync(_urls.Comparisons,
                                                                     cancellationToken,
                                                                     data: new Dictionary<string, string>
                                                                         {
                                                                             {"identifier", identifier},
                                                                             {"public", isPublic ? "true" : null},
                                                                             {
                                                                                 "expires",
                                                                                 SerializeDateTime(
                                                                                     DateTime.UtcNow + expires)
                                                                             }
                                                                         }.Concat(left.GetFormData("left"))
                                                                          .Concat(right.GetFormData("right")),
                                                                     files: left.GetFileContent("left")
                                                                        .Concat(right.GetFileContent("right")))
                                                          .ConfigureAwait(false));
            }
            catch (RestApiClient.UnexpectedResponseException ex)
            {
                throw BadRequestException.For(ex) ??
                      InvalidCredentialsException.For(ex) ?? new UnknownResponseException(ex);
            }
        }

        #endregion Side, Create[Async]

        #region PublicViewerURL, SignedViewerURL

        /// <summary>
        ///     Returns a viewer URL for a public comparison with the given <paramref name="identifier" />.
        /// </summary>
        /// <remarks>
        ///     If the comparison is private, the public viewer URL will display an error message indicating authentication is
        ///     required.
        /// </remarks>
        /// <param name="identifier">The identifier for the comparison.</param>
        /// <param name="wait">
        ///     <para>Whether or not to wait for the comparison to exist.</para>
        ///     <para>
        ///         If you are creating a comparison in the background, set this to <langword>true</langword> or else the viewer
        ///         will 404 if it is accessed before the comparison has been created.
        ///     </para>
        /// </param>
        /// <returns>A URL at which the public comparison can be viewed.</returns>
        [PublicAPI]
        [Pure]
        [NotNull]
        public string PublicViewerURL([NotNull] string identifier, bool wait = false)
        {
            ValidateIdentifier(identifier ?? throw new ArgumentNullException(nameof(identifier)));
            var url = _urls.ComparisonViewer(AccountId, identifier);
            if (wait)
            {
                url += "?wait";
            }

            return url;
        }

        /// <summary>
        ///     Returns a viewer URL for a private comparison with the given <paramref name="identifier" /> that is valid for half
        ///     an hour.
        /// </summary>
        /// <remarks>
        ///     After half an hour, the URL will have expired. If the URL is visited afterwards, there will be an error message
        ///     indicating that the link has expired.
        /// </remarks>
        /// <param name="identifier">The identifier for the comparison.</param>
        /// <param name="wait">
        ///     <para>Whether or not to wait for the comparison to exist.</para>
        ///     <para>
        ///         If you are creating a comparison in the background, set this to <langword>true</langword> or else the viewer
        ///         will 404 if it is accessed before the comparison has been created.
        ///     </para>
        /// </param>
        /// <returns>A URL at which the private comparison can be viewed that is valid for half an hour.</returns>
        [PublicAPI]
        [Pure]
        [NotNull]
        public string SignedViewerURL([NotNull] string identifier, bool wait = false)
        {
            return SignedViewerURL(identifier, TimeSpan.FromMinutes(30), wait);
        }

        /// <summary>
        ///     Returns a viewer URL for a private comparison with the given <paramref name="identifier" /> that is valid for a
        ///     length of time.
        /// </summary>
        /// <remarks>
        ///     After the time given by <paramref name="validFor" /> has passed, the URL will have expired. If the URL is visited
        ///     afterwards, there will be an error message indicating that the link has expired.
        /// </remarks>
        /// <param name="identifier">The identifier for the comparison.</param>
        /// <param name="validFor">The length of time the URL will be valid for.</param>
        /// <param name="wait">
        ///     <para>Whether or not to wait for the comparison to exist.</para>
        ///     <para>
        ///         If you are creating a comparison in the background, set this to <langword>true</langword> or else the viewer
        ///         will 404 if it is accessed before the comparison has been created.
        ///     </para>
        /// </param>
        /// <returns>A URL at which the private comparison can be viewed for a period of time.</returns>
        [PublicAPI]
        [Pure]
        [NotNull]
        public string SignedViewerURL([NotNull] string identifier, TimeSpan validFor, bool wait = false)
        {
            return SignedViewerURL(identifier, DateTime.UtcNow + validFor, wait);
        }

        /// <summary>
        ///     Returns a viewer URL for a private comparison with the given <paramref name="identifier" /> that is valid for a
        ///     length of time.
        /// </summary>
        /// <remarks>
        ///     After the time given by <paramref name="validUntil" /> has passed, the URL will have expired. If the URL is visited
        ///     afterwards, there will be an error message indicating that the link has expired.
        /// </remarks>
        /// <param name="identifier">The identifier for the comparison.</param>
        /// <param name="validUntil">The time at which the URL will become invalid.</param>
        /// <param name="wait">
        ///     <para>Whether or not to wait for the comparison to exist.</para>
        ///     <para>
        ///         If you are creating a comparison in the background, set this to <langword>true</langword> or else the viewer
        ///         will 404 if it is accessed before the comparison has been created.
        ///     </para>
        /// </param>
        /// <returns>A URL at which the private comparison can be viewed for a period of time.</returns>
        [PublicAPI]
        [Pure]
        [NotNull]
        public string SignedViewerURL([NotNull] string identifier, DateTime validUntil, bool wait = false)
        {
            ValidateIdentifier(identifier ?? throw new ArgumentNullException(nameof(identifier)));

            var baseURL = _urls.ComparisonViewer(AccountId, identifier);
            var validUntilTimestamp = Signing.ValidUntilTimestamp(validUntil);
            var signature = Signing.ViewerSignatureFor(AccountId, AuthToken, identifier, validUntilTimestamp);

            return $"{baseURL}?valid_until={validUntilTimestamp}&signature={signature}{(wait ? "&wait" : "")}";
        }

        #endregion PublicViewerURL, SignedViewerURL

        #region Export

        /// <summary>
        ///     Runs an export of given kind, for a given existing comparison
        /// </summary>
        /// <param name="comparisonIdentifier">
        ///     The unique identifier of the comparison to export
        /// </param>
        /// <param name="kind">
        ///     Export kind. Supported values: single_page, combined, left, right.
        /// </param>
        /// <param name="includeCoverPage">
        ///     Relevant only for combined comparison, indicates whether it should include a cover page
        /// </param>
        /// <returns>
        ///     An <see cref="Export" /> object giving metadata about the newly created export.
        /// </returns>
        /// <exception cref="BadRequestException">
        ///     One or more given parameters were invalid.
        /// </exception>
        /// <exception cref="InvalidCredentialsException">
        ///     You have provided invalid credentials.
        /// </exception>
        /// <exception cref="HttpRequestException">
        ///     Unable to perform the request.
        /// </exception>
        [PublicAPI]
        [Pure]
        [NotNull]
        public Export RunExport([NotNull] string comparisonIdentifier, ExportKind kind, bool includeCoverPage = true)
        {
            ValidateIdentifier(comparisonIdentifier ?? throw new ArgumentNullException(nameof(comparisonIdentifier)));

            try
            {
                var inputData = new Dictionary<string, string>
                {
                    {"comparison", comparisonIdentifier},
                    {"kind", ExportKindStrings.Resolve(kind)},
                    {"include_cover_page", includeCoverPage.ToString()}
                };

                var exportJson = _client.Post(_urls.Exports, data: inputData);

                return SerializationUtils.DeserializeExport(exportJson);
            }
            catch (RestApiClient.UnexpectedResponseException ex)
            {
                throw NotFoundException.For(ex) ??
                      InvalidCredentialsException.For(ex) ?? new UnknownResponseException(ex);
            }
        }

        /// <summary>
        ///     Runs an export of given kind, for a given existing comparison
        /// </summary>
        /// <param name="comparisonIdentifier">
        ///     The unique identifier of the comparison to export
        /// </param>
        /// <param name="kind">
        ///     Export kind. Supported values: single_page, combined, left, right.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken" /> for cancelling the operation.
        /// </param>
        /// <param name="includeCoverPage">
        ///     Relevant only for combined comparison, indicates whether it should include a cover page
        /// </param>
        /// <returns>
        ///     An <see cref="Export" /> object giving metadata about the newly created export.
        /// </returns>
        /// <exception cref="BadRequestException">
        ///     One or more given parameters were invalid.
        /// </exception>
        /// <exception cref="InvalidCredentialsException">
        ///     You have provided invalid credentials.
        /// </exception>
        /// <exception cref="HttpRequestException">
        ///     Unable to perform the request.
        /// </exception>
        [PublicAPI]
        [Pure]
        [NotNull]
        public async Task<Export> RunExportAsync([NotNull] string comparisonIdentifier,
                                                 ExportKind kind,
                                                 CancellationToken cancellationToken,
                                                 bool includeCoverPage = true)
        {
            ValidateIdentifier(comparisonIdentifier ?? throw new ArgumentNullException(nameof(comparisonIdentifier)));

            try
            {
                var inputData = new Dictionary<string, string>
                {
                    {"comparison", comparisonIdentifier},
                    {"kind", ExportKindStrings.Resolve(kind)},
                    {"include_cover_page", includeCoverPage.ToString()}
                };

                var exportJson =
                    await _client.PostAsync(_urls.Exports, data: inputData, cancellationToken: cancellationToken);

                return SerializationUtils.DeserializeExport(exportJson);
            }
            catch (RestApiClient.UnexpectedResponseException ex)
            {
                throw NotFoundException.For(ex) ??
                      InvalidCredentialsException.For(ex) ?? new UnknownResponseException(ex);
            }
        }

        /// <summary>
        ///     Gets an existing Export, of given identifier.
        /// </summary>
        /// <param name="exportIdentifier">
        ///     The unique identifier of the Export to access.
        /// </param>
        /// <returns>
        ///     An <see cref="Export" /> object giving metadata about the export accessed
        /// </returns>
        /// <exception cref="BadRequestException">
        ///     One or more given parameters were invalid.
        /// </exception>
        /// <exception cref="InvalidCredentialsException">
        ///     You have provided invalid credentials.
        /// </exception>
        /// <exception cref="HttpRequestException">
        ///     Unable to perform the request.
        /// </exception>
        [PublicAPI]
        [Pure]
        [NotNull]
        public Export GetExport([NotNull] string exportIdentifier)
        {
            try
            {
                var exportJson = _client.Get(_urls.Export(exportIdentifier));

                return SerializationUtils.DeserializeExport(exportJson);
            }
            catch (RestApiClient.UnexpectedResponseException ex)
            {
                throw NotFoundException.For(ex) ??
                      InvalidCredentialsException.For(ex) ?? new UnknownResponseException(ex);
            }
        }

        /// <summary>
        ///     Gets an existing Export, of given identifier.
        /// </summary>
        /// <param name="exportIdentifier">
        ///     The unique identifier of the Export to access.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken" /> for cancelling the operation.
        /// </param>
        /// <returns>
        ///     An <see cref="Export" /> object giving metadata about the export accessed
        /// </returns>
        /// <exception cref="BadRequestException">
        ///     One or more given parameters were invalid.
        /// </exception>
        /// <exception cref="InvalidCredentialsException">
        ///     You have provided invalid credentials.
        /// </exception>
        /// <exception cref="HttpRequestException">
        ///     Unable to perform the request.
        /// </exception>
        [PublicAPI]
        [Pure]
        [NotNull]
        public async Task<Export> GetExportAsync([NotNull] string exportIdentifier, CancellationToken cancellationToken)
        {
            try
            {
                var exportJson = await _client.GetAsync(_urls.Export(exportIdentifier), cancellationToken);

                return SerializationUtils.DeserializeExport(exportJson);
            }
            catch (RestApiClient.UnexpectedResponseException ex)
            {
                throw NotFoundException.For(ex) ??
                      InvalidCredentialsException.For(ex) ?? new UnknownResponseException(ex);
            }
        }

        #endregion Export

        #region Dispose

        [NotNull] private readonly object _disposeLock = new object();
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (_disposeLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            _client.Dispose();
        }

        #endregion Dispose

        #endregion Public interface

        #region Public static methods: GenerateIdentifier

        [PublicAPI]
        [Pure]
        [NotNull]
        public static string GenerateIdentifier()
        {
            // Constants for generating a unique identifier with very high probability:
            const int identifierLength = 12;
            const string identifierCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

            var random = new Random();
            var sb = new StringBuilder();
            for (var i = 0; i < identifierLength; ++i)
            {
                sb.Append(identifierCharacters[random.Next(0, identifierCharacters.Length)]);
            }

            return sb.ToString();
        }

        #endregion Public static methods: GenerateIdentifier

        #region Private fields and helpers

        [NotNull] private readonly RestApiClient _client;

        #region Serialization

        [DataContract]
        [Serializable]
        private class AllComparisonsResult
        {
            [DataMember(Name = "results")]
            [NotNull]
            // ReSharper disable once NotNullMemberIsNotInitialized
            public List<Comparison> Results { get; private set; }
        }


        /// <exception cref="UnknownResponseException">
        ///     Unable to parse the response and extract the array of comparison results.
        /// </exception>
        [Pure]
        [NotNull]
        private static List<Comparison> DeserializeAllComparisons([NotNull] string jsonComparisonArray)
        {
            try
            {
                return JsonConvert.DeserializeObject<AllComparisonsResult>(jsonComparisonArray).AssertNotNull().Results
                                  .AssertNotNull();
            }
            catch (Exception ex) when (ex is JsonException || ex is NullReferenceException)
            {
                throw new UnknownResponseException(jsonComparisonArray,
                                                   "Unable to parse the response and extract the array of comparison results.",
                                                   ex);
            }
        }

        /// <exception cref="UnknownResponseException">
        ///     Unable to parse the response as a comparison.
        /// </exception>
        [Pure]
        [NotNull]
        private static Comparison DeserializeComparison([NotNull] string jsonComparison)
        {
            try
            {
                return JsonConvert.DeserializeObject<Comparison>(jsonComparison).AssertNotNull();
            }
            catch (Exception ex) when (ex is JsonException || ex is NullReferenceException)
            {
                throw new UnknownResponseException(jsonComparison, "Unable to parse the response as a comparison.", ex);
            }
        }

        /// <summary>
        ///     Serializes a given <see cref="DateTime" /> in ISO format.
        /// </summary>
        /// <param name="dateTime">
        ///     An optional <see cref="DateTime" /> to serialize in ISO format.
        /// </param>
        /// <returns>
        //      If a <paramref name="dateTime" /> was provided, its serialized representation in ISO format, otherwise <see langword="null" />.
        /// </returns>
        [Pure]
        [CanBeNull]
        [ContractAnnotation("dateTime:null => null; dateTime:notnull => notnull")]
        private static string SerializeDateTime([CanBeNull] DateTime? dateTime)
        {
            return dateTime?.ToUniversalTime().ToString("o");
        }

        #endregion Serialization

        #region Validation

        private const int identifierLengthMin = 1;
        private const int identifierLengthMax = 1024;

        [NotNull] private static readonly HashSet<string> _allowedFileTypes = new HashSet<string>
        {
            "doc", // Word 97-2003 Document
            "docm", // Word Macro-Enabled Document
            "docx", // Word Document
            "pdf", // Portable Document Format
            "ppt", // PowerPoint 97-2003 Presentation
            "pptm", // PowerPoint Macro-Enabled Presentation
            "pptx", // PowerPoint Presentation
            "rtf", // Rich Text Format
            "txt" // text
        };

        private static readonly string _allowedFileTypesString = string.Join(", ", _allowedFileTypes.OrderBy(x => x));

        private readonly URLs _urls;

        /// <exception cref="ArgumentOutOfRangeException">
        ///     Invalid value for comparison <paramref name="expires">expiry</paramref>.
        /// </exception>
        private static void ValidateExpires(TimeSpan? expires)
        {
            if (expires.HasValue && expires.Value.TotalSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expires), expires.Value,
                                                      "The comparison expiry time must be in the future.");
            }
        }

        /// <exception cref="ArgumentOutOfRangeException">
        ///     Invalid value for comparison side <paramref name="fileType">file type</paramref>.
        /// </exception>
        private static void ValidateFileType([NotNull] string fileType)
        {
            Debug.Assert(fileType != null);

#pragma warning disable CA1308
            if (!_allowedFileTypes.Contains(fileType.ToLowerInvariant())) // Normalizing to lowercase is fine
#pragma warning restore CA1308
            {
                throw new ArgumentOutOfRangeException(nameof(fileType), fileType,
                                                      $"An unsupported comparison side file type was specified. Supported types: {_allowedFileTypesString}");
            }
        }

        /// <exception cref="ArgumentOutOfRangeException">
        ///     Invalid value for comparison <paramref name="identifier" />.
        /// </exception>
        private static void ValidateIdentifier([NotNull] string identifier)
        {
            Debug.Assert(identifier != null);

            if (identifier.Length < identifierLengthMin)
            {
                throw new ArgumentOutOfRangeException(nameof(identifier), identifier,
                                                      $"Comparison identifier must be at least {identifierLengthMin} characters.");
            }

            if (identifier.Length > identifierLengthMax)
            {
                throw new ArgumentOutOfRangeException(nameof(identifier), identifier,
                                                      $"Comparison identifier must be at most {identifierLengthMax} characters.");
            }

            if (identifier.Any(c => (c < 'a' || c > 'z') && (c < 'A' || c > 'Z') && (c < '0' || c > '9') &&
                                    !"-._".Contains(c)))
            {
                throw new ArgumentOutOfRangeException(nameof(identifier), identifier,
                                                      "Comparison identifier can only contain ASCII letters, numbers, and the \"-._\" characters.");
            }
        }

        [Pure]
        private static bool ValidateSourceUrlScheme([NotNull] string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            var scheme = uri.Scheme.ToUpperInvariant();
            return scheme == "HTTP" || scheme == "HTTPS";
        }

        #endregion Validation

        #endregion Private fields and helpers
    }
}
