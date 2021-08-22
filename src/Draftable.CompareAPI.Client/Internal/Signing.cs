using System;
using System.Security.Cryptography;
using System.Text;

using JetBrains.Annotations;

using Newtonsoft.Json;


namespace Draftable.CompareAPI.Client.Internal
{
    internal static class Signing
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        [Pure]
        public static int ValidUntilTimestamp(DateTime validUntil)
        {
            return (int)(validUntil - UnixEpoch).TotalSeconds;
        }

        [Pure]
        [NotNull]
        public static string ViewerSignatureFor([NotNull] string accountId,
                                                [NotNull] string authToken,
                                                [NotNull] string identifier,
                                                int validUntilTimestamp)
        {
            var jsonPolicy = JsonConvert.SerializeObject(new object[] {accountId, identifier, validUntilTimestamp});

            return HmacHexDigest(authToken, jsonPolicy.AssertNotNull());
        }

        [Pure]
        [NotNull]
        private static string HmacHexDigest([NotNull] string key, [NotNull] string content)
        {
            return Hexify(HmacDigest(key, content));
        }

        [Pure]
        [NotNull]
        private static byte[] HmacDigest([NotNull] string key, [NotNull] string content)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                return hmac.ComputeHash(Encoding.UTF8.GetBytes(content));
            }
        }

        [Pure]
        [NotNull]
        [CollectionAccess(CollectionAccessType.Read)]
        private static string Hexify([NotNull] [InstantHandle] byte[] bytes)
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
    }
}
