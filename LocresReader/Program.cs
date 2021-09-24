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
            string readString(BinaryReader br, int version)
            {
                var length = br.ReadInt32();
                if (length > 0)
                {
                    var result = Encoding.ASCII.GetString(br.ReadBytes(length)).TrimEnd('\0');
                    if (version == 2)
                    {
                        br.ReadBytes(4);
                    }
                    return result;
                }
                else if (length == 0)
                {
                    return "";
                }
                else
                {
                    var data = br.ReadBytes((-1 - length) * 2);
                    if (version == 2)
                    {
                        br.ReadBytes(4);
                    }
                    br.ReadBytes(2);
                    return Encoding.Unicode.GetString(data);
                }
            }
            foreach (var file in Directory.GetFiles(Environment.CurrentDirectory, "*.locres", SearchOption.AllDirectories))
            {
                Console.WriteLine($"Working on {file}");
                var _fileName = Path.GetFileName(file);
                var dirName = Path.GetDirectoryName(file);
                var lang = Path.GetFileName(dirName);
                var path = Path.GetDirectoryName(dirName);
                var reader = new BinaryReader(new MemoryStream(File.ReadAllBytes(file)));
                var MagicNumber = reader.ReadBytes(16);
                var VersionNumber = reader.ReadByte();
                if (VersionNumber <= 0 || VersionNumber >= 3)
                {
                    Console.WriteLine($"Version {VersionNumber} not supported.");
                    continue;
                }
                var LocalizedStringArrayOffset = reader.ReadInt64();
                var CurrentFileOffset = reader.BaseStream.Position;
                reader.BaseStream.Position = LocalizedStringArrayOffset;
                var arrayLength = reader.ReadInt32();
                var LocalizedStringArray = new string[arrayLength];
                for (var i = 0; i < arrayLength; i++)
                {
                    LocalizedStringArray[i] = readString(reader, VersionNumber);
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
                var EntriesCount = VersionNumber == 2 ? reader.ReadUInt32() : 0;
                var NamespaceCount = reader.ReadUInt32();
                for (var i = 0; i < NamespaceCount; i++)
                {
                    var Namespace = VersionNumber == 2 ? readStringComplex(reader) : readString(reader, VersionNumber);
                    var KeyCount = reader.ReadInt32();
                    for (var j = 0; j < KeyCount; j++)
                    {
                        var Key = VersionNumber == 2 ? readStringComplex(reader) : readString(reader, VersionNumber);
                        var _SourceStringHash = reader.ReadUInt32();
                        var LocalizedStringIndex = reader.ReadInt32();
                        dict[(path, lang, Namespace, Key)] = LocalizedStringArray[LocalizedStringIndex];
                    }
                }
            }
            bool Duplicate<TKey, TValue>(SortedDictionary<TKey, TValue> first, SortedDictionary<TKey, TValue> second) where TValue: class
            {
                if (first.Count != second.Count)
                {
                    return false;
                }
                foreach (var item in first)
                {
                    if (!second.ContainsKey(item.Key))
                    {
                        return false;
                    }
                    if (second[item.Key] != item.Value)
                    {
                        return false;
                    }
                }
                return true;
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
                if (args.Length == 1 && args[0] == "dedupe")
                {
                    foreach (var nsKvp in result.ToList())
                    {
                        var ns = new SortedDictionary<string, SortedDictionary<string, string>>();
                        foreach (var keyKvp in nsKvp.Value)
                        {
                            if (!ns.Any(nsItem => Duplicate(nsItem.Value, keyKvp.Value)))
                            {
                                ns[keyKvp.Key] = keyKvp.Value;
                            }
                        }
                        result[nsKvp.Key] = ns;
                    }
                }
                File.WriteAllText(Path.Combine(kvp.Key, "locres.json"), JsonConvert.SerializeObject(result, Formatting.Indented));
            }
        }
    }
}
