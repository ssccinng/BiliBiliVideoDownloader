﻿using BiliBiliVideoDownloader;
using BiliBiliCore;

BangumiTools BangumiTools = new("your sessdata");

// 下载番剧
await BangumiTools.DownEpAsync("https://www.bilibili.com/bangumi/play/ss41410?t=707", true, Quality.Q720p);
await BangumiTools.DownEpAsync("https://www.bilibili.com/bangumi/play/ss2102?spm_id_from=333.337.0.0", epIndex: 2);

// 下载bv
await BangumiTools.DownBVAsync("BV1zq4y1h7Q8");

// 下载漫画
BiliBiliManga.SetSessData("your sessdata");
BiliBiliManga BiliManga = new BiliBiliManga(28201);
await BiliManga.DownloadAsync();