using System.Runtime.Serialization;

using JetBrains.Annotations;


namespace Draftable.CompareAPI.Client
{
    /// <summary>
    ///     Represents an export created via the Draftable API.
    /// </summary>
    [PublicAPI]
    [DataContract(Name = "export")]
    public class Export
    {
        /// <summary>
        ///     Identifier of the export itself (note that it is different from the comparison ID).
        /// </summary>
        [DataMember(Name = "identifier")]
        [NotNull]
        // ReSharper disable once NotNullMemberIsNotInitialized
        public string Identifier { get; private set; }

        /// <summary>
        ///     Identifier of the comparison used for running this export
        /// </summary>
        [DataMember(Name = "comparison")]
        [NotNull]
        // ReSharper disable once NotNullMemberIsNotInitialized
        public string Comparison { get; private set; }

        /// <summary>
        ///     Url of the export
        /// </summary>
        [DataMember(Name = "url")]
        [NotNull]
        // ReSharper disable once NotNullMemberIsNotInitialized
        public string Url { get; private set; }

        /// <summary>
        ///     Export kind. Supported values: single_page, combined, left, right.
        /// </summary>
        [DataMember(Name = "kind")]
        [NotNull]
        // ReSharper disable once NotNullMemberIsNotInitialized
        public string Kind { get; private set; }

        /// <summary>
        ///     Indicates if processing of the export request has completed.
        /// </summary>
        [DataMember(Name = "ready")]
        public bool Ready { get; private set; }

        /// <summary>
        ///     Indicates whether cover page should be included for combined exports
        /// </summary>
        [DataMember(Name = "include_cover_page")]
        public bool IncludeCoverPage { get; private set; }

        /// <summary>
        ///     Indicates if exporting failed if <see cref="Ready" /> is true.
        /// </summary>
        /// <remarks>
        ///     Will be <see langword="null" /> if the export is not <see cref="Ready" />.
        /// </remarks>
        /// TODO: Ensure above re: null is implemented.
        [DataMember(Name = "failed")]
        public bool? Failed { get; private set; }

        /// <summary>
        ///     Error message for failed exports. This is set to null for successful exports.
        /// </summary>
        [DataMember(Name = "error_message")]
        [CanBeNull]
        public string ErrorMessage { get; private set; }
    }
}
