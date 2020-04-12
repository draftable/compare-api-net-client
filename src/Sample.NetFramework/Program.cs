using System.Configuration;

using Sample.Core;

namespace Tests
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var cloudAccountId = ConfigurationManager.AppSettings["cloudAccountId"];
            var cloudAuthToken = ConfigurationManager.AppSettings["cloudAuthToken"];
            var selfHostedAccountId = ConfigurationManager.AppSettings["selfHostedAccountId"];
            var selfHostedAuthToken = ConfigurationManager.AppSettings["selfHostedAuthToken"];
            var selfHostedBaseUrl = ConfigurationManager.AppSettings["selfHostedBaseUrl"];

            new ComparisonsSample(cloudAccountId, cloudAuthToken, selfHostedAccountId, selfHostedAuthToken, selfHostedBaseUrl).Run();
        }
    }
}
