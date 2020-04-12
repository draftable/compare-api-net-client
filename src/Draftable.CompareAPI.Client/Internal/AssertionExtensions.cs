using System;
using System.Diagnostics;

using JetBrains.Annotations;


namespace Draftable.CompareAPI.Client.Internal
{
    internal static class AssertionExtensions
    {
        [Pure, NotNull, ContractAnnotation("value:null => halt")]
        public static T AssertNotNull<T>([CanBeNull] this T value)
            where T : class
        {
            #if DEBUG
            Debug.Assert(value != null, "Unexpected null value encountered.");
            #endif
            return value ?? throw new NullReferenceException("Unexpected null value encountered.");
        }
    }
}
