﻿using System.Reflection;

namespace System.IO.Endian.Dynamic
{
    internal interface IFieldDefinition
    {
        ByteOrder? ByteOrder { get; }
        bool IsDataLengthProperty { get; init; }
        bool IsVersionProperty { get; init; }
        long Offset { get; }
        PropertyInfo TargetProperty { get; }
        Type TargetType { get; }
    }
}