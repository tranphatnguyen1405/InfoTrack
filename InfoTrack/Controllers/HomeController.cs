using Common;
using InfoTrack.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Web;

namespace InfoTrack.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppConfigurations _appConfigurations;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            AppConfigurations appConfigurations,
            ILogger<HomeController> logger)
        {
            _appConfigurations = appConfigurations;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        public async Task<JsonResult> Search([FromBody] SearchParam searchParam)
        {
            try
            {
                if (searchParam == null ||
                    string.IsNullOrEmpty(searchParam.Search) ||
                    string.IsNullOrEmpty(searchParam.Url))
                {
                    return Json(new { success = false, message = "Must provide SearchPhrase and Url data." });
                }

                int searchNumber = _appConfigurations.SearchNumber;
                int maxNumAtOneCall = _appConfigurations.MaxNumAtOneCall;

                int start = 0;
                string urlString = string.Empty;
                List<int> results = new List<int>();
                while (searchNumber > 0)
                {
                    int exactSearchNum = (searchNumber >= maxNumAtOneCall) ? maxNumAtOneCall : searchNumber;

                    urlString = $"http://www.google.com/search?q={HttpUtility.UrlEncode(searchParam.Search)}&num={exactSearchNum}&start={start}";

                    results.AddRange(await GetSearchedIndexes(urlString, exactSearchNum, start, keyword: searchParam.Url));

                    start += exactSearchNum;
                    searchNumber -= exactSearchNum;
                }

                //List<int> results = new List<int> { 14, 29 };

                string msg = (results == null || results.Count == 0) ? "No data found" : ("Indexes: " + string.Join(", ", results));

                return Json(new { success = true, message = msg });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private async Task<List<int>> GetSearchedIndexes(string urlString, int exactSearchNum, int start, string keyword)
        {
            using HttpClient client = new HttpClient();

            var sss = await client.GetStringAsync(urlString);

            string hrefPattern = @"href\s*=\s*(?:[""'](?<1>[^""']*)[""']|(?<1>[^>\s]+))";
            string filterStr = "/url?q=";
            var urls = Regex.Matches(sss, hrefPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(2))
                            .Select(x => x.Groups[1].Value)
                            .Where(x => x.StartsWith(filterStr + "http"))
                            .Select(x => x.Substring(filterStr.Length))
                            .ToList();

            string httpPattern = @"(http:|https:)\/\/(.*?)\/";
            List<string> filteredUrls = urls.Select(x =>
                                                {
                                                    return Regex.Match(x, httpPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled).Groups[0].Value
                                                                .Replace("https://", string.Empty)
                                                                .Replace("http://", string.Empty);
                                                })
                                            .Distinct()
                                            .ToList();

            if (filteredUrls.Count > exactSearchNum &&
                filteredUrls[filteredUrls.Count - 1].Contains("accounts.google.com"))
            {
                filteredUrls.RemoveAt(filteredUrls.Count - 1);
            }

            if (filteredUrls.Count > exactSearchNum &&
                filteredUrls[filteredUrls.Count - 1].Contains("support.google.com"))
            {
                filteredUrls.RemoveAt(filteredUrls.Count - 1);
            }

            if (filteredUrls.Count > exactSearchNum &&
                (filteredUrls[0].Contains("www.google.com") || filteredUrls[0].Contains("maps.google.com")))
            {
                filteredUrls.RemoveAt(0);
            }

            return Enumerable.Range(start, filteredUrls.Count)
                     .Where(i => filteredUrls[i - start].Contains(keyword))
                     .ToList();
        }
    }
}
