using System.IO.Compression;
using HtmlAgilityPack;
using RestSharp;

DownloadRockAndRoll downloadRockAndRoll = new DownloadRockAndRoll();
Console.WriteLine("Start search and download...");
downloadRockAndRoll.SearchAndDownload("red hot chili peppers");
Console.WriteLine("Finish");
class DownloadRockAndRoll
{
    private readonly string Url = "https://www.rockdownload.org/en/";
    private readonly string SavePath = @"/Users/nguyenmautuan/Desktop/";
    public void SearchAndDownload(string nameSongOrArtist)
    {
        var linkSearch = SearchSongOrArtist(nameSongOrArtist);
        var linkFileProviders =  GetLinkFileProvider(linkSearch);
        ProcessDownload(linkFileProviders);
    }
    private List<string> SearchSongOrArtist(string name)
    {
        var urlDownloads = new List<string>();
        string queryUrl = $"{Url}?s={name}";
        var web = new HtmlWeb();
        var doc = web.Load(queryUrl);
        bool found = FoundSong(doc);
        if (!found)
        {
            return urlDownloads;
        }
        var htmlNodes = doc.DocumentNode.SelectNodes("//main/section/div[@class='container']/div[@class='content']/h2[@class='site-title']");
        var albums = htmlNodes?.Where(x => x.InnerText == "Albums").FirstOrDefault();
        if (albums is null)
        {
            return urlDownloads;
        }
        var gridCards = albums.SelectNodes("//div[@class='grid-cards']");
        var cardWithExtends = gridCards.Where(x => x.InnerHtml.Contains("card-with-extends")).ToList();
        if (cardWithExtends.Count == 0)
        {
            return urlDownloads;
        }
        var cardWithExtend = cardWithExtends[0];
        var cardExtends = cardWithExtend.SelectNodes("//div[@class='card-extends']");
        foreach (var node in cardExtends)
        {
            var childNodes = node.ChildNodes;
            var nodeInput = childNodes[1].Descendants("input").FirstOrDefault();
            if (nodeInput is null)
            {
                continue;
            }
            var value = nodeInput?.GetAttributeValue("value", "");
            if (value == String.Empty)
            {
                continue;
            }
            urlDownloads.Add(value);
        }
        return urlDownloads;
    }

    private List<(string,string)> GetLinkFileProvider(List<string> urlDownloads)
    {
        var clientOpt = new RestClientOptions()
        {
            MaxTimeout =  10000
        };
        var dataFileProviders = new List<(string,string)>(); 
        var restRequest = new RestRequest($"{Url}/download",Method.Post);
        restRequest.AddParameter("action","redirect");
        restRequest.AlwaysMultipartFormData = true;
        var clientRequest = new RestClient(clientOpt);
        HtmlDocument htmlDocument = new HtmlDocument();
        foreach (var value in urlDownloads)
        {
            restRequest.AddParameter("down_id",value);
            var response = clientRequest.Execute(restRequest);
            if (response.IsSuccessStatusCode)
            {
                var content = response.Content;
                if (content != null)
                {
                    htmlDocument.LoadHtml(content);
                    var doc = htmlDocument.DocumentNode;
                    var shortLink = doc.SelectSingleNode("//link[@rel='shortlink']");
                    var link = shortLink.GetAttributeValue("href","");
                    if (link == String.Empty)
                    {
                        continue;
                    }
                    string valueFileProvider = link.Split('=')[1];
                    dataFileProviders.Add((link,valueFileProvider));
                }
            }
        }
        return dataFileProviders;
    }

    private void ProcessDownload(List<(string,string)> linkFileProviders)
    {
        foreach (var item in linkFileProviders)
        {
            var (url, value) = item;
            Thread thread = new Thread(() =>
            {
                Download(url, value);
            });
            thread.IsBackground = true;
            thread.Start();
        }
    }
    private void Download(string url,string value)
    {
        var clientOpt = new RestClientOptions()
        {
            MaxTimeout =  10000
        };
        var restRequest = new RestRequest(url,Method.Post);
            restRequest.AlwaysMultipartFormData = true;
            restRequest.AddParameter("id",value);
            restRequest.AddParameter("action","download");
            var clientRequest = new RestClient(clientOpt);
            var response = clientRequest.Execute(restRequest); 
            if (response.IsSuccessStatusCode)
            {
                var content = response.Content;
                if (content != null)
                {
                    HtmlDocument htmlDocument = new HtmlDocument();
                    htmlDocument.LoadHtml(content);
                    var doc = htmlDocument.DocumentNode;
                    var link = doc?.SelectSingleNode("//div[@id='alternate-download']/p/a[@id='forced-download']");
                    if (link is not null)
                    {
                        var href =link?.GetAttributeValue("href", "");
                        if (href != String.Empty)
                        {
                            var removeQuery = href?.Split('?')[0];
                            var dataSeparator = removeQuery?.Split('/');
                            var nameFile = dataSeparator?[dataSeparator.Length-1];
                            restRequest.Method = Method.Get;
                            restRequest.Resource = href;
                            var data = clientRequest.DownloadData(restRequest);
                            if (data is not null && data.Length > 0)
                            {
                                SaveFile(data, nameFile);
                            }
                        } 
                    }
                }
            }
    }

    private void SaveFile(byte[] data,string nameFile)
    {
        var zipPath = Path.Combine(SavePath, nameFile);
        File.WriteAllBytes(zipPath, data);
        ZipFile.ExtractToDirectory(zipPath,SavePath);
        File.Delete(zipPath); 
    }
    private bool FoundSong(HtmlDocument doc)
    {
        var htmlNodes = doc.DocumentNode.SelectNodes("//main/section/div[@class='container']/div[@class='content']");
        if (htmlNodes is null)
        {
            return false;
        }
        var notfound = htmlNodes.First().SelectNodes("p");
        if (notfound is not null)
        {
            return false;
        }
        return true;
    }
}

