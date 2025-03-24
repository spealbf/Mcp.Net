using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Apis.CustomSearchAPI.v1;
using Google.Apis.Services;
using Mcp.Net.Core.Attributes;

namespace Mcp.Net.Examples.SimpleServer
{
    public class GoogleSearchTool
    {
        private const string GoogleApiKey = "YOUR_GOOGLE_API_KEY";
        private const string GoogleSearchEngineId = "YOUR_SEARCH_ENGINE_ID";

        [McpTool("google_search", "Search the web using Google")]
        public static async Task<SearchResult> Search(
            [McpParameter(required: true, description: "Search query")] string query,
            [McpParameter(required: false, description: "Number of results to return (max 10)")]
                int numResults = 5
        )
        {
            if (string.IsNullOrEmpty(query))
                throw new ArgumentException("Search query cannot be empty");

            if (numResults <= 0 || numResults > 10)
                numResults = 5;

            try
            {
                var customSearchService = new CustomSearchAPIService(
                    new BaseClientService.Initializer
                    {
                        ApiKey = GoogleApiKey,
                        ApplicationName = "MCP Server Google Search Tool",
                    }
                );

                var searchRequest = customSearchService.Cse.List();
                searchRequest.Q = query;
                searchRequest.Cx = GoogleSearchEngineId;
                searchRequest.Num = numResults;

                var searchResponse = await searchRequest.ExecuteAsync();

                var results = new List<SearchResultItem>();
                if (searchResponse.Items != null)
                {
                    foreach (var item in searchResponse.Items)
                    {
                        results.Add(
                            new SearchResultItem
                            {
                                Title = item.Title,
                                Link = item.Link,
                                Snippet = item.Snippet,
                                DisplayLink = item.DisplayLink,
                            }
                        );
                    }
                }

                return new SearchResult
                {
                    Query = query,
                    TotalResults = searchResponse.SearchInformation?.TotalResults ?? "0",
                    SearchTime = searchResponse.SearchInformation?.SearchTime ?? 0,
                    Results = results,
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error performing Google search: {ex.Message}");
            }
        }
    }

    public class SearchResult
    {
        public required string Query { get; set; }
        public required string TotalResults { get; set; }
        public double SearchTime { get; set; }
        public List<SearchResultItem> Results { get; set; } = new List<SearchResultItem>();
    }

    public class SearchResultItem
    {
        public required string Title { get; set; }
        public required string Link { get; set; }
        public required string Snippet { get; set; }
        public required string DisplayLink { get; set; }
    }
}
