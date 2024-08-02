using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Newtonsoft.Json;
using RestSharp;

namespace WebApplication1.Controllers
{
    [RoutePrefix("user-insights")]
    public class UserInsightsController : ApiController
    {
        [HttpGet]
        [Route("{username}")]
        public async Task<IHttpActionResult> GetUserInsights(string username)
        {
            var client = new RestClient("https://api.github.com");
            var request = new RestRequest($"/users/{username}/repos", Method.Get);
            request.AddHeader("User-Agent", "request");
            request.AddHeader("Authorization", "token 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");

            var response = await client.ExecuteAsync(request);

            if (!response.IsSuccessful)
            {
                return Content((HttpStatusCode)response.StatusCode, response.Content);
            }

            var repos = JsonConvert.DeserializeObject<List<GitHubRepo>>(response.Content);

            var languages = new Dictionary<string, int>();
            var repoPRs = new Dictionary<string, int>();
            var events = new List<GitHubEvent>();

            foreach (var repo in repos)
            {
                // Get lenguajes
                var langRequest = new RestRequest($"/repos/{username}/{repo.Name}/languages", Method.Get);
                langRequest.AddHeader("User-Agent", "request");
                langRequest.AddHeader("Authorization", "token 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");
                var langResponse = await client.ExecuteAsync(langRequest);

                if (langResponse.IsSuccessful)
                {
                    var repoLanguages = JsonConvert.DeserializeObject<Dictionary<string, int>>(langResponse.Content);
                    foreach (var lang in repoLanguages)
                    {
                        if (languages.ContainsKey(lang.Key))
                            languages[lang.Key] += lang.Value;
                        else
                            languages[lang.Key] = lang.Value;
                    }
                }

                // Get pull requests
                var prRequest = new RestRequest($"/repos/{username}/{repo.Name}/pulls?state=closed", Method.Get);
                prRequest.AddHeader("User-Agent", "request");
                prRequest.AddHeader("Authorization", "token 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");
                var prResponse = await client.ExecuteAsync(prRequest);

                if (prResponse.IsSuccessful)
                {
                    var pullRequests = JsonConvert.DeserializeObject<List<GitHubPullRequest>>(prResponse.Content);
                    var mergedPRs = pullRequests.Count(pr => pr.MergedAt.HasValue);
                    repoPRs[repo.Name] = mergedPRs;
                }

                // Get events
                var eventsRequest = new RestRequest($"/repos/{username}/{repo.Name}/events", Method.Get);
                eventsRequest.AddHeader("User-Agent", "request");
                eventsRequest.AddHeader("Authorization", "token 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");
                var eventsResponse = await client.ExecuteAsync(eventsRequest);

                if (eventsResponse.IsSuccessful)
                {
                    var repoEvents = JsonConvert.DeserializeObject<List<GitHubEvent>>(eventsResponse.Content);
                    events.AddRange(repoEvents);
                }
            }

            //get the 3 most used languages
            var topLanguages = languages.OrderByDescending(l => l.Value).Take(3).Select(l => l.Key).ToList();

            // Get the repositories with the most merged pull requests
            var topRepos = repoPRs.OrderByDescending(r => r.Value).Take(3).ToList();

            // Get contributions in the last 6 months
            var sixMonthsAgo = DateTimeOffset.Now.AddMonths(-6);
            var contributions = events
                .Where(e => e.CreatedAt > sixMonthsAgo)
                .GroupBy(e => new { e.CreatedAt.Year, e.CreatedAt.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    PullRequests = g.Count(e => e.Type == "PullRequestEvent"),
                    Issues = g.Count(e => e.Type == "IssuesEvent"),
                    Commits = g.Count(e => e.Type == "PushEvent")
                })
                .ToList();

            // Analyze the busiest days and timeslots
            var activity = events
                .GroupBy(e => new
                {
                    DayOfWeek = e.CreatedAt.DayOfWeek,
                    TimeOfDay = GetTimeOfDay(e.CreatedAt.TimeOfDay)
                })
                .Select(g => new
                {
                    DayOfWeek = g.Key.DayOfWeek,
                    TimeOfDay = g.Key.TimeOfDay,
                    Count = g.Count()
                })
                .ToList();

            return Ok(new
            {
                TopLanguages = topLanguages,
                TopRepos = topRepos,
                Contributions = contributions,
                Activity = activity
            });
        }

        private string GetTimeOfDay(TimeSpan time)
        {
            if (time.Hours < 12)
                return "Morning";
            if (time.Hours < 18)
                return "Afternoon";
            return "Evening";
        }

        public class GitHubRepo
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            // Other fields as needed
        }

        public class GitHubPullRequest
        {
            [JsonProperty("merged_at")]
            public DateTimeOffset? MergedAt { get; set; }

            // Other fields as needed
        }

        public class GitHubEvent
        {
            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("created_at")]
            public DateTimeOffset CreatedAt { get; set; }

            // Other fields as needed
        }

    }
}