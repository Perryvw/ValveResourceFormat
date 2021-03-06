﻿using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Text;
using ValveResourceFormat.Blocks;

namespace ValveResourceFormat.ResourceTypes
{
    public class NTRO : ResourceData
    {
        protected BinaryReader Reader { get; private set; }
        protected Resource Resource { get; private set; }
        public NTROSerialization.NTROStruct Output { get; private set; }

        public override void Read(BinaryReader reader, Resource resource)
        {
            Reader = reader;
            Resource = resource;

            foreach (var refStruct in resource.IntrospectionManifest.ReferencedStructs)
            {
                Output = ReadStructure(refStruct, Offset);

                break;
            }
        }

        private NTROSerialization.NTROStruct ReadStructure(ResourceIntrospectionManifest.ResourceDiskStruct refStruct, long startingOffset)
        {
            var structEntry = new NTROSerialization.NTROStruct(refStruct.Name);

            foreach (var field in refStruct.FieldIntrospection)
            {
                Reader.BaseStream.Position = startingOffset + field.OnDiskOffset;

                ReadFieldIntrospection(field, ref structEntry);
            }

            if (refStruct.BaseStructId != 0)
            {
                var previousOffset = Reader.BaseStream.Position;

                var newStruct = Resource.IntrospectionManifest.ReferencedStructs.First(x => x.Id == refStruct.BaseStructId);

                // Valve doesn't print this struct's type, so we can't just call ReadStructure *sigh*
                foreach (var field in newStruct.FieldIntrospection)
                {
                    Reader.BaseStream.Position = startingOffset + field.OnDiskOffset;

                    ReadFieldIntrospection(field, ref structEntry);
                }

                Reader.BaseStream.Position = previousOffset;
            }

            return structEntry;
        }

        private void ReadFieldIntrospection(ResourceIntrospectionManifest.ResourceDiskStruct.Field field, ref NTROSerialization.NTROStruct structEntry)
        {
            uint count = (uint)field.Count;
            bool pointer = false; // TODO: get rid of this

            if (count == 0)
            {
                count = 1;
            }

            long prevOffset = 0;

            if (field.Indirections.Count > 0)
            {
                // TODO
                if (field.Indirections.Count > 1)
                {
                    throw new NotImplementedException("More than one indirection, not yet handled.");
                }

                // TODO
                if (field.Count > 0)
                {
                    throw new NotImplementedException("Indirection.Count > 0 && field.Count > 0");
                }

                var indirection = field.Indirections[0]; // TODO: depth needs fixing?

                var offset = Reader.ReadUInt32();

                if (indirection == 0x03)
                {
                    pointer = true;

                    if (offset == 0)
                    {
                        structEntry.Add(field.FieldName, new NTROSerialization.NTROValue<byte?>(field.Type, (byte?)null, true)); //being byte shouldn't matter

                        return;
                    }

                    prevOffset = Reader.BaseStream.Position;

                    Reader.BaseStream.Position += offset - 4;
                }
                else if (indirection == 0x04)
                {
                    count = Reader.ReadUInt32();

                    prevOffset = Reader.BaseStream.Position;

                    if (count > 0)
                    {
                        Reader.BaseStream.Position += offset - 8;
                    }
                }
                else
                {
                    throw new NotImplementedException(string.Format("Unknown indirection. ({0})", indirection));
                }
            }

            //if (pointer)
            //{
            //    Writer.Write("{0} {1}* = (ptr) ->", ValveDataType(field.Type), field.FieldName);
            //}
            if (field.Count > 0 || field.Indirections.Count > 0)
            {
                var ntroValues = new NTROSerialization.NTROArray(field.Type, (int)count, pointer, field.Indirections.Count > 0);

                for (var i = 0; i < count; i++)
                {
                    ntroValues[i] = ReadField(field, pointer);
                }

                structEntry.Add(field.FieldName, ntroValues);
            }
            else
            {
                for (var i = 0; i < count; i++)
                {
                    structEntry.Add(field.FieldName, ReadField(field, pointer));
                }
            }

            if (prevOffset > 0)
            {
                Reader.BaseStream.Position = prevOffset;
            }
        }

        private NTROSerialization.NTROValue ReadField(ResourceIntrospectionManifest.ResourceDiskStruct.Field field, bool pointer)
        {
            switch (field.Type)
            {
                case DataType.Struct:
                    var newStruct = Resource.IntrospectionManifest.ReferencedStructs.First(x => x.Id == field.TypeData);
                    return new NTROSerialization.NTROValue<NTROSerialization.NTROStruct>(field.Type, ReadStructure(newStruct, Reader.BaseStream.Position), pointer);

                case DataType.Enum:
                    // TODO: Lookup in ReferencedEnums
                    return new NTROSerialization.NTROValue<uint>(field.Type, Reader.ReadUInt32(), pointer);

                case DataType.SByte:
                    return new NTROSerialization.NTROValue<sbyte>(field.Type, Reader.ReadSByte(), pointer);

                case DataType.Byte:
                    return new NTROSerialization.NTROValue<byte>(field.Type, Reader.ReadByte(), pointer);

                case DataType.Boolean:
                    return new NTROSerialization.NTROValue<bool>(field.Type, Reader.ReadByte() == 1 ? true : false, pointer);

                case DataType.Int16:
                    return new NTROSerialization.NTROValue<short>(field.Type, Reader.ReadInt16(), pointer);

                case DataType.UInt16:
                    return new NTROSerialization.NTROValue<ushort>(field.Type, Reader.ReadUInt16(), pointer);

                case DataType.Int32:
                    return new NTROSerialization.NTROValue<int>(field.Type, Reader.ReadInt32(), pointer);

                case DataType.UInt32:
                    return new NTROSerialization.NTROValue<uint>(field.Type, Reader.ReadUInt32(), pointer);

                case DataType.Float:
                    return new NTROSerialization.NTROValue<float>(field.Type, Reader.ReadSingle(), pointer);

                case DataType.Int64:
                    return new NTROSerialization.NTROValue<long>(field.Type, Reader.ReadInt64(), pointer);

                case DataType.ExternalReference:
                    var id = Reader.ReadUInt64();
                    return new NTROSerialization.NTROValue<ResourceExtRefList.ResourceReferenceInfo>(field.Type, Resource.ExternalReferences.ResourceRefInfoList.FirstOrDefault(c => c.Id == id), pointer);

                case DataType.UInt64:
                    return new NTROSerialization.NTROValue<ulong>(field.Type, Reader.ReadUInt64(), pointer);

                case DataType.Vector:
                    var vector3 = new NTROSerialization.Vector3(
                        Reader.ReadSingle(),
                        Reader.ReadSingle(),
                        Reader.ReadSingle()
                    );

                    return new NTROSerialization.NTROValue<NTROSerialization.Vector3>(field.Type, vector3, pointer);

                case DataType.Quaternion:
                case DataType.Color:
                case DataType.Fltx4:
                case DataType.Vector4D:
                    var vector4 = new NTROSerialization.Vector4(
                        Reader.ReadSingle(),
                        Reader.ReadSingle(),
                        Reader.ReadSingle(),
                        Reader.ReadSingle()
                    );

                    return new NTROSerialization.NTROValue<NTROSerialization.Vector4>(field.Type, vector4, pointer);

                case DataType.String4:
                case DataType.String:
                    return new NTROSerialization.NTROValue<string>(field.Type, Reader.ReadOffsetString(Encoding.UTF8), pointer);

                case DataType.Matrix3x4:
                case DataType.Matrix3x4a:
                    var matrix3x4a = new NTROSerialization.Matrix3x4(
                        Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle(),
                        Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle(),
                        Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle()
                     );

                    return new NTROSerialization.NTROValue<NTROSerialization.Matrix3x4>(field.Type, matrix3x4a, pointer);

                case DataType.CTransform:
                    var transform = new NTROSerialization.CTransform(
                        Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle(),
                        Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle()
                     );

                    return new NTROSerialization.NTROValue<NTROSerialization.CTransform>(field.Type, transform, pointer);

                default:
                    throw new NotImplementedException(string.Format("Unknown data type: {0}", field.Type));
            }
        }

        public override string ToString()
        {
            return Output.ToString() ?? "Nope.";
        }
    }
}
