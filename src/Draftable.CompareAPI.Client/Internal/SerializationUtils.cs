using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

using JetBrains.Annotations;

using Newtonsoft.Json;


namespace Draftable.CompareAPI.Client.Internal
{
    internal class SerializationUtils
    {
        /// <exception cref="Comparisons.UnknownResponseException">Unable to parse the response as a comparison.</exception>
        [Pure]
        [NotNull]
        public static Comparison DeserializeComparison([NotNull] string jsonComparison)
        {
            try
            {
                return JsonConvert.DeserializeObject<Comparison>(jsonComparison).AssertNotNull();
            }
            catch (JsonException ex)
            {
                throw new Comparisons.UnknownResponseException(jsonComparison,
                                                               "Unable to parse the response as a comparison.", ex);
            }
            catch (NullReferenceException ex)
            {
                throw new Comparisons.UnknownResponseException(jsonComparison,
                                                               "Unable to parse the response as a comparison.", ex);
            }
        }

        /// <exception cref="Comparisons.UnknownResponseException">Unable to parse the response as a series of comparisons.</exception>
        [Pure]
        [NotNull]
        public static List<Comparison> DeserializeAllComparisons([NotNull] string jsonComparisonArray)
        {
            try
            {
                return JsonConvert.DeserializeObject<AllComparisonsResult>(jsonComparisonArray).AssertNotNull().Results
                                  .AssertNotNull();
            }
            catch (JsonException ex)
            {
                throw new Comparisons.UnknownResponseException(jsonComparisonArray,
                                                               "Unable to parse the response and extract the array of comparison results.",
                                                               ex);
            }
            catch (NullReferenceException ex)
            {
                throw new Comparisons.UnknownResponseException(jsonComparisonArray,
                                                               "Unable to parse the response and extract the array of comparison results.",
                                                               ex);
            }
        }

        /// <exception cref="Comparisons.UnknownResponseException">Unable to parse the response as an export.</exception>
        [Pure]
        [NotNull]
        public static Export DeserializeExport([NotNull] string jsonExport)
        {
            try
            {
                return JsonConvert.DeserializeObject<Export>(jsonExport).AssertNotNull();
            }
            catch (JsonException ex)
            {
                throw new Comparisons.UnknownResponseException(jsonExport, "Unable to parse the response as an export.",
                                                               ex);
            }
            catch (NullReferenceException ex)
            {
                throw new Comparisons.UnknownResponseException(jsonExport, "Unable to parse the response as an export.",
                                                               ex);
            }
        }


        [DataContract]
        [Serializable]
        internal class AllComparisonsResult
        {
            /*
                // Unnecessary fields
                [DataMember(Name="count")]
                public int Count { get; private set; }

                [DataMember(Name="limit"), CanBeNull]
                public int? Limit { get; private set; }

                [DataMember(Name="offset")]
                public int Offset { get; private set; }
                */

            // ReSharper disable once NotNullMemberIsNotInitialized
            [DataMember(Name = "results")]
            [NotNull]
            public List<Comparison> Results { get; private set; }
        }
    }
}
