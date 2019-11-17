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
            
            using (var comparisons = new Comparisons(accountId, authToken, compareServiceBaseUrl))
            {
                var list = comparisons.GetAll();
                var count1 = list.Count;
                Console.WriteLine($"[{label}] Comparisons count: {count1}");
                
                var newComparison = CreateSampleComparison(comparisons);
                var newId = newComparison.Identifier;
                Console.WriteLine($"[{label}] New comparison: {newId}, isReady: {newComparison.Ready}, " +
                                  $"public url: {comparisons.PublicViewerURL(newId)}, signed url: {comparisons.SignedViewerURL(newId)}");

                Thread.Sleep(3000); // wait for a comparison to run to completion
                
                var comparisonAgain = comparisons.Get(newId);
                Console.WriteLine($"[{label}] Retrieved again: {newId}, isReady: {comparisonAgain.Ready}, " +
                                  $"has failed: {HasFailed(comparisonAgain)}, error message: {comparisonAgain.ErrorMessage}");
                
                
                var count2 = comparisons.GetAll().Count;
                Console.WriteLine($"[{label}] Comparisons count: {count2}");

                comparisons.Delete(newId);
                var count3 = comparisons.GetAll().Count;
                Console.WriteLine($"[{label}] After delete, count: {count3}");
            }
        }

        private static string HasFailed(Comparison newComparison)
        {
            return newComparison.Failed == true ? "yes" : "no";
        }

        private static Comparison CreateSampleComparison(Comparisons comparisons)
        {
            var identifier = Comparisons.GenerateIdentifier();
            
            return comparisons.Create(
                Comparisons.Side.FromURL("https://api.draftable.com/static/test-documents/paper/left.pdf", "pdf"),
                Comparisons.Side.FromURL("https://api.draftable.com/static/test-documents/paper/right.pdf", "pdf"),
                identifier: identifier,
                expires: TimeSpan.FromMinutes(30)
            );
        }
    }
}
