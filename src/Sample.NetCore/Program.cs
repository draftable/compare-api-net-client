using System.Net.Http;

using Sample.Core;


namespace Sample.NetCore
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            const string cloudAccountId = "vCOGnu-test";
            const string cloudAuthToken = "ab8c6d575a49cc4f728167140f1b3d0e";

            const string selfHostedAccountId = "";
            const string selfHostedAuthToken = "";
            const string selfHostedBaseUrl = "";

            /*
             * Replace null with "ConfigureHttpHandler" to disable certificate
             * validation (see comments in the referenced method below).
             */
            new ComparisonsSample(cloudAccountId, cloudAuthToken, selfHostedAccountId, selfHostedAuthToken,
                                  selfHostedBaseUrl, null).Run();
        }

        private static void ConfigureHttpHandler(HttpClientHandler httpClientHandler)
        {
            /*
             * Disable certificate validation. This can be useful in dev or
             * test environments, but should *never* be used in production
             * deployments. Enabling this is ***COMPLETELY INSECURE***.
             */
            httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        }
    }
}
