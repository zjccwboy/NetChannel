using Newtonsoft.Json;

namespace Common
{
    public static class Convert
    {
        private readonly static JsonSerializerSettings settings = new JsonSerializerSettings();

        static Convert()
        {
            settings.NullValueHandling = NullValueHandling.Ignore;
        }

        public static string ToJson(this object obj, Formatting formatting = Formatting.None)
        {
            var json = JsonConvert.SerializeObject(obj, formatting, settings);
            return json;
        }

        public static object ToObj(this string json)
        {
            var obj = JsonConvert.DeserializeObject(json, settings);
            return obj;
        }

        public static string ToJson<T>(this T obj, Formatting formatting = Formatting.None)
        {
            var json = JsonConvert.SerializeObject(obj,typeof(T), formatting, settings);
            return json;
        }

        public static T ToObj<T>(this string json)
        {
            var obj = JsonConvert.DeserializeObject<T>(json, settings);
            return obj;
        }
    }
}
