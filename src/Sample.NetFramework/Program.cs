// ReSharper disable once RedundantUsingDirective

using System.Configuration;
using System.Net;

using Sample.Core;


namespace Sample.NetFramework
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

            /*
             * Disable certificate validation. This can be useful in dev or
             * test environments, but should *never* be used in production
             * deployments. Enabling this is ***COMPLETELY INSECURE***.
             */
            //ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            new ComparisonsSample(cloudAccountId, cloudAuthToken, selfHostedAccountId, selfHostedAuthToken,
                                  selfHostedBaseUrl).Run();
        }
    }
}
