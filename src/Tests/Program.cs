using System;
using System.Configuration;
using System.Diagnostics;
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
            RunComparison(SelfHostedAccountId, SelfHostedAuthToken, SelfHostedBaseUrl);
        }

        private static void RunComparisonInCloud()
        {
            RunComparison(CloudAccountId, CloudAuthToken, KnownURLs.CloudBaseURL);
        }

        private static void RunComparison(string accountId, string authToken, string compareServiceBaseUrl)
        {
            if (accountId == null)
            {
                throw new ArgumentException("AccountId must be configured to run the tests");
            }

            if (authToken == null)
            {
                throw new ArgumentException("AuthToken must be configured to run the tests");
            }
            
            using (var comparisons = new Comparisons(accountId, authToken, compareServiceBaseUrl))
            {
                var identifier = Comparisons.GenerateIdentifier();

                Process.Start(comparisons.SignedViewerURL(identifier, validFor: TimeSpan.FromMinutes(30), wait: true));

                var comparison = comparisons.Create(
                    Comparisons.Side.FromURL("https://api.draftable.com/static/test-documents/paper/left.pdf", "pdf"),
                    Comparisons.Side.FromURL("https://api.draftable.com/static/test-documents/paper/right.pdf", "pdf"),
                    identifier: identifier,
                    expires: TimeSpan.FromMinutes(30)
                );

                Console.WriteLine(comparison);
            }
        }
    }
}
