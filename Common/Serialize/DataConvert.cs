using Newtonsoft.Json;
using System;
using System.Text;

namespace Common
{
    /// <summary>
    /// Json系列化辅助类
    /// </summary>
    public static class DataConvert
    {
        private readonly static JsonSerializerSettings settings = new JsonSerializerSettings();

        static DataConvert()
        {
            settings.NullValueHandling = NullValueHandling.Ignore;
        }

        /// <summary>
        /// object转json string
        /// </summary>
        /// <param name="obj">object</param>
        /// <param name="type">object type</param>
        /// <returns>json string</returns>
        public static string ConvertToJson(this object obj, Type type)
        {
            if (obj == null)
            {
                return null;
            }

            var json = JsonConvert.SerializeObject(obj, settings);
            return json;
        }

        /// <summary>
        /// object转json string
        /// </summary>
        /// <param name="obj">object</param>
        /// <param name="type">object type</param>
        /// <param name="formatting"></param>
        /// <returns>json string</returns>
        public static string ConvertToJson(this object obj, Type type, Formatting formatting)
        {
            if(obj == null)
            {
                return null;
            }

            var json = JsonConvert.SerializeObject(obj, formatting, settings);
            return json;
        }

        /// <summary>
        /// object转json bytes
        /// </summary>
        /// <param name="obj">object</param>
        /// <param name="type">object type</param>
        /// <param name="formatting">json格式化参数</param>
        /// <returns>json bytes</returns>
        public static byte[] ConvertToBytes(this object obj, Type type, Formatting formatting)
        {
            if(obj == null)
            {
                return null;
            }

            var json = JsonConvert.SerializeObject(obj, type, formatting, settings);
            var bytes = Encoding.UTF8.GetBytes(json);
            return bytes;
        }

        /// <summary>
        /// object转json string
        /// </summary>
        /// <typeparam name="T">泛型对象,class约束</typeparam>
        /// <param name="obj">泛型对象,class约束</param>
        /// <returns>json string</returns>
        public static string ConvertToJson<T>(this T obj) where T : class, new()
        {
            if (obj == null)
            {
                return null;
            }

            var json = JsonConvert.SerializeObject(obj, typeof(T), settings);
            return json;
        }

        /// <summary>
        /// object转json string
        /// </summary>
        /// <typeparam name="T">泛型对象,class约束</typeparam>
        /// <param name="obj">泛型对象,class约束</param>
        /// <param name="formatting">json格式化参数</param>
        /// <returns>json string</returns>
        public static string ConvertToJson<T>(this T obj, Formatting formatting) where T : class, new()
        {
            if(obj == null)
            {
                return null;
            }

            var json = JsonConvert.SerializeObject(obj,typeof(T), formatting, settings);
            return json;
        }

        /// <summary>
        /// object转bytes
        /// </summary>
        /// <typeparam name="T">泛型对象,class约束</typeparam>
        /// <param name="obj">泛型对象,class约束</param>
        /// <param name="formatting">json格式化参数</param>
        /// <returns>json bytes</returns>
        public static byte[] ConvertToBytes<T>(this T obj) where T : class, new()
        {
            if (obj == null)
            {
                return null;
            }

            var json = JsonConvert.SerializeObject(obj, typeof(T), settings);
            var bytes = Encoding.UTF8.GetBytes(json);
            return bytes;
        }

        /// <summary>
        /// object转bytes
        /// </summary>
        /// <typeparam name="T">泛型对象,class约束</typeparam>
        /// <param name="obj">泛型对象,class约束</param>
        /// <param name="formatting">json格式化参数</param>
        /// <returns>json bytes</returns>
        public static byte[] ConvertToBytes<T>(this T obj, Formatting formatting) where T : class, new()
        {
            if (obj == null)
            {
                return null;
            }

            var json = JsonConvert.SerializeObject(obj, typeof(T), formatting, settings);
            var bytes = Encoding.UTF8.GetBytes(json);
            return bytes;
        }

        /// <summary>
        /// json string转object
        /// </summary>
        /// <typeparam name="T">泛型对象,class约束</typeparam>
        /// <param name="json">json string</param>
        /// <returns>泛型对象,class约束</returns>
        public static T ConvertToObject<T>(this string json) where T : class, new()
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            var obj = JsonConvert.DeserializeObject<T>(json, settings);
            return obj;
        }

        /// <summary>
        /// json bytes转object
        /// </summary>
        /// <typeparam name="T">泛型对象,class约束</typeparam>
        /// <param name="bytes">json bytes</param>
        /// <returns>泛型对象,class约束</returns>
        public static T ConvertToObject<T>(this byte[] bytes) where T : class, new()
        {
            if(bytes == null)
            {
                return null;
            }

            var json = Encoding.UTF8.GetString(bytes);
            var obj = json.ConvertToObject<T>();
            return obj;
        }
    }
}
