using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

using Draftable.CompareAPI.Client;


namespace Sample.Core
{
    public class ComparisonsSample
    {
        private const string SampleFileLeft = "left.pdf";
        private const string SampleFileRight = "right.pdf";

        private const string SampleUrlLeft = "https://api.draftable.com/static/test-documents/paper/left.pdf";
        private const string SampleUrlRight = "https://api.draftable.com/static/test-documents/paper/right.pdf";

        private readonly string _cloudAccountId;
        private readonly string _cloudAuthToken;

        private readonly string _selfHostedAccountId;
        private readonly string _selfHostedAuthToken;
        private readonly string _selfHostedBaseUrl;

        private readonly Action<HttpClientHandler> _clientConfig;

        public ComparisonsSample(string cloudAccountId,
                                 string cloudAuthToken,
                                 string selfHostedAccountId,
                                 string selfHostedAuthToken,
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
                RunComparisonCloudHosted();
                RunComparisonSelfHosted();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Exception running comparison:\n{e}");
                Environment.Exit(1);
            }
        }

        private void RunComparisonCloudHosted()
        {
            RunTestsCore("Cloud", _cloudAccountId, _cloudAuthToken, KnownURLs.CloudBaseURL);
        }

        private void RunComparisonSelfHosted()
        {
            if (string.IsNullOrEmpty(_selfHostedBaseUrl))
            {
                throw new ArgumentException("Draftable API Self-hosted base URL was not provided.");
            }

            RunTestsCore("ApiSh", _selfHostedAccountId, _selfHostedAuthToken, _selfHostedBaseUrl);
        }

        private void RunTestsCore(string label, string accountId, string authToken, string compareServiceBaseUrl)
        {
            if (string.IsNullOrEmpty(accountId))
            {
                throw new ArgumentException("AccountId was not provided.");
            }

            if (string.IsNullOrEmpty(authToken))
            {
                throw new ArgumentException("AuthToken was not provided.");
            }

            using (var client = new Comparisons(accountId, authToken, compareServiceBaseUrl, _clientConfig))
            {
                var comparisons = client.GetAll();
                Console.WriteLine($"[{label}] Initial comparisons count: {comparisons.Count}");

                var compareFromData = CreateComparison(label, client, CreateComparisonFromData);
                var compareFromUrls = CreateComparison(label, client, CreateComparisonFromUrls);
                comparisons = client.GetAll();
                Console.WriteLine($"[{label}] Comparisons count (Post-create): {comparisons.Count}");

                client.Delete(compareFromData);
                client.Delete(compareFromUrls);
                comparisons = client.GetAll();
                Console.WriteLine($"[{label}] Comparisons count (Post-delete): {comparisons.Count}");
            }
        }

        private static string CreateComparison(string label,
                                               Comparisons client,
                                               Func<Comparisons, Comparison> createCore)
        {
            label = $"[{label}]";
            var prefix = new string(' ', label.Length);

            var comparison = createCore(client);
            var cid = comparison.Identifier;

            Console.WriteLine($"{label} Comparison ID: {cid}");
            Console.WriteLine($"{prefix} isReady: {comparison.Ready}");
            Console.WriteLine($"{prefix} Public URL: {client.PublicViewerURL(cid)}");
            Console.WriteLine($"{prefix} Signed URL: {client.SignedViewerURL(cid)}");

            var timeoutCount = 0;
            while (!comparison.Ready)
            {
                if (timeoutCount > 20)
                {
                    throw new TimeoutException("Timeout exceeded while waiting for comparison to be ready.");
                }

                Task.Delay(1000).Wait();
                comparison = client.Get(cid);
                timeoutCount++;
            }

            Console.WriteLine($"{label} Comparison ID: {cid}");
            Console.WriteLine($"{prefix} isReady: {comparison.Ready}");
            Console.WriteLine($"{prefix} Failed: {HasFailed(comparison)}");
            Console.WriteLine($"{prefix} Error: {comparison.ErrorMessage}");

            return cid;
        }

        private static Comparison CreateComparisonFromData(Comparisons client)
        {
            var identifier = Comparisons.GenerateIdentifier();

            return client.Create(Comparisons.Side.FromFile(SampleFileLeft),
                                 Comparisons.Side.FromFile(SampleFileRight),
                                 identifier,
                                 expires: TimeSpan.FromMinutes(30));
        }

        private static Comparison CreateComparisonFromUrls(Comparisons client)
        {
            var identifier = Comparisons.GenerateIdentifier();

            return client.Create(Comparisons.Side.FromURL(SampleUrlLeft, Path.GetExtension(SampleUrlLeft)),
                                 Comparisons.Side.FromURL(SampleUrlRight, Path.GetExtension(SampleUrlRight)),
                                 identifier,
                                 expires: TimeSpan.FromMinutes(30));
        }

        private static string HasFailed(Comparison comparison) { return comparison.Failed == true ? "yes" : "no"; }
    }
}
