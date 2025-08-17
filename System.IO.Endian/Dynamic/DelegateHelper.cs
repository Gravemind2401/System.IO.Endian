﻿using System.Linq.Expressions;
using System.Reflection;

namespace System.IO.Endian.Dynamic
{
    internal static class DelegateHelper
    {
        private static readonly MethodInfo GenericTypeCheckMethod = typeof(DelegateHelper)
            .GetMethod(nameof(IsTypeSupported), BindingFlags.Static | BindingFlags.Public, Type.EmptyTypes)!;

        public static bool IsTypeSupported(Type type)
        {
            return (bool)GenericTypeCheckMethod
                .MakeGenericMethod(type)
                .Invoke(null, null)!;
        }

        public static bool IsTypeSupported<TStruct>()
        {
            var typeCode = Type.GetTypeCode(typeof(TStruct));

            return (typeCode >= TypeCode.Boolean && typeCode <= TypeCode.Decimal)
                || typeof(TStruct) == typeof(Half)
                || typeof(TStruct) == typeof(Guid)
                || default(TStruct) is IBufferable<TStruct>;
        }
    }

    //no struct constraint so it can easily be called from unconstrained generics after a type check.
    //since it is an internal class we can be sure it is only ever used with the correct types.
    internal static class DelegateHelper<TStruct>
    {
        public delegate TStruct DefaultReadMethod(EndianReader reader);
        public delegate TStruct ByteOrderReadMethod(EndianReader reader, ByteOrder byteOrder);

        public delegate void DefaultWriteMethod(EndianWriter writer, TStruct value);
        public delegate void ByteOrderWriteMethod(EndianWriter writer, TStruct value, ByteOrder byteOrder);

        public static readonly DefaultReadMethod InvokeDefaultRead;
        public static readonly ByteOrderReadMethod? InvokeByteOrderRead;

        public static readonly DefaultWriteMethod InvokeDefaultWrite;
        public static readonly ByteOrderWriteMethod? InvokeByteOrderWrite;

        static DelegateHelper()
        {
            if (default(TStruct) is IBufferable<TStruct>)
            {
                var methods = typeof(EndianReader).GetMethods();

                InvokeDefaultRead = methods.First(m => m.Name == nameof(EndianReader.ReadBufferable) && m.GetParameters().Length == 0)
                    .MakeGenericMethod(typeof(TStruct))
                    .CreateDelegate<DefaultReadMethod>();

                InvokeByteOrderRead = methods.First(m => m.Name == nameof(EndianReader.ReadBufferable) && m.GetParameters().Length == 1)
                    .MakeGenericMethod(typeof(TStruct))
                    .CreateDelegate<ByteOrderReadMethod>();

                methods = typeof(EndianWriter).GetMethods();

                InvokeDefaultWrite = methods.First(m => m.Name == nameof(EndianWriter.WriteBufferable) && m.GetParameters().Length == 1)
                    .MakeGenericMethod(typeof(TStruct))
                    .CreateDelegate<DefaultWriteMethod>();

                InvokeByteOrderWrite = methods.First(m => m.Name == nameof(EndianWriter.WriteBufferable) && m.GetParameters().Length == 2)
                    .MakeGenericMethod(typeof(TStruct))
                    .CreateDelegate<ByteOrderWriteMethod>();

                return;
            }

            var typeCode = Type.GetTypeCode(typeof(TStruct));

            InvokeDefaultRead = typeCode switch
            {
                TypeCode.Boolean => CreateReadDelegate(r => r.ReadBoolean()),
                TypeCode.Char => CreateReadDelegate(r => r.ReadChar()),
                TypeCode.SByte => CreateReadDelegate(r => r.ReadSByte()),
                TypeCode.Byte => CreateReadDelegate(r => r.ReadByte()),
                TypeCode.Int16 => CreateReadDelegate(r => r.ReadInt16()),
                TypeCode.UInt16 => CreateReadDelegate(r => r.ReadUInt16()),
                TypeCode.Int32 => CreateReadDelegate(r => r.ReadInt32()),
                TypeCode.UInt32 => CreateReadDelegate(r => r.ReadUInt32()),
                TypeCode.Int64 => CreateReadDelegate(r => r.ReadInt64()),
                TypeCode.UInt64 => CreateReadDelegate(r => r.ReadUInt64()),
                TypeCode.Single => CreateReadDelegate(r => r.ReadSingle()),
                TypeCode.Double => CreateReadDelegate(r => r.ReadDouble()),
                TypeCode.Decimal => CreateReadDelegate(r => r.ReadDecimal()),
                _ when typeof(TStruct) == typeof(Half) => CreateReadDelegate(r => r.ReadHalf()),
                _ when typeof(TStruct) == typeof(Guid) => CreateReadDelegate(r => r.ReadGuid()),
                _ => throw new NotSupportedException()
            };

            InvokeByteOrderRead = typeCode switch
            {
                TypeCode.Boolean or TypeCode.Char or TypeCode.SByte or TypeCode.Byte => null,
                TypeCode.Int16 => CreateReadDelegate((r, o) => r.ReadInt16(o)),
                TypeCode.UInt16 => CreateReadDelegate((r, o) => r.ReadUInt16(o)),
                TypeCode.Int32 => CreateReadDelegate((r, o) => r.ReadInt32(o)),
                TypeCode.UInt32 => CreateReadDelegate((r, o) => r.ReadUInt32(o)),
                TypeCode.Int64 => CreateReadDelegate((r, o) => r.ReadInt64(o)),
                TypeCode.UInt64 => CreateReadDelegate((r, o) => r.ReadUInt64(o)),
                TypeCode.Single => CreateReadDelegate((r, o) => r.ReadSingle(o)),
                TypeCode.Double => CreateReadDelegate((r, o) => r.ReadDouble(o)),
                TypeCode.Decimal => CreateReadDelegate((r, o) => r.ReadDecimal(o)),
                _ when typeof(TStruct) == typeof(Half) => CreateReadDelegate((r, o) => r.ReadHalf(o)),
                _ when typeof(TStruct) == typeof(Guid) => CreateReadDelegate((r, o) => r.ReadGuid(o)),
                _ => throw new NotSupportedException()
            };

            InvokeDefaultWrite = typeCode switch
            {
                TypeCode.Boolean => CreateWriteDelegate<bool>((w, v) => w.Write(v)),
                TypeCode.Char => CreateWriteDelegate<char>((w, v) => w.Write(v)),
                TypeCode.SByte => CreateWriteDelegate<sbyte>((w, v) => w.Write(v)),
                TypeCode.Byte => CreateWriteDelegate<byte>((w, v) => w.Write(v)),
                TypeCode.Int16 => CreateWriteDelegate<short>((w, v) => w.Write(v)),
                TypeCode.UInt16 => CreateWriteDelegate<ushort>((w, v) => w.Write(v)),
                TypeCode.Int32 => CreateWriteDelegate<int>((w, v) => w.Write(v)),
                TypeCode.UInt32 => CreateWriteDelegate<uint>((w, v) => w.Write(v)),
                TypeCode.Int64 => CreateWriteDelegate<long>((w, v) => w.Write(v)),
                TypeCode.UInt64 => CreateWriteDelegate<ulong>((w, v) => w.Write(v)),
                TypeCode.Single => CreateWriteDelegate<float>((w, v) => w.Write(v)),
                TypeCode.Double => CreateWriteDelegate<double>((w, v) => w.Write(v)),
                TypeCode.Decimal => CreateWriteDelegate<decimal>((w, v) => w.Write(v)),
                _ when typeof(TStruct) == typeof(Half) => CreateWriteDelegate<Half>((w, v) => w.Write(v)),
                _ when typeof(TStruct) == typeof(Guid) => CreateWriteDelegate<Guid>((w, v) => w.Write(v)),
                _ => throw new NotSupportedException()
            };

            InvokeByteOrderWrite = typeCode switch
            {
                TypeCode.Boolean or TypeCode.Char or TypeCode.SByte or TypeCode.Byte => null,
                TypeCode.Int16 => CreateWriteDelegate<short>((w, v, o) => w.Write(v, o)),
                TypeCode.UInt16 => CreateWriteDelegate<ushort>((w, v, o) => w.Write(v, o)),
                TypeCode.Int32 => CreateWriteDelegate<int>((w, v, o) => w.Write(v, o)),
                TypeCode.UInt32 => CreateWriteDelegate<uint>((w, v, o) => w.Write(v, o)),
                TypeCode.Int64 => CreateWriteDelegate<long>((w, v, o) => w.Write(v, o)),
                TypeCode.UInt64 => CreateWriteDelegate<ulong>((w, v, o) => w.Write(v, o)),
                TypeCode.Single => CreateWriteDelegate<float>((w, v, o) => w.Write(v, o)),
                TypeCode.Double => CreateWriteDelegate<double>((w, v, o) => w.Write(v, o)),
                TypeCode.Decimal => CreateWriteDelegate<decimal>((w, v, o) => w.Write(v, o)),
                _ when typeof(TStruct) == typeof(Half) => CreateWriteDelegate<Half>((w, v, o) => w.Write(v, o)),
                _ when typeof(TStruct) == typeof(Guid) => CreateWriteDelegate<Guid>((w, v, o) => w.Write(v, o)),
                _ => throw new NotSupportedException()
            };
        }

        private static DefaultReadMethod CreateReadDelegate<TRead>(Expression<Func<EndianReader, TRead>> expression)
        {
            return typeof(TRead) == typeof(TStruct)
                ? CreateDelegate<DefaultReadMethod>(expression)
                : throw new ArgumentException(null, nameof(TRead));
        }

        private static ByteOrderReadMethod CreateReadDelegate<TRead>(Expression<Func<EndianReader, ByteOrder, TRead>> expression)
        {
            return typeof(TRead) == typeof(TStruct)
                ? CreateDelegate<ByteOrderReadMethod>(expression)
                : throw new ArgumentException(null, nameof(TRead));
        }

        private static DefaultWriteMethod CreateWriteDelegate<TWrite>(Expression<Action<EndianWriter, TWrite>> expression)
        {
            return typeof(TWrite) == typeof(TStruct)
                ? CreateDelegate<DefaultWriteMethod>(expression)
                : throw new ArgumentException(null, nameof(TWrite));
        }

        private static ByteOrderWriteMethod CreateWriteDelegate<TWrite>(Expression<Action<EndianWriter, TWrite, ByteOrder>> expression)
        {
            return typeof(TWrite) == typeof(TStruct)
                ? CreateDelegate<ByteOrderWriteMethod>(expression)
                : throw new ArgumentException(null, nameof(TWrite));
        }

        private static TDelegate CreateDelegate<TDelegate>(LambdaExpression expression)
            where TDelegate : Delegate
        {
            if (expression.Body is not MethodCallExpression methodCall)
                throw new ArgumentException(null, nameof(expression));

            var method = methodCall.Method
                ?? throw new ArgumentException(null, nameof(expression));

            return method.CreateDelegate<TDelegate>();
        }
    }
}
