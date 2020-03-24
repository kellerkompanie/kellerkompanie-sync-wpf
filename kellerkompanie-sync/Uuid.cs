using Newtonsoft.Json;
using System;

namespace kellerkompanie_sync
{
    [JsonConverter(typeof(UuidConverter))]
    public class Uuid
    {
        public string Value { get; }

        public Uuid(string uuid)
        {
            // TODO check format
            Value = uuid ?? throw new ArgumentException("uuid cannot be null");
        }

        public override bool Equals(object obj)
        {
            if (obj == null) 
            { 
                return false; 
            }
            return obj is Uuid uuid && Value.Equals(uuid.Value);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Value);
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public class UuidConverter : JsonConverter
    {
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string value = (string)reader.Value;
            return new Uuid(value);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Uuid uuid = (Uuid)value;
            writer.WriteValue(uuid.Value);
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Uuid);
        }
    }
}
