using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using Mcp.Net.Core.Attributes;

namespace Mcp.Net.Examples.ExternalTools;

[McpTool("googleSearch", "Search the web using Google Custom Search API")]
public class GoogleSearchTool
{
    private readonly HttpClient _httpClient;
    private const string ApiKey = "";
    private const string SearchEngineId = "";

    public GoogleSearchTool()
    {
        _httpClient = new HttpClient();
    }

    [McpTool("search", "Search the web using Google's Custom Search API")]
    public async Task<GoogleSearchResults> SearchAsync(
        [McpParameter(required: true, description: "The search query to execute")] string query,
        [McpParameter(
            required: false,
            description: "Maximum number of results to return (1-20, default is 10)"
        )]
            int maxResults = 10
    )
    {
        // Check for empty API key or Search Engine ID
        if (string.IsNullOrEmpty(ApiKey))
        {
            return new GoogleSearchResults
            {
                Query = query,
                TotalResults = 0,
                Error = "Google API Key is not configured. Please add a valid API key to use this tool.",
                Results = new List<SearchResult>()
            };
        }
        
        if (string.IsNullOrEmpty(SearchEngineId))
        {
            return new GoogleSearchResults
            {
                Query = query,
                TotalResults = 0,
                Error = "Google Search Engine ID is not configured. Please add a valid Search Engine ID to use this tool.",
                Results = new List<SearchResult>()
            };
        }

        // Validate and adjust maxResults
        maxResults = Math.Clamp(maxResults, 1, 20);

        // Build the Google Custom Search API URL
        var encodedQuery = HttpUtility.UrlEncode(query);
        var requestUrl =
            $"https://www.googleapis.com/customsearch/v1?key={ApiKey}&cx={SearchEngineId}&q={encodedQuery}&num={maxResults}";

        try
        {
            // Send the request
            var response = await _httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();

            // Parse the JSON response
            var json = await response.Content.ReadAsStringAsync();
            var searchResult = JsonSerializer.Deserialize<GoogleApiResponse>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (searchResult == null || searchResult.Items == null || !searchResult.Items.Any())
            {
                return new GoogleSearchResults
                {
                    Query = query,
                    TotalResults = 0,
                    Results = new List<SearchResult>(),
                };
            }

            // Extract and format the results
            var results = searchResult
                .Items.Select(item => new SearchResult
                {
                    Title = CleanHtml(item.Title),
                    Link = item.Link,
                    Snippet = CleanHtml(item.Snippet),
                    DisplayLink = item.DisplayLink,
                })
                .ToList();

            return new GoogleSearchResults
            {
                Query = query,
                TotalResults = results.Count,
                Results = results,
            };
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Error searching Google: {ex.Message}");
        }
        catch (JsonException ex)
        {
            throw new Exception($"Error parsing Google search results: {ex.Message}");
        }
    }

    // Clean HTML tags and special characters from strings
    private string CleanHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        // Remove HTML tags
        var withoutTags = Regex.Replace(html, "<.*?>", string.Empty);

        // Decode HTML entities
        return HttpUtility.HtmlDecode(withoutTags);
    }
}

// Classes to represent Google Custom Search API responses
public class GoogleApiResponse
{
    [JsonPropertyName("items")]
    public List<GoogleSearchItem>? Items { get; set; }
}

public class GoogleSearchItem
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("link")]
    public string Link { get; set; } = string.Empty;

    [JsonPropertyName("snippet")]
    public string Snippet { get; set; } = string.Empty;

    [JsonPropertyName("displayLink")]
    public string DisplayLink { get; set; } = string.Empty;
}

// Models for API response
public class GoogleSearchResults
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("totalResults")]
    public int TotalResults { get; set; }

    [JsonPropertyName("results")]
    public List<SearchResult> Results { get; set; } = new();
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class SearchResult
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("link")]
    public string Link { get; set; } = string.Empty;

    [JsonPropertyName("snippet")]
    public string Snippet { get; set; } = string.Empty;

    [JsonPropertyName("displayLink")]
    public string DisplayLink { get; set; } = string.Empty;
}
