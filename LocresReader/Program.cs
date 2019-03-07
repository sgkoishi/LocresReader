using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace LocresReader
{
    static class Program
    {
        static void Main(string[] args)
        {
            var dict = new SortedDictionary<string, SortedDictionary<string, string>>();
            string readString(BinaryReader br)
            {
                var length = br.ReadInt32();
                if (length > 0)
                {
                    return Encoding.ASCII.GetString(br.ReadBytes(length)).TrimEnd('\0');
                }
                else if (length == 0)
                {
                    return "";
                }
                else
                {
                    var data = br.ReadBytes((-1 - length) * 2);
                    br.ReadBytes(2); // Null terminated string I guess?
                    return Encoding.Unicode.GetString(data);
                }
            }
            var reader = new BinaryReader(new MemoryStream(File.ReadAllBytes(args[0])));
            var MagicNumber = reader.ReadBytes(16);
            var VersionNumber = reader.ReadByte();
            var LocalizedStringArrayOffset = reader.ReadInt64();
            var CurrentFileOffset = reader.BaseStream.Position;
            reader.BaseStream.Position = LocalizedStringArrayOffset;
            var arrayLength = reader.ReadInt32();
            var LocalizedStringArray = new string[arrayLength];
            for (var i = 0; i < arrayLength; i++)
            {
                LocalizedStringArray[i] = readString(reader);
            }
            reader.BaseStream.Position = CurrentFileOffset;
            var NamespaceCount = reader.ReadUInt32();
            for (var i = 0; i < NamespaceCount; i++)
            {
                var Namespace = readString(reader);
                if (!dict.ContainsKey(Namespace))
                {
                    dict[Namespace] = new SortedDictionary<string, string>();
                }
                var KeyCount = reader.ReadInt32();
                for (var j = 0; j < KeyCount; j++)
                {
                    var Key = readString(reader);
                    var _SourceStringHash = BitConverter.ToString(reader.ReadBytes(4)).Replace("-", ""); // String after CRC slice by 8
                    var LocalizedStringIndex = reader.ReadInt32();
                    dict[Namespace][Key] = LocalizedStringArray[LocalizedStringIndex];
                }
            }
            File.WriteAllText(args[1], JsonConvert.SerializeObject(dict, Formatting.Indented));
        }
    }
}
