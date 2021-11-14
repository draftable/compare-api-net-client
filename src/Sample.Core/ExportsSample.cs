using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

using Draftable.CompareAPI.Client;

using Newtonsoft.Json;


namespace Sample.Core
{
    public class ExportsSample
    {
        /*
         * Before this is run, check the local file paths carefully.
         * You'll need to provide your account name and authentication token as well.
         */

        private const string Account = "";
        private const string Token = "";
        private const string ExportsDir = @"";
        private const string CompareIdsFile = @"";

        public void Run()
        {
            try
            {
                var filePairs = BuildInputPairs();
                var comparisonsCreated = CreateComparisons(filePairs);

                using (var exportClient = new Exports(Token, KnownURLs.CloudBaseURL, null))
                {
                    // tuple here is: comparisonId,mode,exportId
                    var exportInfos =
                        StartExportsAndBuildInfosList(comparisonsCreated.Select(c => c.Identifier), exportClient);
                    foreach (var exportInfo in exportInfos)
                    {
                        var exportRetrieved = exportClient.Get(exportInfo.Item3);
                        var outputPath = Path.Combine(ExportsDir, $"{exportInfo.Item1}_{exportInfo.Item2}.pdf");
                        if (string.IsNullOrEmpty(exportRetrieved.Url))
                        {
                            throw new InvalidOperationException(
                                "At this point, the export must have  the url assigned");
                        }

                        using (var client = new WebClient())
                        {
                            client.DownloadFile(exportRetrieved.Url, outputPath);
                        }
                    }
                }

                return;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private static List<Tuple<string, string, string>> StartExportsAndBuildInfosList(IEnumerable<string> compareIds,
            Exports exportClient)
        {
            var allModes = new[] {ExportKinds.Left, ExportKinds.Right, ExportKinds.SinglePage, ExportKinds.Combined};
            var exportInfos = new List<Tuple<string, string, string>>();
            foreach (var cid in compareIds)
            {
                foreach (var mode in allModes)
                {
                    var newExport = exportClient.Create(cid, mode);
                    exportInfos.Add(new Tuple<string, string, string>(cid, mode, newExport.Identifier));
                }
            }

            File.WriteAllText(@"C:\draftable\exports\export-infos.txt", JsonConvert.SerializeObject(exportInfos));
            return exportInfos;
        }

        private static List<Comparison> CreateComparisons(Tuple<string, string>[] filePairs)
        {
            var comparisonsCreated = new List<Comparison>();

            using (var comparisons = new Comparisons(Account, Token, KnownURLs.CloudBaseURL, null))
            {
                foreach (var pair in filePairs)
                {
                    var identifier = Comparisons.GenerateIdentifier();
                    var newComparison = comparisons.Create(Comparisons.Side.FromFile(pair.Item1),
                                                           Comparisons.Side.FromFile(pair.Item2),
                                                           identifier,
                                                           expires: TimeSpan.FromMinutes(100));
                    comparisonsCreated.Add(newComparison);
                }
            }

            var ccIdsJoined = string.Join(Environment.NewLine, comparisonsCreated.Select(c => c.Identifier));
            File.WriteAllText(CompareIdsFile, ccIdsJoined);
            return comparisonsCreated;
        }

        private static Tuple<string, string>[] BuildInputPairs()
        {
            var basicPath = @"C:\draftable\exports\";
            var filePairs = new[]
                {
                    new Tuple<string, string>("equations-1.pdf", "equations-2.pdf"),
                    new Tuple<string, string>("rotated-left.pdf", "rotated-right.pdf"),
                    new Tuple<string, string>("eq2-left.pdf", "eq2-right.pdf")
                }.Select(t => new Tuple<string, string>(basicPath + t.Item1, basicPath + t.Item2))
                 .ToArray();
            return filePairs;
        }
    }
}
