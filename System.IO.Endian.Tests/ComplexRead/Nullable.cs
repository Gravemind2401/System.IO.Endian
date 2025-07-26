﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace System.IO.Endian.Tests.ComplexRead
{
    public partial class ComplexRead
    {
        [DataTestMethod]
        [DataRow(ByteOrder.LittleEndian)]
        [DataRow(ByteOrder.BigEndian)]
        public void Nullable01(ByteOrder order)
        {
            var rng = new Random();
            using (var stream = new MemoryStream(new byte[500]))
            using (var reader = new EndianReader(stream, order))
            using (var writer = new EndianWriter(stream, order))
            {
                var rand = new object[11];

                rand[0] = (sbyte)rng.Next(sbyte.MinValue, sbyte.MaxValue);
                writer.Seek(0x00, SeekOrigin.Begin);
                writer.Write((sbyte)rand[0]);

                rand[1] = (short)rng.Next(short.MinValue, short.MaxValue);
                writer.Seek(0x10, SeekOrigin.Begin);
                writer.Write((short)rand[1]);

                rand[2] = rng.Next(int.MinValue, int.MaxValue);
                writer.Seek(0x20, SeekOrigin.Begin);
                writer.Write((int)rand[2]);

                rand[3] = (long)rng.Next(int.MinValue, int.MaxValue);
                writer.Seek(0x30, SeekOrigin.Begin);
                writer.Write((long)rand[3]);

                rand[4] = (byte)rng.Next(byte.MinValue, byte.MaxValue);
                writer.Seek(0x40, SeekOrigin.Begin);
                writer.Write((byte)rand[4]);

                rand[5] = (ushort)rng.Next(ushort.MinValue, ushort.MaxValue);
                writer.Seek(0x50, SeekOrigin.Begin);
                writer.Write((ushort)rand[5]);

                rand[6] = unchecked((uint)rng.Next(int.MinValue, int.MaxValue));
                writer.Seek(0x60, SeekOrigin.Begin);
                writer.Write((uint)rand[6]);

                rand[7] = (ulong)unchecked((uint)rng.Next(int.MinValue, int.MaxValue));
                writer.Seek(0x70, SeekOrigin.Begin);
                writer.Write((ulong)rand[7]);

                rand[8] = (float)rng.NextDouble();
                writer.Seek(0x80, SeekOrigin.Begin);
                writer.Write((float)rand[8]);

                rand[9] = rng.NextDouble();
                writer.Seek(0x90, SeekOrigin.Begin);
                writer.Write((double)rand[9]);

                rand[10] = Guid.NewGuid();
                writer.Seek(0xA0, SeekOrigin.Begin);
                writer.Write((Guid)rand[10]);

                stream.Position = 0;
                var obj = reader.ReadObject<DataClass07>();

                Assert.AreEqual(0xFF, stream.Position);
                Assert.AreEqual(obj.Property1, rand[0]);
                Assert.AreEqual(obj.Property2, rand[1]);
                Assert.AreEqual(obj.Property3, rand[2]);
                Assert.AreEqual(obj.Property4, rand[3]);
                Assert.AreEqual(obj.Property5, rand[4]);
                Assert.AreEqual(obj.Property6, rand[5]);
                Assert.AreEqual(obj.Property7, rand[6]);
                Assert.AreEqual(obj.Property8, rand[7]);
                Assert.AreEqual(obj.Property9, rand[8]);
                Assert.AreEqual(obj.Property10, rand[9]);
            }
        }

        [DataTestMethod]
        [DataRow(ByteOrder.LittleEndian)]
        [DataRow(ByteOrder.BigEndian)]
        public void Nullable02(ByteOrder order)
        {
            var rng = new Random();
            using (var stream = new MemoryStream(new byte[500]))
            using (var reader = new EndianReader(stream, order))
            using (var writer = new EndianWriter(stream, order))
            {
                var rand = new object[11];

                rand[0] = (sbyte)rng.Next(sbyte.MinValue, sbyte.MaxValue);
                writer.Seek(0x70, SeekOrigin.Begin);
                writer.Write((sbyte)rand[0]);

                rand[1] = (short)rng.Next(short.MinValue, short.MaxValue);
                writer.Seek(0x40, SeekOrigin.Begin);
                writer.Write((short)rand[1]);

                rand[2] = rng.Next(int.MinValue, int.MaxValue);
                writer.Seek(0x30, SeekOrigin.Begin);
                writer.Write((int)rand[2]);

                rand[3] = (long)rng.Next(int.MinValue, int.MaxValue);
                writer.Seek(0x10, SeekOrigin.Begin);
                writer.Write((long)rand[3]);

                rand[4] = (byte)rng.Next(byte.MinValue, byte.MaxValue);
                writer.Seek(0x90, SeekOrigin.Begin);
                writer.Write((byte)rand[4]);

                rand[5] = (ushort)rng.Next(ushort.MinValue, ushort.MaxValue);
                writer.Seek(0xA0, SeekOrigin.Begin);
                writer.Write((ushort)rand[5]);

                rand[6] = unchecked((uint)rng.Next(int.MinValue, int.MaxValue));
                writer.Seek(0x00, SeekOrigin.Begin);
                writer.Write((uint)rand[6]);

                rand[7] = (ulong)unchecked((uint)rng.Next(int.MinValue, int.MaxValue));
                writer.Seek(0x80, SeekOrigin.Begin);
                writer.Write((ulong)rand[7]);

                rand[8] = (float)rng.NextDouble();
                writer.Seek(0x20, SeekOrigin.Begin);
                writer.Write((float)rand[8]);

                rand[9] = rng.NextDouble();
                writer.Seek(0x50, SeekOrigin.Begin);
                writer.Write((double)rand[9]);

                rand[10] = Guid.NewGuid();
                writer.Seek(0x60, SeekOrigin.Begin);
                writer.Write((Guid)rand[10]);

                stream.Position = 0;
                var obj = reader.ReadObject<DataClass08>();

                //the highest offset should always be read last
                //so if no size is specified the position should end
                //up at the highest offset + the size of the property
                Assert.AreEqual(0xA2, stream.Position);
                Assert.AreEqual(obj.Property1, rand[0]);
                Assert.AreEqual(obj.Property2, rand[1]);
                Assert.AreEqual(obj.Property3, rand[2]);
                Assert.AreEqual(obj.Property4, rand[3]);
                Assert.AreEqual(obj.Property5, rand[4]);
                Assert.AreEqual(obj.Property6, rand[5]);
                Assert.AreEqual(obj.Property7, rand[6]);
                Assert.AreEqual(obj.Property8, rand[7]);
                Assert.AreEqual(obj.Property9, rand[8]);
                Assert.AreEqual(obj.Property10, rand[9]);
            }
        }

        [FixedSize(0xFF)]
        public class DataClass07
        {
            [Offset(0x00)]
            public sbyte? Property1 { get; set; }

            [Offset(0x10)]
            public short? Property2 { get; set; }

            [Offset(0x20)]
            public int? Property3 { get; set; }

            [Offset(0x30)]
            public long? Property4 { get; set; }

            [Offset(0x40)]
            public byte? Property5 { get; set; }

            [Offset(0x50)]
            public ushort? Property6 { get; set; }

            [Offset(0x60)]
            public uint? Property7 { get; set; }

            [Offset(0x70)]
            public ulong? Property8 { get; set; }

            [Offset(0x80)]
            public float? Property9 { get; set; }

            [Offset(0x90)]
            public double? Property10 { get; set; }

            [Offset(0xA0)]
            public Guid? Property11 { get; set; }
        }

        public class DataClass08
        {
            [Offset(0x70)]
            public sbyte? Property1 { get; set; }

            [Offset(0x40)]
            public short? Property2 { get; set; }

            [Offset(0x30)]
            public int? Property3 { get; set; }

            [Offset(0x10)]
            public long? Property4 { get; set; }

            [Offset(0x90)]
            public byte? Property5 { get; set; }

            [Offset(0xA0)]
            public ushort? Property6 { get; set; }

            [Offset(0x00)]
            public uint? Property7 { get; set; }

            [Offset(0x80)]
            public ulong? Property8 { get; set; }

            [Offset(0x20)]
            public float? Property9 { get; set; }

            [Offset(0x50)]
            public double? Property10 { get; set; }

            [Offset(0x60)]
            public Guid? Property11 { get; set; }
        }
    }
}
