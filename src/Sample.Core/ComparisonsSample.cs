using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Draftable.CompareAPI.Client;

namespace Sample.Core
{
    public class ComparisonsSample
    {
        private readonly string _cloudAccountId;
        private readonly string _cloudAuthToken;
        private readonly string _selfHostedAccountId;
        private readonly string _selfHostedAuthToken;
        private readonly string _selfHostedBaseUrl;
        private readonly Action<HttpClientHandler> _clientConfig;

        public ComparisonsSample(
            string cloudAccountId, string cloudAuthToken,
            string selfHostedAccountId, string selfHostedAuthToken,
            string selfHostedBaseUrl,
            Action<HttpClientHandler> clientConfig = null)
        {
            _cloudAccountId = cloudAccountId;
            _cloudAuthToken = cloudAuthToken;
            _selfHostedAccountId = selfHostedAccountId;
            _selfHostedAuthToken = selfHostedAuthToken;
            _selfHostedBaseUrl = selfHostedBaseUrl;
            _clientConfig = clientConfig;
        }

        public void Run()
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

        private void RunComparisonWithSelfHosted()
        {
            if (string.IsNullOrEmpty(_selfHostedBaseUrl))
            {
                throw new ArgumentException("To continue, you must specify Self-Hosted base URL");
            }

            // Run this line to ignore SSL certificate validation (but be careful with that, it should NEVER be done in production).
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            RunTestsCore("SELF-HOSTED", _selfHostedAccountId, _selfHostedAuthToken, _selfHostedBaseUrl);
        }

        private void RunComparisonInCloud()
        {
            RunTestsCore("CLOUD", _cloudAccountId, _cloudAuthToken, KnownURLs.CloudBaseURL);
        }

        private void RunTestsCore(string label, string accountId, string authToken, string compareServiceBaseUrl)
        {
            if (string.IsNullOrEmpty(accountId))
            {
                throw new ArgumentException("AccountId must be configured to run the tests");
            }

            if (string.IsNullOrEmpty(authToken))
            {
                throw new ArgumentException("AuthToken must be configured to run the tests");
            }

            using (var client = new Comparisons(accountId, authToken, compareServiceBaseUrl, _clientConfig))
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

        private string CreateComparison(string label, Comparisons client, Func<Comparisons, Comparison> createCore)
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

        private string HasFailed(Comparison newComparison)
        {
            return newComparison.Failed == true ? "yes" : "no";
        }

        private Comparison CreateComparisonFromUrls(Comparisons comparisons)
        {
            var identifier = Comparisons.GenerateIdentifier();

            return comparisons.Create(
                Comparisons.Side.FromURL("https://api.draftable.com/static/test-documents/paper/left.pdf", "pdf"),
                Comparisons.Side.FromURL("https://api.draftable.com/static/test-documents/paper/right.pdf", "pdf"),
                identifier: identifier,
                expires: TimeSpan.FromMinutes(30)
            );
        }

        private Comparison CreateComparisonFromData(Comparisons comparisons)
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
