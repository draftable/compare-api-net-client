using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Draftable.CompareAPI.Client;
using JetBrains.Annotations;


namespace Tests
{
    internal static class Program
    {
        // ReSharper disable AssignNullToNotNullAttribute
        [NotNull] private static readonly string AccountId = ConfigurationManager.AppSettings["accountId"];
        [NotNull] private static readonly string AuthToken = ConfigurationManager.AppSettings["authToken"];
        // ReSharper restore AssignNullToNotNullAttribute

        private static void Main(string[] args)
        {
            using (var comparisons = new Comparisons(AccountId, AuthToken)) {
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
            
            Console.ReadLine();
        }
    }
}
