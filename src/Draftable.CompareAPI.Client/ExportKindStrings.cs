using System.Collections.Generic;


namespace Draftable.CompareAPI.Client
{
    public static class ExportKindStrings
    {
        public static string Resolve(ExportKind exportKind) => Dictionary[exportKind];

        private static readonly Dictionary<ExportKind, string> Dictionary = new Dictionary<ExportKind, string>
        {
            {ExportKind.SinglePage, "single_page"},
            {ExportKind.Combined, "combined"},
            {ExportKind.Left, "left"},
            {ExportKind.Right, "right"},
        };
    }
}
