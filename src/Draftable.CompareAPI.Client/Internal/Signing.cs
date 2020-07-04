using System;
using System.Security.Cryptography;
using System.Text;

using JetBrains.Annotations;

using Newtonsoft.Json;


namespace Draftable.CompareAPI.Client.Internal
{
    internal static class Signing
    {
        private static readonly DateTime UnixEpoch = new DateTime(year: 1970, month: 1, day: 1, hour: 0, minute: 0, second: 0, millisecond: 0, kind: DateTimeKind.Utc);

        [Pure]
        public static int ValidUntilTimestamp(DateTime validUntil) => (int)(validUntil - UnixEpoch).TotalSeconds;

        [Pure, NotNull]
        public static string ViewerSignatureFor([NotNull] string accountId, [NotNull] string authToken, [NotNull] string identifier, int validUntilTimestamp)
        {
            var jsonPolicy = JsonConvert.SerializeObject(new object[] {
                accountId,
                identifier,
                validUntilTimestamp,
            });

            return HMACHexDigest(authToken, jsonPolicy.AssertNotNull());
        }

        #region HMACHexDigest

        [Pure, NotNull]
        private static string HMACHexDigest([NotNull] string key, [NotNull] string content) => Hexify(HMACDigest(key, content));

        [Pure, NotNull]
        private static byte[] HMACDigest([NotNull] string key, [NotNull] string content)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                return hmac.ComputeHash(Encoding.UTF8.GetBytes(content));
            }
        }

        [Pure, NotNull, CollectionAccess(CollectionAccessType.Read)]
        private static string Hexify([NotNull, InstantHandle] byte[] bytes)
        {
            const string hexDigits = "0123456789abcdef";
            var sb = new StringBuilder(2 * bytes.Length);
            foreach (var b in bytes)
            {
                sb.Append(hexDigits[b >> 4]);
                sb.Append(hexDigits[b & 0xF]);
            }
            return sb.ToString();
        }

        #endregion HMACHexDigest
    }
}
