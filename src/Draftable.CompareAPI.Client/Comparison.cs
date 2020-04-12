using System;
using System.Runtime.Serialization;

using Draftable.CompareAPI.Client.Internal;

using JetBrains.Annotations;

using Newtonsoft.Json;


namespace Draftable.CompareAPI.Client
{
    /// <summary>
    /// Represents a comparison created via the Draftable API.
    /// </summary>
    [PublicAPI, DataContract(Name = "comparison")]
    public class Comparison
    {
        /// <summary>
        /// Represents one side of a comparison created via the Draftable API.
        /// </summary>
        [PublicAPI, DataContract(Name = "side")]
        public class Side
        {
            /// <summary>
            /// The type of the file, given as the file extension.
            /// </summary>
            [DataMember(Name = "file_type"), NotNull]
            public string FileType { get; private set; }

            /// <summary>
            /// If the file was provided by URL, gives the source URL for the file.
            /// </summary>
            /// <remarks>
            /// If the file was uploaded, this will be <langword>null</langword>.
            /// </remarks>
            [DataMember(Name = "source_url"), CanBeNull]
            public string SourceURL { get; private set; }

            /// <summary>
            /// The display name for the side, if one was provided.
            /// </summary>
            /// <remarks>
            /// Will be <langword>null</langword> if no display name was provided.
            /// </remarks>
            [DataMember(Name = "display_name"), CanBeNull]
            public string DisplayName { get; private set; }

            /// <summary>
            /// Creates a <see cref="Side"/> object representing one side of a comparison.
            /// </summary>
            /// <param name="fileType">The type of the file, given as the file extension.</param>
            /// <param name="sourceURL">The source URL for the file, if it was provided by URL (as opposed to uploaded).</param>
            /// <param name="displayName">The display name for the side, or <langword>null</langword> for no display name.</param>
            public Side([NotNull] string fileType, [CanBeNull] string sourceURL, [CanBeNull] string displayName)
            {
                FileType = fileType ?? throw new ArgumentNullException(nameof(fileType));
                SourceURL = sourceURL;
                DisplayName = displayName;
            }

            [Pure, NotNull]
            public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore}).AssertNotNull();
        }


        /// <summary>
        /// The comparison's string identifier.
        /// </summary>
        /// <remarks>
        /// The <see cref="Identifier"/> uniquely identifies a comparison within your account.
        /// </remarks>
        [DataMember(Name = "identifier"), NotNull]
        public string Identifier { get; private set; }

        /// <summary>
        /// Information about the left side of the comparison.
        /// </summary>
        [DataMember(Name = "left"), NotNull]
        public Side Left { get; private set; }

        /// <summary>
        /// Information about the right side of the comparison.
        /// </summary>
        [DataMember(Name = "right"), NotNull]
        public Side Right { get; private set; }

        /// <summary>
        /// Whether the comparison is publically accessible, or requires authorization to view.
        /// </summary>
        [DataMember(Name = "public")]
        public bool IsPublic { get; private set; }

        /// <summary>
        /// When the comparison was first created.
        /// </summary>
        [DataMember(Name = "creation_time")]
        public DateTime CreationTime { get; private set; }

        /// <summary>
        /// When the comparison will expire and be automatically deleted, or <langword>null</langword> for no expiry.
        /// </summary>
        [DataMember(Name = "expiry_time"), CanBeNull]
        public DateTime? ExpiryTime { get; private set; }

        /// <summary>
        /// Whether the comparison is ready - i.e. whether the comparison has been processed and is ready for display.
        /// </summary>
        [IgnoreDataMember]
        public bool Ready => ReadyTime.HasValue;

        /// <summary>
        /// If the comparison is <see cref="Ready"/>, gives the time at which it became ready.
        /// </summary>
        /// <remarks>
        /// Will be <langword>null</langword> if the comparison is not <see cref="Ready"/>.
        /// </remarks>
        [DataMember(Name = "ready_time"), CanBeNull]
        public DateTime? ReadyTime { get; private set; }

        /// <summary>
        /// If the comparison is <see cref="Ready"/>, indicates whether the comparison failed.
        /// </summary>
        /// <remarks>
        /// Will be <langword>null</langword> if the comparison is not <see cref="Ready"/>.
        /// </remarks>
        [DataMember(Name = "failed"), CanBeNull]
        public bool? Failed { get; private set; }

        /// <summary>
        /// If the comparison <see cref="Failed"/>, an error message describing the failure.
        /// </summary>
        /// <remarks>
        /// Will be <langword>null</langword> if the comparison has not <see cref="Failed"/>.
        /// </remarks>
        [DataMember(Name = "error_message"), CanBeNull]
        public string ErrorMessage { get; private set; }


        /// <summary>
        /// Creates a <see cref="Comparison"/> object representing a comparison.
        /// </summary>
        /// <param name="identifier">The comparison's string identifier.</param>
        /// <param name="left">A <see cref="Side"/> object representing the left side of the comparison.</param>
        /// <param name="right">A <see cref="Side"/> object representing the right side of the comparison.</param>
        /// <param name="isPublic">Whether the comparison is public or not.</param>
        /// <param name="creationTime">When the comparison was created.</param>
        /// <param name="expiryTime">When the comparison will expire, if an expiry time is set.</param>
        /// <param name="readyTime">If the comparison is ready, the time at which it became ready, otherwise <langword>null</langword>.</param>
        /// <param name="failed">If the comparison is ready, whether the comparison failed.</param>
        /// <param name="errorMessage">If the comparison failed, an error message describing the failure.</param>
        /// <exception cref="ArgumentOutOfRangeException">If the comparison is ready, <paramref name="failed"/> must be non-null. If not ready, <paramref name="failed"/> must be null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If the comparison has <paramref name="failed"/> then <paramref name="errorMessage"/> must be non-null. Otherwise, <paramref name="errorMessage"/> must be null.</exception>
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

            if (Ready) {
                if (!failed.HasValue) {
                    throw new ArgumentOutOfRangeException(nameof(failed), failed, "If the comparison is ready, `failed` cannot be null.");
                }
            } else {
                if (failed.HasValue) {
                    throw new ArgumentOutOfRangeException(nameof(failed), failed, "If the comparison isn't ready, `failed` must be null.");
                }
            }

            if (failed.HasValue && failed.Value) {
                if (errorMessage == null) {
                    throw new ArgumentOutOfRangeException(nameof(errorMessage), errorMessage, "If the comparison has failed, `errorMessage` cannot be null.");
                }
            } else {
                if (errorMessage != null) {
                    throw new ArgumentOutOfRangeException(nameof(errorMessage), errorMessage, "If the comparison hasn't failed, `errorMessage` must be null.");
                }
            }
        }

        [Pure, NotNull]
        public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore}).AssertNotNull();
    }

}
