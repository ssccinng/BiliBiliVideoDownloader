using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace BiliBiliVideoDownloader
{
    public enum Quality
    {
        Q1080p60 = 116,
        Q1080pplus = 112,
        Q1080p = 80,
        Q720p60 = 74,
        Q720p = 64,
        Q480p = 32,
        Q360p = 16,
    }

    public class EpInfo
    {
        public int Aid { get; set; }
        public int Cid { get; set; }
        public string TitleFormat { get; set; }
        public int Index { get; set; }
        public string LongTitle { get; set; }

        public override string ToString()
        {
            if (Index == 0)
            {
                return $"{TitleFormat} - {LongTitle}\taid = {Aid}\tcid = {Cid}";
            }
            else
            {
                return $"第{Index}话 - {LongTitle}\taid = {Aid}\tcid = {Cid}";
            }
        }
    }
    //项目: B站动漫番剧(bangumi)下载
    //版本2: 无加密API版,但是需要加入登录后cookie中的SESSDATA字段,才可下载720p及以上视频
    //API:
    //1.获取cid的api为 https://api.bilibili.com/x/web-interface/view?aid=47476691 aid后面为av号
    //2.下载链接api为 https://api.bilibili.com/x/player/playurl?avid=44743619&cid=78328965&qn=32 cid为上面获取到的 avid为输入的av号 qn为视频质量
    //注意:
    //但是此接口headers需要加上登录后'Cookie': 'SESSDATA=3c5d20cf%2C1556704080%2C7dcd8c41' (30天的有效期)(因为现在只有登录后才能看到720P以上视频了)
    //不然下载之后都是最低清晰度, 哪怕选择了80也是只有480p的分辨率!!
    public class BangumiTools
    {
        private HttpClient _httpClient { get; set; }
        private HttpClient _httpClientDown { get; set; }
        public Dictionary<string, string> Header { get; set; } = new Dictionary<string, string>
        {
            {"User-Agent", "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/55.0.2883.87 Safari/537.36"},
            {"Cookie", "SESSDATA=75a75cf2%2C1564669876%2Cb7c7b171" },
            {"Host", "api.bilibili.com" },
        };
        public BangumiTools(string sessData = null)
        {   if (sessData != null)
            {
                Header["Cookie"] = $"SESSDATA={sessData}";
            }
            _httpClient = new HttpClient();
            _httpClientDown = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", Header["User-Agent"]);

            _httpClientDown.DefaultRequestHeaders.Add("User-Agent", Header["User-Agent"]);
            _httpClientDown.DefaultRequestHeaders.Add("Cookie", Header["Cookie"]);
            _httpClientDown.DefaultRequestHeaders.Add("Host", Header["Host"]);
        }
        public async Task<bool> DownEpAsync(string url, bool downAll = false, Quality quality = Quality.Q720p)
        {
            string html = await _httpClient.GetStringAsync(url);

            Regex regex = new(@"INITIAL_STATE__=(.*?""]});");
            var a = regex.Match(html);
            List<EpInfo> epInfos = new List<EpInfo>();
            if (a.Success)
            {
                //Console.WriteLine(a.Groups[1].Value);
                var data = JsonDocument.Parse(a.Groups[1].Value).RootElement;

                if (downAll)
                {
                    var epList = data.GetProperty("epList");
                    for (int i = 0; i < epList.GetArrayLength(); i++)
                    {
                        //Console.WriteLine(epList[i]);

                        if (data.TryGetProperty("index", out var index))
                        {
                            epInfos.Add(new EpInfo
                            {
                                Aid = epList[i].GetProperty("aid").GetInt32(),
                                Cid = epList[i].GetProperty("cid").GetInt32(),
                                Index = index.GetInt32(),
                                LongTitle = epList[i].GetProperty("index_title").GetString(),
                            });;
                        }
                        else
                        {
                            epInfos.Add(new EpInfo
                            {
                                Aid = epList[i].GetProperty("aid").GetInt32(),
                                Cid = epList[i].GetProperty("cid").GetInt32(),
                                TitleFormat = epList[i].GetProperty("titleFormat").GetString(),
                                LongTitle = epList[i].GetProperty("longTitle").GetString(),
                            });
                            Console.WriteLine(epInfos.Last());
                        }
                    }
                }
                else
                {
                    var epInfo = data.GetProperty("epInfo");
                    if (data.TryGetProperty("index", out var index))
                    {
                        epInfos.Add(new EpInfo
                        {
                            Aid = epInfo.GetProperty("aid").GetInt32(),
                            Cid = epInfo.GetProperty("cid").GetInt32(),
                            Index = index.GetInt32(),
                            LongTitle = epInfo.GetProperty("index_title").GetString(),
                        }); ;
                    }
                    else
                    {
                        epInfos.Add(new EpInfo
                        {
                            Aid = epInfo.GetProperty("aid").GetInt32(),
                            Cid = epInfo.GetProperty("cid").GetInt32(),
                            TitleFormat = epInfo.GetProperty("titleFormat").GetString(),
                            LongTitle = epInfo.GetProperty("longTitle").GetString(),
                        });
                        Console.WriteLine(epInfos.Last());
                    }
                }

                List<Task> downTasks = new List<Task>();
                foreach (var ep in epInfos)
                {

                    Console.WriteLine($"[正在下载: ] {ep}");
                    var videoList = await GetVideoList(ep, quality);
                    downTasks.Add(DownloadVideo(videoList, ep.LongTitle, url));
                }
                foreach (var task in downTasks)
                {
                    await task;
                }
                return true;
            }
            
            return false;
        }
        public async Task<List<string>> GetVideoList(EpInfo ep, Quality qualit)
        {
            return await GetVideoList(ep.Aid, ep.Cid, qualit);
        }
        public async Task<List<string>> GetVideoList(int aid, int cid, Quality quality)
        {
            List<string> videoList = new List<string>();
            string urlApi = $"https://api.bilibili.com/x/player/playurl?cid={cid}&avid={aid}&qn={(int)quality}";
            var data = JsonDocument.Parse(await _httpClientDown.GetStringAsync(urlApi)).RootElement;
            //Console.WriteLine(data);

            if (data.GetProperty("code").GetInt32() != 0)
            {
                Console.WriteLine("意!当前集数为B站大会员专享,若想下载,Cookie中请传入大会员的SESSDATA");
                return null;
            }
            var durl = data.GetProperty("data").GetProperty("durl");
            for (int i = 0; i < durl.GetArrayLength(); i++)
            {
                videoList.Add(durl[i].GetProperty("url").GetString());
                //Console.WriteLine(videoList.Last());
            }
            return videoList;
        }

        public async Task DownloadVideo(List<string> videoList, string title, string startUrl)
        {
            List<(string, string)> headers = new List<(string, string)> {
            ("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.13; rv:56.0) Gecko/20100101 Firefox/56.0"),
            ("Accept", "*/*"),
            ("Accept-Language", "en-US,en;q=0.5"),
            ("Accept-Encoding", "gzip, deflate, br"),
            ("Range", "bytes=0-"),
            ("Referer", startUrl),
            ("Origin", "https://www.bilibili.com"),
            ("Connection", "keep-alive"), };
            HttpClient httpClient = new HttpClient();
            
            httpClient.Timeout = Timeout.InfiniteTimeSpan;
            for (int i = 0; i < headers.Count; i++)
            {
                httpClient.DefaultRequestHeaders.Add(headers[i].Item1, headers[i].Item2);
            }
            int idx = 1;
            foreach (var video in videoList)
            {
                //using (HttpResponseMessage response = httpClient.GetAsync(video, HttpCompletionOption.ResponseHeadersRead).Result)
                //{
                //    response.EnsureSuccessStatusCode();

                //    using (Stream contentStream = await response.Content.ReadAsStreamAsync(), fileStream = new FileStream("test", FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                //    {
                //        var totalRead = 0L;
                //        var totalReads = 0L;
                //        var buffer = new byte[8192];
                //        var isMoreToRead = true;

                //        do
                //        {
                //            var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                //            if (read == 0)
                //            {
                //                isMoreToRead = false;
                //            }
                //            else
                //            {
                //                await fileStream.WriteAsync(buffer, 0, read);

                //                totalRead += read;
                //                totalReads += 1;

                //                if (totalReads % 2000 == 0)
                //                {
                //                    Console.WriteLine(string.Format("total bytes downloaded so far: {0:n0}", totalRead));
                //                }
                //            }
                //        }
                //        while (isMoreToRead);
                //    }
                //}
                var videoData = await httpClient.GetByteArrayAsync(video);
                if (videoList.Count > 1)
                {
                    File.WriteAllBytes($"{Regex.Replace(title, @"[\/\\:*?"" <>|]", "")} - {idx}.mp4", videoData);
                }
                else
                {
                    File.WriteAllBytes($"{Regex.Replace(title, @"[\/\\:*?"" <>|]", "")} .mp4", videoData);
                }
                idx++;
            }

        }
    }
}
