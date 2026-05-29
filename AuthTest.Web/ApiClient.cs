using System;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace AuthTest.Web
{
    public static class ApiClient
    {
        private static readonly HttpClient Http;
        private static readonly string ApiKey;
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        static ApiClient()
        {
            ApiKey = ConfigurationManager.AppSettings["ApiKey"];
            Http = new HttpClient
            {
                BaseAddress = new Uri(ConfigurationManager.AppSettings["ApiBaseUrl"])
            };
            Http.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        }

        public static async Task<T> GetAsync<T>(string path)
        {
            var response = await Http.GetStringAsync(path);
            return Json.Deserialize<T>(response);
        }

        public static async Task<T> PostAsync<T>(string path, object body)
        {
            var json = Json.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await Http.PostAsync(path, content);
            var responseBody = await response.Content.ReadAsStringAsync();
            return Json.Deserialize<T>(responseBody);
        }

        public static async Task<HttpResponseMessage> PostRawAsync(string path, object body)
        {
            var json = Json.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await Http.PostAsync(path, content);
        }

        public static async Task<HttpResponseMessage> DeleteAsync(string path)
        {
            return await Http.DeleteAsync(path);
        }
    }
}
