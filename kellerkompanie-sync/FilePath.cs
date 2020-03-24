using Newtonsoft.Json;
using System;
using System.IO;

namespace kellerkompanie_sync
{
    [JsonConverter(typeof(FilePathConverter))]
    public class FilePath : IComparable
    {
        public string Value
        {
            get
            {
                return OriginalValue.ToLower();
            }
        }

        public string OriginalValue { get; private set; }

        public int Length
        {
            get
            {
                return Value.Length;
            }
        }

        public bool Contains(string str)
        {
            return Value.Contains(str);
        }

        public override bool Equals(object obj)
        {
            return obj is FilePath path && Value.Equals(path.Value);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Value);
        }

        public int IndexOf(string str)
        {
            return Value.IndexOf(str);
        }

        public int LastIndexOf(string str)
        {
            return Value.LastIndexOf(str);
        }

        public static FilePath Combine(FilePath filePath, FilePath filePathOther)
        {
            string combinedPath = Path.Combine(filePath.OriginalValue, filePathOther.OriginalValue);
            return new FilePath(combinedPath);
        }

        public FilePath SubPath(int index)
        {
            var filePath = new FilePath(OriginalValue.Substring(index));
            return filePath;
        }

        public FilePath SubPath(int start, int end)
        {
            var filePath = new FilePath(OriginalValue.Substring(start, end));
            return filePath;
        }

        public override string ToString()
        {
            return OriginalValue;
        }

        public FilePath Replace(string oldValue, string newValue)
        {
            return new FilePath(OriginalValue.Replace(oldValue, newValue));
        }

        public int CompareTo(object obj)
        {
            if (obj == null) return 1;

            FilePath otherFilePath = obj as FilePath;
            return Value.CompareTo(otherFilePath.Value);
        }

        public FilePath(string filePath)
        {
            OriginalValue = filePath ?? throw new ArgumentException("filePath cannot be null");
        }
    }

    public class FilePathConverter : JsonConverter
    {
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string value = (string)reader.Value;
            return new FilePath(value);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            FilePath filePath = (FilePath)value;
            writer.WriteValue(filePath.OriginalValue);
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(FilePath);
        }
    }
}
