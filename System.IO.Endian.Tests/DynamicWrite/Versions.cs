﻿namespace System.IO.Endian.Tests.DynamicWrite
{
    public partial class DynamicWrite
    {
        [DataTestMethod]
        [DataRow(ByteOrder.LittleEndian)]
        [DataRow(ByteOrder.BigEndian)]
        public void Attributes_Versions01(ByteOrder order)
        {
            Versions01<VersionedClass01>(order);
        }

        [DataTestMethod]
        [DataRow(ByteOrder.LittleEndian)]
        [DataRow(ByteOrder.BigEndian)]
        public void Attributes_Versions02(ByteOrder order)
        {
            Versions02<VersionedClass02b>(order);
        }

        [DataTestMethod]
        [DataRow(ByteOrder.LittleEndian)]
        [DataRow(ByteOrder.BigEndian)]
        public void Attributes_Versions03(ByteOrder order)
        {
            Versions03<VersionedClass03>(order);
        }

        [DataTestMethod]
        [DataRow(ByteOrder.LittleEndian)]
        [DataRow(ByteOrder.BigEndian)]
        public void Attributes_Versions04(ByteOrder order)
        {
            Versions04<VersionedClass04>(order);
        }

        [DataTestMethod]
        [DataRow(ByteOrder.LittleEndian)]
        [DataRow(ByteOrder.BigEndian)]
        public void Attributes_Versions05(ByteOrder order)
        {
            Versions05<VersionedClass05>(order);
        }

        [DataTestMethod]
        [DataRow(ByteOrder.LittleEndian)]
        [DataRow(ByteOrder.BigEndian)]
        public void Builder_Versions01(ByteOrder order)
        {
            Versions01<VersionedClass01_Builder>(order);
        }

        [DataTestMethod]
        [DataRow(ByteOrder.LittleEndian)]
        [DataRow(ByteOrder.BigEndian)]
        public void Builder_Versions02(ByteOrder order)
        {
            Versions02<VersionedClass02b_Builder>(order);
        }

        [DataTestMethod]
        [DataRow(ByteOrder.LittleEndian)]
        [DataRow(ByteOrder.BigEndian)]
        public void Builder_Versions03(ByteOrder order)
        {
            Versions03<VersionedClass03_Builder>(order);
        }

        [DataTestMethod]
        [DataRow(ByteOrder.LittleEndian)]
        [DataRow(ByteOrder.BigEndian)]
        public void Builder_Versions04(ByteOrder order)
        {
            Versions04<VersionedClass04_Builder>(order);
        }

        [DataTestMethod]
        [DataRow(ByteOrder.LittleEndian)]
        [DataRow(ByteOrder.BigEndian)]
        public void Builder_Versions05(ByteOrder order)
        {
            Versions05<VersionedClass05_Builder>(order);
        }

        private static void Versions01<T>(ByteOrder order)
            where T : VersionedClass01, new()
        {
            var rng = new Random();
            var obj = new T
            {
                Version = 1,
                Property1 = rng.Next(int.MinValue, int.MaxValue),
                Property2 = (float)rng.NextDouble(),
                Property3 = (float)rng.NextDouble(),
                Property4 = rng.NextDouble()
            };

            using (var stream = new MemoryStream())
            using (var reader = new EndianReader(stream, order))
            using (var writer = new EndianWriter(stream, order))
            {
                writer.WriteObject(obj);

                stream.Position = 0;
                Assert.AreEqual(obj.Property1, reader.ReadInt32());
                Assert.AreEqual(obj.Version, reader.ReadInt32());
                Assert.AreEqual(obj.Property2, reader.ReadSingle());
                Assert.IsTrue(reader.ReadBytes(64).All(b => b == 0));

                stream.Position = 0;
                writer.Write(new byte[64]); //set to zeros

                stream.Position = 0;
                obj.Version = 2;
                writer.WriteObject(obj);

                stream.Position = 0;
                Assert.AreEqual(obj.Property1, reader.ReadInt32());
                Assert.AreEqual(obj.Version, reader.ReadInt32());
                Assert.AreEqual(0, reader.ReadInt32());
                Assert.AreEqual(obj.Property2, reader.ReadSingle());
                Assert.AreEqual(obj.Property3, reader.ReadSingle());
                Assert.IsTrue(reader.ReadBytes(64).All(b => b == 0));

                stream.Position = 0;
                writer.Write(new byte[64]); //set to zeros

                stream.Position = 0;
                obj.Version = 3;
                writer.WriteObject(obj);

                stream.Position = 0;
                Assert.AreEqual(obj.Property1, reader.ReadInt32());
                Assert.AreEqual(obj.Version, reader.ReadInt32());
                Assert.AreEqual(0, reader.ReadInt32());
                Assert.AreEqual(obj.Property2, reader.ReadSingle());
                Assert.AreEqual(obj.Property3, reader.ReadSingle());
                Assert.IsTrue(reader.ReadBytes(64).All(b => b == 0));

                stream.Position = 0;
                writer.Write(new byte[64]); //set to zeros

                stream.Position = 0;
                obj.Version = 4;
                writer.WriteObject(obj);

                stream.Position = 0;
                Assert.AreEqual(obj.Property1, reader.ReadInt32());
                Assert.AreEqual(obj.Version, reader.ReadInt32());
                Assert.AreEqual(0, reader.ReadInt32());
                Assert.AreEqual(obj.Property2, reader.ReadSingle());
                Assert.AreEqual(0, reader.ReadInt32());
                Assert.AreEqual(obj.Property4, reader.ReadDouble());
            }
        }

        private static void Versions02<T>(ByteOrder order)
            where T : VersionedClass02b, new()
        {
            var rng = new Random();
            var obj = new T
            {
                Version = 0,
                Property1 = rng.Next(int.MinValue, int.MaxValue),
                Property2 = (float)rng.NextDouble(),
                Property3 = (float)rng.NextDouble(),
                Property4 = rng.NextDouble()
            };

            using (var stream = new MemoryStream())
            using (var reader = new EndianReader(stream, order))
            using (var writer = new EndianWriter(stream, order))
            {
                writer.WriteObject(obj, 1);

                stream.Position = 0;
                Assert.AreEqual(obj.Property1, reader.ReadInt32());
                Assert.AreEqual(1, reader.ReadInt32()); //version in stream must match version used to write
                Assert.AreEqual(obj.Property2, reader.ReadSingle());
                Assert.IsTrue(reader.ReadBytes(64).All(b => b == 0));

                stream.Position = 0;
                writer.Write(new byte[64]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 2);

                stream.Position = 0;
                Assert.AreEqual(obj.Property1, reader.ReadInt32());
                Assert.AreEqual(2, reader.ReadInt32()); //version in stream must match version used to write
                Assert.AreEqual(0, reader.ReadInt32());
                Assert.AreEqual(obj.Property2, reader.ReadSingle());
                Assert.AreEqual(obj.Property3, reader.ReadSingle());
                Assert.IsTrue(reader.ReadBytes(64).All(b => b == 0));

                stream.Position = 0;
                writer.Write(new byte[64]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 3);

                stream.Position = 0;
                Assert.AreEqual(obj.Property1, reader.ReadInt32());
                Assert.AreEqual(3, reader.ReadInt32()); //version in stream must match version used to write
                Assert.AreEqual(0, reader.ReadInt32());
                Assert.AreEqual(obj.Property2, reader.ReadSingle());
                Assert.AreEqual(obj.Property3, reader.ReadSingle());
                Assert.IsTrue(reader.ReadBytes(64).All(b => b == 0));

                stream.Position = 0;
                writer.Write(new byte[64]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 4);

                stream.Position = 0;
                Assert.AreEqual(obj.Property1, reader.ReadInt32());
                Assert.AreEqual(4, reader.ReadInt32()); //version in stream must match version used to write
                Assert.AreEqual(0, reader.ReadInt32());
                Assert.AreEqual(obj.Property2, reader.ReadSingle());
                Assert.AreEqual(0, reader.ReadInt32());
                Assert.AreEqual(obj.Property4, reader.ReadDouble());
            }
        }

        private static void Versions03<T>(ByteOrder order)
            where T : VersionedClass03, new()
        {
            var rng = new Random();
            var obj = new T
            {
                Property1 = rng.Next(int.MinValue, int.MaxValue)
            };

            using (var stream = new MemoryStream())
            using (var reader = new EndianReader(stream, order))
            using (var writer = new EndianWriter(stream, order))
            {
                writer.WriteObject(obj, 0);

                Assert.AreEqual(0x20, stream.Position);
                reader.Seek(0x08, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1, reader.ReadInt32());

                stream.Position = 0;
                writer.Write(new byte[0x50]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 1);

                Assert.AreEqual(0x20, stream.Position);
                reader.Seek(0x08, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1, reader.ReadInt32());

                stream.Position = 0;
                writer.Write(new byte[0x50]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 2);

                Assert.AreEqual(0x20, stream.Position);
                reader.Seek(0x18, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1, reader.ReadInt32());

                stream.Position = 0;
                writer.Write(new byte[0x50]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 3);

                Assert.AreEqual(0x30, stream.Position);
                reader.Seek(0x18, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1, reader.ReadInt32());

                stream.Position = 0;
                writer.Write(new byte[0x50]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 4);

                Assert.AreEqual(0x30, stream.Position);
                reader.Seek(0x28, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1, reader.ReadInt32());

                stream.Position = 0;
                writer.Write(new byte[0x50]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 5);

                Assert.AreEqual(0x40, stream.Position);
                reader.Seek(0x28, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1, reader.ReadInt32());

                stream.Position = 0;
                writer.Write(new byte[0x50]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 6);

                Assert.AreEqual(0x40, stream.Position);
                reader.Seek(0x28, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1, reader.ReadInt32());
            }
        }

        private static void Versions04<T>(ByteOrder order)
            where T : VersionedClass04, new()
        {
            var rng = new Random();
            var obj = new T
            {
                Property1 = rng.Next(int.MinValue, int.MaxValue)
            };

            using (var stream = new MemoryStream())
            using (var reader = new EndianReader(stream, order))
            using (var writer = new EndianWriter(stream, order))
            {
                writer.WriteObject(obj, 0);

                Assert.AreEqual(0x20, stream.Position);
                reader.Seek(0x10, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1, reader.ReadInt32());

                stream.Position = 0;
                writer.Write(new byte[0x50]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 1);

                Assert.AreEqual(0x20, stream.Position);
                reader.Seek(0x10, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1, reader.ReadInt32());

                stream.Position = 0;
                writer.Write(new byte[0x50]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 2);

                Assert.AreEqual(0x20, stream.Position);
                reader.Seek(0x10, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1, reader.ReadInt32());

                stream.Position = 0;
                writer.Write(new byte[0x50]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 3);

                Assert.AreEqual(0x30, stream.Position);
                reader.Seek(0x10, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1, reader.ReadInt32());

                stream.Position = 0;
                writer.Write(new byte[0x50]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 4);

                Assert.AreEqual(0x30, stream.Position);
                reader.Seek(0x10, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1, reader.ReadInt32());

                stream.Position = 0;
                writer.Write(new byte[0x50]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 5);

                Assert.AreEqual(0x40, stream.Position);
                reader.Seek(0x10, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1, reader.ReadInt32());

                stream.Position = 0;
                writer.Write(new byte[0x50]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 6);

                Assert.AreEqual(0x40, stream.Position);
                reader.Seek(0x10, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1, reader.ReadInt32());
            }
        }

        private static void Versions05<T>(ByteOrder order)
            where T : VersionedClass05, new()
        {
            var rng = new Random();
            var obj = new T
            {
                Property1a = rng.Next(int.MinValue, int.MaxValue),
                Property1b = rng.Next(int.MinValue, int.MaxValue)
            };

            using (var stream = new MemoryStream())
            using (var reader = new EndianReader(stream, order))
            using (var writer = new EndianWriter(stream, order))
            {
                writer.WriteObject(obj, 0);

                reader.Seek(0x10, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1a, reader.ReadInt32());

                stream.Position = 0;
                writer.Write(new byte[0x50]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 1);

                reader.Seek(0x10, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1a, reader.ReadInt32());

                stream.Position = 0;
                writer.Write(new byte[0x50]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 2);

                reader.Seek(0x10, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1a, reader.ReadInt32());

                stream.Position = 0;
                writer.Write(new byte[0x50]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 3);

                reader.Seek(0x10, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1b, reader.ReadInt32());

                stream.Position = 0;
                writer.Write(new byte[0x50]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 4);

                reader.Seek(0x10, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1b, reader.ReadInt32());

                stream.Position = 0;
                writer.Write(new byte[0x50]); //set to zeros

                stream.Position = 0;
                writer.WriteObject(obj, 5);

                reader.Seek(0x10, SeekOrigin.Begin);
                Assert.AreEqual(obj.Property1b, reader.ReadInt32());
            }
        }
    }
}
