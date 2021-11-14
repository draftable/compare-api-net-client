using System;

using Draftable.CompareAPI.Client.Internal;

using JetBrains.Annotations;

using Newtonsoft.Json;


namespace Draftable.CompareAPI.Client
{
    /// <summary>
    ///     Thrown when the response received from the server is unrecognized. This should not occur in regular use of the API,
    ///     but is possible if for example you have a misconfigured proxy server.
    /// </summary>
    /// <remarks>
    ///     If requests made to the API are not being intercepted, an <see cref="UnknownResponseException" /> indicates an
    ///     error in this client library. Please contact support@draftable.com if this is the case.
    /// </remarks>
    [PublicAPI]
    public class UnknownResponseException : Comparisons.RequestExceptionBase
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
                                          [NotNull] JsonException ex)
            : base(
                $"{message}\nA deserialization error indicates an issue in this client library, or the comparison API. Contact support@draftable.com for assistance, or open an issue on GitHub.",
                ex)
        {
            ResponseContent = responseContent;
        }

        internal UnknownResponseException([NotNull] string responseContent,
                                          [NotNull] string message,
                                          [NotNull] NullReferenceException ex)
            : base(
                $"{message}\nA deserialization error indicates an issue in this client library, or the comparison API. Contact support@draftable.com for assistance, or open an issue on GitHub.",
                ex)
        {
            ResponseContent = responseContent;
        }
    }
}
