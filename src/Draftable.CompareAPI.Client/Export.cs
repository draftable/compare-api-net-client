using System.Runtime.Serialization;
using JetBrains.Annotations;

namespace Draftable.CompareAPI.Client
{
    /// <summary>
    /// Represents an export, created via the Draftable API
    /// </summary>
    [PublicAPI, DataContract(Name = "export")]
    public class Export
    {
        /// <summary>
        /// Identifier of the export itself (note that it is different from the comparison ID).
        /// </summary>
        [DataMember(Name = "identifier"), NotNull]
        public string Identifier { get; private set; }

        /// <summary>
        /// Identifier of the comparison used for running this export
        /// </summary>
        [DataMember(Name = "comparison"), NotNull]
        public string Comparison { get; private set; }

        /// <summary>
        /// Url of the export
        /// </summary>
        [DataMember(Name = "url"), NotNull]
        public string Url { get; private set; }

        /// <summary>
        /// Export kind. Supported values: single_page, combined, left, right.
        /// </summary>
        [DataMember(Name = "kind"), NotNull]
        public string Kind { get; private set; }

        /// <summary>
        /// Indicates if the export is Ready.
        /// </summary>
        [DataMember(Name = "ready")]
        public bool Ready { get; private set; }

        /// <summary>
        /// Indicates if the export has failed.
        /// </summary>
        [DataMember(Name = "failed")]
        public bool Failed { get; private set; }
    }
}
