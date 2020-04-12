using System.Net.Http;

using Sample.Core;

namespace Sample.NetCore
{
    class Program
    {
        static void Main(string[] args)
        {
            // TODO - PROVIDE PROPER VALUES
            var cloudAccountId = "";
            var cloudAuthToken = "";
            var selfHostedAccountId = "";
            var selfHostedAuthToken = "";
            var selfHostedBaseUrl = "";

            new ComparisonsSample(cloudAccountId, cloudAuthToken, selfHostedAccountId, selfHostedAuthToken, selfHostedBaseUrl, ConfigureHttpHandler).Run();
        }

        /*
         * This way you are able to bypass SSL certificate check when using .NET CORE.
         * Be careful though, such bypass should NEVER be used in real-world prod environments.
         * In such situations, just properly install the SSL certificate.
         */
        private static void ConfigureHttpHandler(HttpClientHandler httpClientHandler)
        {
            httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        }
    }
}
