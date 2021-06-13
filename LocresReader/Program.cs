using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LocresReader
{
    static class Program
    {
        static void Main(string[] args)
        {
            // Path, Language, Namespace and Key
            var dict = new Dictionary<(string Path, string Language, string Namespace, string Key), string>();
            string readString(BinaryReader br)
            {
                var length = br.ReadInt32();
                if (length > 0)
                {
                    var result = Encoding.ASCII.GetString(br.ReadBytes(length)).TrimEnd('\0');
                    br.ReadBytes(4);
                    return result;
                }
                else if (length == 0)
                {
                    return "";
                }
                else
                {
                    var data = br.ReadBytes((-1 - length) * 2);
                    var result = br.ReadBytes(4);
                    br.ReadBytes(2);
                    return Encoding.Unicode.GetString(data);
                }
            }
            foreach (var file in Directory.GetFiles(Environment.CurrentDirectory, "*.locres", SearchOption.AllDirectories))
            {
                var _fileName = Path.GetFileName(file);
                var dirName = Path.GetDirectoryName(file);
                var lang = Path.GetFileName(dirName);
                var path = Path.GetDirectoryName(dirName);
                var reader = new BinaryReader(new MemoryStream(File.ReadAllBytes(file)));
                var MagicNumber = reader.ReadBytes(16);
                var VersionNumber = reader.ReadByte();
                if (VersionNumber != 2)
                {
                    Console.WriteLine("Version not match. Try history version of this tool https://github.com/sgkoishi/LocresReader");
                }
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
                string readStringComplex(BinaryReader br)
                {
                    if (VersionNumber == 2)
                    {
                        var _hash = br.ReadUInt32();
                        var savenum = br.ReadInt32();
                        if (savenum < 0)
                        {
                            var data = br.ReadBytes(-savenum * 2);
                            return Encoding.Unicode.GetString(data).Trim('\0');
                        }
                        else
                        {
                            var data = br.ReadBytes(savenum);
                            return Encoding.ASCII.GetString(data).Trim('\0');
                        }
                    }
                    return "";
                }
                var EntriesCount = reader.ReadUInt32();
                var NamespaceCount = reader.ReadUInt32();
                for (var i = 0; i < NamespaceCount; i++)
                {
                    var Namespace = readStringComplex(reader);
                    var KeyCount = reader.ReadInt32();
                    for (var j = 0; j < KeyCount; j++)
                    {
                        var Key = readStringComplex(reader);
                        var _SourceStringHash = reader.ReadUInt32();
                        var LocalizedStringIndex = reader.ReadInt32();
                        dict[(path, lang, Namespace, Key)] = LocalizedStringArray[LocalizedStringIndex];
                    }
                }
            }
            foreach (var kvp in dict.GroupBy(kvp => kvp.Key.Path))
            {
                var result = new SortedDictionary<string, SortedDictionary<string, SortedDictionary<string, string>>>();
                foreach (var item in kvp)
                {
                    if (!result.ContainsKey(item.Key.Namespace))
                    {
                        result[item.Key.Namespace] = new SortedDictionary<string, SortedDictionary<string, string>>();
                    }
                    if (!result[item.Key.Namespace].ContainsKey(item.Key.Key))
                    {
                        result[item.Key.Namespace][item.Key.Key] = new SortedDictionary<string, string>();
                    }
                    result[item.Key.Namespace][item.Key.Key][item.Key.Language] = item.Value;
                }
                File.WriteAllText(Path.Combine(kvp.Key, "locres.json"), JsonConvert.SerializeObject(result, Formatting.Indented));
            }
        }
    }
}
