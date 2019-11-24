using System;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Draftable.CompareAPI.Client;

namespace Tests
{
    internal static class Program
    {
        private static readonly string CloudAccountId = ConfigurationManager.AppSettings["cloudAccountId"];
        private static readonly string CloudAuthToken = ConfigurationManager.AppSettings["cloudAuthToken"];
        private static readonly string SelfHostedAccountId = ConfigurationManager.AppSettings["selfHostedAccountId"];
        private static readonly string SelfHostedAuthToken = ConfigurationManager.AppSettings["selfHostedAuthToken"];
        private static readonly string SelfHostedBaseUrl = ConfigurationManager.AppSettings["selfHostedBaseUrl"];

        private static void Main(string[] args)
        {
            try
            {
               RunComparisonInCloud();
               RunComparisonWithSelfHosted();
            }
            catch (Exception e)
            {
                Console.WriteLine("Failure when trying to run a comparison");
                Console.WriteLine(e);
            }
        }

        private static void RunComparisonWithSelfHosted()
        {
            if (string.IsNullOrEmpty(SelfHostedBaseUrl))
            {
                throw new ArgumentException("To continue, you must specify Self-Hosted base URL");
            }

            // Run this line to ignore SSL certificate validation (but be careful with that, it should NEVER be done in production).
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            RunTestsCore("SELF-HOSTED", SelfHostedAccountId, SelfHostedAuthToken, SelfHostedBaseUrl);
        }

        private static void RunComparisonInCloud()
        {
            RunTestsCore("CLOUD", CloudAccountId, CloudAuthToken, KnownURLs.CloudBaseURL);
        }

        private static void RunTestsCore(string label, string accountId, string authToken, string compareServiceBaseUrl)
        {
            if (string.IsNullOrEmpty(accountId))
            {
                throw new ArgumentException("AccountId must be configured to run the tests");
            }

            if (string.IsNullOrEmpty(authToken))
            {
                throw new ArgumentException("AuthToken must be configured to run the tests");
            }

            using (var client = new Comparisons(accountId, authToken, compareServiceBaseUrl))
            {
                var list = client.GetAll();
                var count1 = list.Count;
                Console.WriteLine($"[{label}] Comparisons count: {count1}");

                var newId1 = CreateComparison(label, client, CreateComparisonFromUrls);
                var newId2 = CreateComparison(label, client, CreateComparisonFromData);

                var count2 = client.GetAll().Count;
                Console.WriteLine($"[{label}] Comparisons count: {count2}");

                client.Delete(newId1);
                client.Delete(newId2);
                var count3 = client.GetAll().Count;
                Console.WriteLine($"[{label}] After delete, count: {count3}");
            }
        }

        private static string CreateComparison(string label, Comparisons client, Func<Comparisons, Comparison> createCore)
        {
            var newComparison = createCore(client);
            var newId = newComparison.Identifier;
            Console.WriteLine($"[{label}] New comparison: {newId}, isReady: {newComparison.Ready}, " +
                              $"public url: {client.PublicViewerURL(newId)}, signed url: {client.SignedViewerURL(newId)}");

            var timeoutCount = 0;
            while (!newComparison.Ready)
            {
                if (timeoutCount > 20)
                {
                    throw new TimeoutException("Timeout exceeded while waiting for comparison to get ready");
                }
                Task.Delay(1000).Wait();
                newComparison = client.Get(newId);
                timeoutCount++;
            }

            Console.WriteLine($"[{label}] Retrieved again: {newId}, isReady: {newComparison.Ready}, " +
                              $"has failed: {HasFailed(newComparison)}, error message: {newComparison.ErrorMessage}");
            return newId;
        }

        private static string HasFailed(Comparison newComparison)
        {
            return newComparison.Failed == true ? "yes" : "no";
        }

        private static Comparison CreateComparisonFromUrls(Comparisons comparisons)
        {
            var identifier = Comparisons.GenerateIdentifier();

            return comparisons.Create(
                Comparisons.Side.FromURL("https://api.draftable.com/static/test-documents/paper/left.pdf", "pdf"),
                Comparisons.Side.FromURL("https://api.draftable.com/static/test-documents/paper/right.pdf", "pdf"),
                identifier: identifier,
                expires: TimeSpan.FromMinutes(30)
            );
        }

        private static Comparison CreateComparisonFromData(Comparisons comparisons)
        {
            var identifier = Comparisons.GenerateIdentifier();

            // TODO provide proper file urls
            return comparisons.Create(
                Comparisons.Side.FromFile(@"C:\draftable\testing\old.pdf"),
                Comparisons.Side.FromFile(@"C:\draftable\testing\new.pdf"),
                identifier: identifier,
                expires: TimeSpan.FromMinutes(30)
            );
        }
    }
}
