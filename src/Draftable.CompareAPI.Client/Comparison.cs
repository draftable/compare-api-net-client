using System;
using System.Runtime.Serialization;

using Draftable.CompareAPI.Client.Internal;

using JetBrains.Annotations;

using Newtonsoft.Json;


namespace Draftable.CompareAPI.Client
{
    /// <summary>
    ///     A comparison in the Draftable API.
    /// </summary>
    [PublicAPI]
    [DataContract(Name = "comparison")]
    public class Comparison
    {
        /// <summary>
        ///     The unique identifier of the comparison.
        /// </summary>
        [DataMember(Name = "identifier")]
        [NotNull]
        public string Identifier { get; private set; }

        /// <summary>
        ///     A <see cref="Side" /> instance representing the left side of the comparison.
        /// </summary>
        [DataMember(Name = "left")]
        [NotNull]
        public Side Left { get; private set; }

        /// <summary>
        ///     A <see cref="Side" /> instance representing the right side of the comparison.
        /// </summary>
        [DataMember(Name = "right")]
        [NotNull]
        public Side Right { get; private set; }

        /// <summary>
        ///     Indicates if the comparison is public.
        /// </summary>
        [DataMember(Name = "public")]
        public bool IsPublic { get; private set; }

        /// <summary>
        ///     The timestamp for when the comparison was created.
        /// </summary>
        [DataMember(Name = "creation_time")]
        public DateTime CreationTime { get; private set; }

        /// <summary>
        ///     If an expiry time was set, the timestamp when the comparison will expire, otherwise <see langword="null" />.
        /// </summary>
        [DataMember(Name = "expiry_time")]
        [CanBeNull]
        public DateTime? ExpiryTime { get; private set; }

        /// <summary>
        ///     Indicates if the comparison is ready (i.e. processing has been completed). To check if processing was successful
        ///     inspect the <see cref="Failed" /> property.
        /// </summary>
        [IgnoreDataMember]
        public bool Ready => ReadyTime.HasValue;

        /// <summary>
        ///     If the comparison is <see cref="Ready" />, the timestamp when the comparison become ready, otherwise
        ///     <see langword="null" />.
        /// </summary>
        [DataMember(Name = "ready_time")]
        [CanBeNull]
        public DateTime? ReadyTime { get; private set; }

        /// <summary>
        ///     If the comparison is <see cref="Ready" />, indicates if processing failed, otherwise <see langword="null" />.
        /// </summary>
        [DataMember(Name = "failed")]
        [CanBeNull]
        public bool? Failed { get; private set; }

        /// <summary>
        ///     If the comparison <see cref="Failed" />, an error message which describes the failure, otherwise
        ///     <see langword="null" />.
        /// </summary>
        [DataMember(Name = "error_message")]
        [CanBeNull]
        public string ErrorMessage { get; private set; }

        /// <summary>
        ///     Creates a <see cref="Comparison" /> instance representing a comparison.
        /// </summary>
        /// <param name="identifier">
        ///     The unique identifier of the comparison.
        /// </param>
        /// <param name="left">
        ///     A <see cref="Side" /> instance representing the left side of the comparison.
        /// </param>
        /// <param name="right">
        ///     A <see cref="Side" /> instance representing the right side of the comparison.
        /// </param>
        /// <param name="isPublic">
        ///     Indicates if the comparison is public.
        /// </param>
        /// <param name="creationTime">
        ///     The timestamp for when the comparison was created.
        /// </param>
        /// <param name="expiryTime">
        ///     If an expiry time was set, the timestamp when the comparison will expire, otherwise <see langword="null" />.
        /// </param>
        /// <param name="readyTime">
        ///     If the comparison is ready, the timestamp when the comparison become ready, otherwise <see langword="null" />.
        /// </param>
        /// <param name="failed">
        ///     If the comparison is ready, indicates if processing failed, otherwise <see langword="null" />.
        /// </param>
        /// <param name="errorMessage">
        ///     If the comparison <see paramref="failed" />, an error message which describes the failure, otherwise
        ///     <see langword="null" />.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     If the comparison is ready, <paramref name="failed" /> must not be <see langword="null" />. Otherwise,
        ///     <paramref name="failed" /> must be <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     If the comparison <paramref name="failed" />, then <paramref name="errorMessage" /> must not be
        ///     <see langword="null" />. Otherwise, <paramref name="errorMessage" /> must be <see langword="null" />.
        /// </exception>
        public Comparison([NotNull] string identifier,
                          [NotNull] Side left,
                          [NotNull] Side right,
                          bool isPublic,
                          DateTime creationTime,
                          [CanBeNull] DateTime? expiryTime,
                          [CanBeNull] DateTime? readyTime,
                          [CanBeNull] bool? failed,
                          [CanBeNull] string errorMessage)
        {
            Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
            Left = left ?? throw new ArgumentNullException(nameof(left));
            Right = right ?? throw new ArgumentNullException(nameof(right));
            IsPublic = isPublic;
            CreationTime = creationTime;
            ExpiryTime = expiryTime;
            ReadyTime = readyTime;
            Failed = failed;
            ErrorMessage = errorMessage;

            if (Ready)
            {
                if (!failed.HasValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(failed),
                                                          "The failed parameter must not be null if readyTime is not null.");
                }
            }
            else if (failed.HasValue)
            {
                throw new ArgumentOutOfRangeException(nameof(failed), failed,
                                                      "The failed parameter must be null if readyTime is null.");
            }

            if (errorMessage == null)
            {
                if (failed.HasValue && failed.Value)
                {
                    throw new ArgumentOutOfRangeException(nameof(errorMessage),
                                                          "The errorMessage parameter must not be null if failed is true.");
                }
            }
            else if (!failed.HasValue || !failed.Value)
            {
                throw new ArgumentOutOfRangeException(nameof(errorMessage), errorMessage,
                                                      "The errorMessage parameter must be null if failed is false or null.");
            }
        }

        [Pure]
        public override string ToString()
        {
            var settings = new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore};

            return JsonConvert.SerializeObject(this, Formatting.Indented, settings).AssertNotNull();
        }


        /// <summary>
        ///     A side of a comparison in the Draftable API.
        /// </summary>
        [PublicAPI]
        [DataContract(Name = "side")]
        public class Side
        {
            /// <summary>
            ///     The type of the file, provided as a file extension (e.g. "pdf").
            /// </summary>
            [DataMember(Name = "file_type")]
            [NotNull]
            public string FileType { get; private set; }

            /// <summary>
            ///     The source URL of the file if one was provided, otherwise <see langword="null" />.
            /// </summary>
            [DataMember(Name = "source_url")]
            [CanBeNull]
            public string SourceURL { get; private set; }

            /// <summary>
            ///     The display name for the side if one was provided, otherwise <see langword="null" />.
            /// </summary>
            [DataMember(Name = "display_name")]
            [CanBeNull]
            public string DisplayName { get; private set; }

            /// <summary>
            ///     Creates a <see cref="Side" /> instance representing a side of a comparison.
            /// </summary>
            /// <param name="fileType">
            ///     The type of the file, provided as a file extension (e.g. "pdf").
            /// </param>
            /// <param name="sourceURL">
            ///     The source URL of the file, if the file content is not being provided in the request, otherwise
            ///     <see langword="null" />.
            /// </param>
            /// <param name="displayName">
            ///     The display name for the side or <see langword="null" /> for no display name.
            /// </param>
            public Side([NotNull] string fileType, [CanBeNull] string sourceURL, [CanBeNull] string displayName)
            {
                FileType = fileType ?? throw new ArgumentNullException(nameof(fileType));
                SourceURL = sourceURL;
                DisplayName = displayName;
            }

            [Pure]
            public override string ToString()
            {
                var settings = new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore};

                return JsonConvert.SerializeObject(this, Formatting.Indented, settings).AssertNotNull();
            }
        }
    }
}
