using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace BiliBiliCore
{
    public class BiliBiliManga
    {
        

        static string URL_DETAIL = "https://manga.bilibili.com/twirp/comic.v2.Comic/ComicDetail?device=pc&platform=web";
        static string URL_IMAGE_INDEX = "https://manga.bilibili.com/twirp/comic.v1.Comic/GetImageIndex?device=pc&platform=web";
        static string URL_MANGA_HOST = "https://manga.hdslb.com";
        static string URL_IMAGE_TOKEN = "https://manga.bilibili.com/twirp/comic.v1.Comic/ImageToken?device=pc&platform=web";
        static string URL_SEARCH = "https://manga.bilibili.com/twirp/comic.v1.Comic/Search?device=pc&platform=web";
        private int _comicId { get; set; }

        private HttpClient _httpClient { get; set; }
        private static HttpClient _httpClientSearch { get; set; } = new HttpClient();

        public List<JsonElement> Eps { get; private set; } = new();
        private static string SESSDATA = null;
        public string Title { get; private set; }
        public string Evaluate { get; private set; }

        public BiliBiliManga(int ComicId)
        {
            _comicId = ComicId;

            _httpClient = new HttpClient();

            _httpClient.DefaultRequestHeaders.Add("Cookie", $"SESSDATA={SESSDATA}");

            var eps = GetChaptersAsync(_comicId).Result;
            Title = eps.GetProperty("title").ToString();
            Evaluate = eps.GetProperty("evaluate").GetString();
            eps = eps.GetProperty("ep_list");
            for (int i = eps.GetArrayLength() - 1, k = 1; i >= 0; --i, ++k)
            {
                Eps.Add(eps[i]);
            }

        }
        /// <summary>
        /// 在初始化前设置
        /// </summary>
        /// <param name="value"></param>
        public static void SetSessData(string value)
        {
            SESSDATA = value;

        }
        public async Task<List<string>> GetImages(int EPId)
        {
            var res1 =
                await _httpClient.PostAsJsonAsync("https://manga.bilibili.com/twirp/comic.v1.Comic/GetImageIndex?device=pc&platform=web",
                new { ep_id = EPId });
            var mangaData = await res1.Content.ReadFromJsonAsync<JsonElement>();
            mangaData = mangaData.GetProperty("data").GetProperty("images");
            List<string> urls = new();
            for (int j = 0; j < mangaData.GetArrayLength(); j++)
            {
                urls.Add(mangaData[j].GetProperty("path").GetString());
            }
            return await GetFullUrls(urls);
        }
        private async Task<List<string>> GetFullUrls(List<string> urls)
        {
            var res = await _httpClient.PostAsJsonAsync(URL_IMAGE_TOKEN, new { urls = JsonSerializer.Serialize(urls) });
            var data = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
            List<string> picres = new();
            for (int i = 0; i < data.GetArrayLength(); i++)
            {
                picres.Add($"{data[i].GetProperty("url")}?token={data[i].GetProperty("token")}");
            }
            return picres;
        }

        public async Task<JsonElement> GetChaptersAsync(int ComicId = -1)
        {
           ComicId = ComicId == -1? _comicId : ComicId;
            var res = await _httpClient.PostAsJsonAsync(
                URL_DETAIL, new { comic_id = ComicId });
            var data = await res.Content.ReadFromJsonAsync<JsonElement>();
            data = data.GetProperty("data");
            //Console.WriteLine(data.GetProperty("title"));
            //Console.WriteLine(data.GetProperty("evaluate"));

            return data;
            //return data.GetProperty("ep_list");
        }
        public static async Task<BiliBiliManga> SearchOne(string name)
        {
            //_httpClientSearch.DefaultRequestHeaders.Add("Cookie", "SESSDATA=045e584c%2C1649390286%2Cecc98%2Aa1");
            var res = await _httpClientSearch.PostAsJsonAsync(URL_SEARCH, new
            {
                key_word = name,
                page_num = 1,
                page_size = 9
            });
            if (res.IsSuccessStatusCode)
            {
                List<(string Title, int ComicId)> mangaList = new();
                var data = await res.Content.ReadFromJsonAsync<JsonElement>();
                data = data.GetProperty("data").GetProperty("list");
                //Console.WriteLine(data[0].GetRawText());
                //Console.WriteLine(data[0].GetProperty("id").GetInt32());
                if (data.GetArrayLength() > 0)
                {
                    return new BiliBiliManga(data[0].GetProperty("id").GetInt32());
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }
        public static async Task<List<(string Title, int ComicId)>?> Search(string name)
        {
            //_httpClientSearch.DefaultRequestHeaders.Add("Cookie", "SESSDATA=045e584c%2C1649390286%2Cecc98%2Aa1");
            var res = await _httpClientSearch.PostAsJsonAsync(URL_SEARCH, new
            {
                key_word = name,
                page_num = 1,
                page_size = 9
            } );
            if (res.IsSuccessStatusCode)
            {
                List<(string Title, int ComicId)> mangaList = new();
                var data = await res.Content.ReadFromJsonAsync<JsonElement>();
                data = data.GetProperty("data").GetProperty("list");
                for (int i = 0; i < data.GetArrayLength(); i++)
                {
                    string title = Regex.Replace(data[i].GetProperty("title").GetString(), @"\<.+?\>", "");
                    mangaList.Add((title, data[i].GetProperty("id").GetInt32()));
                }
                return mangaList;
            }
            else
            {
                return null;
            }
        }

        public async Task DownloadAsync()
        {
            for (int i = 0; i < Eps.Count; i++)
            {
                int idx = 1;
                int epid = Eps[i].GetProperty("id").GetInt32();

                var mcimgs = await GetImages(epid);
                var title = Eps[i].GetProperty("title").GetString();
                if (title.Length < 2)
                {
                    title = $"（未命名）";
                }
                if (!Directory.Exists(@$"manga\{Title}\{i + 1}.{title}"))
                {
                    Directory.CreateDirectory(@$"manga\{Title}\{i + 1}.{title}");
                }
                foreach (var pic in mcimgs)
                {
                    var img = await _httpClient.GetAsync(pic);
                    File.WriteAllBytes(@$"manga\{Title}\{i + 1}.{title}\{epid}_{idx++}.jpg", await img.Content.ReadAsByteArrayAsync());
                    Console.WriteLine(pic);
                }
            }
            return;
        }
    }
}
