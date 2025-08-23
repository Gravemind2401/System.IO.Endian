namespace System.IO.Endian.SourceGenerator
{
    internal static class Extensions
    {
        //netstandard2.1 has these built in but apparently source generators need to use netstandard2.0
        public static bool StartsWith(this ReadOnlySpan<char> span, string value) => span.StartsWith(value.AsSpan());
        public static bool SequenceEqual(this ReadOnlySpan<char> span, string value) => span.SequenceEqual(value.AsSpan());
        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value) => (key, value) = (pair.Key, pair.Value);
    }
}