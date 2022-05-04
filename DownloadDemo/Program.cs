using BiliBiliVideoDownloader;

BangumiTools BangumiTools = new("your sessdata");
// 下载番剧 目前值做了这个
await BangumiTools.DownEpAsync("https://www.bilibili.com/bangumi/play/ss41410?t=707", true, Quality.Q720p);