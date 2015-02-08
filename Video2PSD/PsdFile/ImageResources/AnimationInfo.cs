using System;
using System.Collections.Generic;
using System.IO;

namespace PhotoshopFile
{
    public enum PsdEncodedType
    {
        Long = 0x6c6f6e67,
        Doub = 0x646f7562,
        Bool = 0x626f6f6c,
        VlLs = 0x566c4c73,
        Objc = 0x4f626a63,
    }

    public class PsdEncodedList
    {
        public int Count { get; private set; }
        public List<Tuple<PsdEncodedType, object>> Entries { get; private set; }
        
        public PsdEncodedList(PsdBinaryReader reader) { ReadFromBinary(reader); }
        
        public void ReadFromBinary(PsdBinaryReader reader)
        {
            Count = reader.ReadInt32();
            Entries = new List<Tuple<PsdEncodedType, object>>(Count);
            for (int i = 0; i < Count; ++i)
            {
                Entries.Add(PsdEncodedObject.DecodePsdAtomic(reader));
            }
        }
    }

    public class PsdEncodedObjectProperty
    {
        public int NullField { get; private set; }
        public string Name { get; private set; }
        public PsdEncodedType Type { get; private set; }
        public object Value { get; private set; }

        public PsdEncodedObjectProperty(PsdBinaryReader reader) { ReadFromBinary(reader); }

        public void ReadFromBinary(PsdBinaryReader reader)
        {
            NullField = reader.ReadInt32();
            Name = reader.ReadAsciiChars(4);
            Tuple<PsdEncodedType, object> atomic = PsdEncodedObject.DecodePsdAtomic(reader);
            Type = atomic.Item1;
            Value = atomic.Item2;
        }
    }

    public class PsdEncodedObject
    {
        public int Signature { get; private set; }
        public int UnknownField1 { get; private set; }
        public int UnknownField2 { get; private set; }
        public short UnknownField3 { get; private set; }
        public int NullField { get; private set; }
        public int NumberOfProperties { get; private set; }
        public Dictionary<string, PsdEncodedObjectProperty> Properties;

        public PsdEncodedObject(PsdBinaryReader reader) { ReadFromBinary(reader); }

        public void ReadFromBinary(PsdBinaryReader reader)
        {
            Signature = reader.ReadInt32();
            UnknownField1 = reader.ReadInt32();
            UnknownField2 = reader.ReadInt32();
            UnknownField3 = reader.ReadInt16();
            NullField = reader.ReadInt32();
            NumberOfProperties = reader.ReadInt32();

            Properties = new Dictionary<string, PsdEncodedObjectProperty>(NumberOfProperties);

            for (int i = 0; i < NumberOfProperties; ++i)
            {
                PsdEncodedObjectProperty prop = new PsdEncodedObjectProperty(reader);
                Properties.Add(prop.Name, prop);
            }
        }

        public static Tuple<PsdEncodedType, object> DecodePsdAtomic(PsdBinaryReader reader)
        {
            PsdEncodedType type = (PsdEncodedType)reader.ReadInt32();
            object value = null;
            switch (type)
            {
                case PsdEncodedType.Long:
                    value = reader.ReadInt32();
                    break;
                case PsdEncodedType.Bool:
                    value = reader.ReadBoolean();
                    break;
                case PsdEncodedType.Doub:
                    value = reader.ReadDouble();
                    break;
                case PsdEncodedType.VlLs:
                    value = new PsdEncodedList(reader);
                    break;
                case PsdEncodedType.Objc:
                    // We have to seek back over the type, since it needs to be read as the signature
                    reader.BaseStream.Seek(-4, SeekOrigin.Current);
                    value = new PsdEncodedObject(reader);
                    break;
                default:
                    throw new PsdInvalidException("Random 4 bytes found instead of type!");
            }
            return new Tuple<PsdEncodedType, object>(type, value);
        }
    }

    public class AnimationInfo : ImageResource
    {
        public AnimationInfo() : base("4000") { }
        public AnimationInfo(PsdBinaryReader reader, string name, int dataLength) : base(name)
        {
            //byte[] data = reader.ReadBytes(dataLength);
            string signature = reader.ReadAsciiChars(8);
            if (signature != "maniIRFR")
            {
                throw new PsdInvalidException("The animation data isn't itself!");
            }
            int unpaddedSize = reader.ReadInt32();
            string sig2 = reader.ReadAsciiChars(4); //Should be "8BIM"
            string sig3 = reader.ReadAsciiChars(4); //Should be "AnDs"
            int AnDsSize = reader.ReadInt32();
            PsdEncodedObject AnDsObject = new PsdEncodedObject(reader);
        }

        public override ResourceID ID
        {
            get { return ResourceID.AnimationInfo; }
        }

        protected override void WriteData(PsdBinaryWriter writer)
        {
        }
    }
}