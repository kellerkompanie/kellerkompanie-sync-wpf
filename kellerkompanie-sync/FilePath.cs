using Newtonsoft.Json;
using System;

namespace kellerkompanie_sync
{
    [JsonConverter(typeof(FilePathConverter))]
    public class FilePath : IComparable
    {
        private string value;
        public string Value
        {
            get
            {
                return value.ToLower();
            }
            set
            {
                this.value = value;
            }
        }

        public string OriginalValue
        {
            get
            {
                return value;
            }
        }

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


        public FilePath SubPath(int index)
        {
            var filePath = new FilePath
            {
                Value = value.Substring(index)
            };
            return filePath;
        }

        public FilePath SubPath(int start, int end)
        {
            var filePath = new FilePath { Value = value.Substring(start, end) };
            return filePath;
        }

        public override string ToString()
        {
            return Value;
        }

        public FilePath Replace(string oldValue, string newValue)
        {
            return new FilePath { Value = value.Replace(oldValue, newValue) };
        }

        public int CompareTo(object obj)
        {
            if (obj == null) return 1;

            FilePath otherFilePath = obj as FilePath;
            return Value.CompareTo(otherFilePath.Value);
        }
    }

    public class FilePathConverter : JsonConverter
    {
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string value = (string)reader.Value;
            return new FilePath { Value = value };
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            FilePath filePath = (FilePath)value;
            writer.WriteValue(filePath.Value);
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(FilePath);
        }
    }
}
