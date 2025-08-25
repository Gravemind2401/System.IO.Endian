using Microsoft.CodeAnalysis;

namespace System.IO.Endian.SourceGenerator
{
    internal static class AttributeHelpers
    {
        public static void GetVersionArgs(this AttributeData attribute, out double? minVersion, out double? maxVersion)
        {
            minVersion = maxVersion = default;

            foreach (var (name, value) in attribute.NamedArguments)
            {
                if (name == nameof(VersionedAttributeData.MinVersion))
                    minVersion = (double)value.Value!;
                else if (name == nameof(VersionedAttributeData.MaxVersion))
                    maxVersion = (double)value.Value!;
            }
        }
    }

    internal abstract record VersionedAttributeData(double? MinVersion, double? MaxVersion)
    {
        public bool IsVersioned => MinVersion.HasValue || MaxVersion.HasValue;

        public bool ValidForVersion(double? version)
        {
            if (MinVersion.HasValue && MaxVersion.HasValue)
                return version >= MinVersion && version < MaxVersion;
            else if (MinVersion.HasValue || MaxVersion.HasValue)
                return version >= MinVersion || version < MaxVersion;
            else
                return true;
        }
    }

    internal sealed record FixedSizeAttributeData(long Size, double? MinVersion, double? MaxVersion) : VersionedAttributeData(MinVersion, MaxVersion);
    internal sealed record ByteOrderAttributeData(int ByteOrder, double? MinVersion, double? MaxVersion) : VersionedAttributeData(MinVersion, MaxVersion);
    internal sealed record OffsetAttributeData(long Offset, double? MinVersion, double? MaxVersion) : VersionedAttributeData(MinVersion, MaxVersion);
    internal sealed record StoreTypeAttributeData(ITypeSymbol StoreType, double? MinVersion, double? MaxVersion) : VersionedAttributeData(MinVersion, MaxVersion);

    internal sealed record StringAttributeInfo(bool IsInterned, bool IsLengthPrefixed, NullTerminatedAttributeData? NullTerminatedAttributeData, FixedLengthAttributeData? FixedLengthAttributeData)
    {
        public bool IsValid => IsLengthPrefixed ^ (NullTerminatedAttributeData != null) ^ (FixedLengthAttributeData != null);
    }

    internal sealed record NullTerminatedAttributeData(int? Length);
    internal sealed record FixedLengthAttributeData(int Length, bool Trim, char Padding);
}
