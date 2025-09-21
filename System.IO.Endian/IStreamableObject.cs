namespace System.IO.Endian
{
    public interface IStreamableObject
    {
        void PopulateFromStream(EndianReader reader, double? version, long origin);
        void WriteToStream(EndianWriter writer, double? version, long origin);
    }
}
