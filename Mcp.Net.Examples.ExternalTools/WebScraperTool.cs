using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Mcp.Net.Core.Attributes;

namespace Mcp.Net.Examples.ExternalTools;

/// <summary>
/// A tool that fetches and cleans web content for easy reading
/// </summary>
[McpTool("webScraper", "Fetches and cleans web content from a URL")]
public class WebScraperTool
{
    private readonly HttpClient _httpClient;

    public WebScraperTool()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mcp.Net Web Scraper Tool/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Fetches a webpage and returns a cleaned version of its content
    /// </summary>
    /// <param name="url">The URL of the webpage to fetch and clean</param>
    /// <param name="maxContentLength">Maximum length of content to return</param>
    /// <returns>The cleaned content of the webpage</returns>
    [McpTool("webscrape_fetchAndCleanPage", "Fetches a webpage and returns a cleaned version of its content")]
    public async Task<ScrapedWebContent> FetchAndCleanPageAsync(
        [McpParameter(required: true, description: "The URL of the webpage to fetch")] string url,
        [McpParameter(
            required: false,
            description: "Maximum length of content to return (default: 10000)"
        )]
            int maxContentLength = 10000
    )
    {
        if (string.IsNullOrEmpty(url))
        {
            return new ScrapedWebContent
            {
                Success = false,
                ErrorMessage = "Please provide a valid URL.",
            };
        }

        try
        {
            // Validate URL format
            if (
                !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            )
            {
                return new ScrapedWebContent
                {
                    Success = false,
                    ErrorMessage =
                        $"Invalid URL format: {url}. Please provide a valid HTTP or HTTPS URL.",
                };
            }

            // Ensure safe domains
            if (!IsSafeDomain(uri.Host))
            {
                return new ScrapedWebContent
                {
                    Success = false,
                    ErrorMessage =
                        $"Access to domain {uri.Host} is restricted for security reasons.",
                };
            }

            // Fetch the webpage content
            using var response = await _httpClient.GetAsync(uri);
            response.EnsureSuccessStatusCode();

            string contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

            // Check if content is HTML
            if (!contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
            {
                return new ScrapedWebContent
                {
                    Success = false,
                    ErrorMessage =
                        $"Content type '{contentType}' is not supported. Only HTML content can be scraped.",
                };
            }

            string htmlContent = await response.Content.ReadAsStringAsync();

            // Clean the HTML content using HtmlAgilityPack
            (string cleanedContent, string title, string metaDescription) = CleanHtmlContent(
                htmlContent,
                maxContentLength
            );

            return new ScrapedWebContent
            {
                Success = true,
                Url = uri.ToString(),
                Title = title,
                MetaDescription = metaDescription,
                Content = cleanedContent,
                ContentLength = cleanedContent.Length,
                Source = uri.Host,
                ScrapedAt = DateTime.UtcNow,
                Truncated = cleanedContent.Length >= maxContentLength,
            };
        }
        catch (HttpRequestException ex)
        {
            return new ScrapedWebContent
            {
                Success = false,
                ErrorMessage = $"Error fetching the webpage: {ex.Message}",
            };
        }
        catch (TaskCanceledException)
        {
            return new ScrapedWebContent
            {
                Success = false,
                ErrorMessage = "Request timed out. The website might be too slow or unavailable.",
            };
        }
        catch (Exception ex)
        {
            return new ScrapedWebContent
            {
                Success = false,
                ErrorMessage = $"An error occurred: {ex.Message}",
            };
        }
    }

    /// <summary>
    /// Fetches all links from a webpage and returns them as a list
    /// </summary>
    /// <param name="url">The URL of the webpage to fetch links from</param>
    /// <param name="includeExternal">Whether to include links to external domains</param>
    /// <returns>A list of links found on the webpage</returns>
    [McpTool("webscrape_fetchLinks", "Fetches all links from a webpage and returns them as a list")]
    public async Task<WebLinks> FetchLinksAsync(
        [McpParameter(required: true, description: "The URL of the webpage to fetch links from")]
            string url,
        [McpParameter(
            required: false,
            description: "Whether to include links to external domains (default: false)"
        )]
            bool includeExternal = false
    )
    {
        if (string.IsNullOrEmpty(url))
        {
            return new WebLinks { Success = false, ErrorMessage = "Please provide a valid URL." };
        }

        try
        {
            // Validate URL format
            if (
                !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            )
            {
                return new WebLinks
                {
                    Success = false,
                    ErrorMessage =
                        $"Invalid URL format: {url}. Please provide a valid HTTP or HTTPS URL.",
                };
            }

            // Ensure safe domains
            if (!IsSafeDomain(uri.Host))
            {
                return new WebLinks
                {
                    Success = false,
                    ErrorMessage =
                        $"Access to domain {uri.Host} is restricted for security reasons.",
                };
            }

            // Fetch the webpage content
            using var response = await _httpClient.GetAsync(uri);
            response.EnsureSuccessStatusCode();

            string contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

            // Check if content is HTML
            if (!contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
            {
                return new WebLinks
                {
                    Success = false,
                    ErrorMessage =
                        $"Content type '{contentType}' is not supported. Only HTML content can be processed.",
                };
            }

            string htmlContent = await response.Content.ReadAsStringAsync();

            // Extract links using HtmlAgilityPack
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            var links =
                doc.DocumentNode.SelectNodes("//a[@href]")
                    ?.Select(a => a.GetAttributeValue("href", string.Empty))
                    .Where(href => !string.IsNullOrWhiteSpace(href))
                    .Select(href => NormalizeUrl(href, uri))
                    .Where(href => !string.IsNullOrWhiteSpace(href))
                    .Distinct()
                    .ToList() ?? new List<string>();

            // Filter external links if not requested
            if (!includeExternal)
            {
                links = links
                    .Where(link =>
                        Uri.TryCreate(link, UriKind.Absolute, out Uri? linkUri)
                        && linkUri.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();
            }

            return new WebLinks
            {
                Success = true,
                Url = uri.ToString(),
                PageTitle = ExtractTitle(doc),
                Links = links,
                InternalLinksCount = links.Count(link =>
                    Uri.TryCreate(link, UriKind.Absolute, out Uri? linkUri)
                    && linkUri.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase)
                ),
                ExternalLinksCount = links.Count(link =>
                    Uri.TryCreate(link, UriKind.Absolute, out Uri? linkUri)
                    && !linkUri.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase)
                ),
                ScrapedAt = DateTime.UtcNow,
            };
        }
        catch (HttpRequestException ex)
        {
            return new WebLinks
            {
                Success = false,
                ErrorMessage = $"Error fetching the webpage: {ex.Message}",
            };
        }
        catch (TaskCanceledException)
        {
            return new WebLinks
            {
                Success = false,
                ErrorMessage = "Request timed out. The website might be too slow or unavailable.",
            };
        }
        catch (Exception ex)
        {
            return new WebLinks
            {
                Success = false,
                ErrorMessage = $"An error occurred: {ex.Message}",
            };
        }
    }

    /// <summary>
    /// Normalizes a URL, converting relative URLs to absolute
    /// </summary>
    private string NormalizeUrl(string href, Uri baseUri)
    {
        if (string.IsNullOrWhiteSpace(href))
            return string.Empty;

        // Skip fragment-only URLs and javascript links
        if (
            href.StartsWith("#")
            || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)
        )
            return string.Empty;

        try
        {
            return new Uri(baseUri, href).ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Extracts title from HTML document
    /// </summary>
    private string ExtractTitle(HtmlDocument doc)
    {
        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        return titleNode != null ? titleNode.InnerText.Trim() : string.Empty;
    }

    /// <summary>
    /// Cleans HTML content using HtmlAgilityPack, which provides more robust HTML parsing
    /// </summary>
    private (string content, string title, string metaDescription) CleanHtmlContent(
        string html,
        int maxLength
    )
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Extract title
        string title =
            doc.DocumentNode.SelectSingleNode("//title")?.InnerText.Trim() ?? string.Empty;

        // Extract meta description
        string metaDescription =
            doc.DocumentNode.SelectSingleNode("//meta[@name='description']")
                ?.GetAttributeValue("content", string.Empty) ?? string.Empty;

        // Remove scripts, styles, and comments
        var nodesToRemove = new List<HtmlNode>();
        nodesToRemove.AddRange(
            doc.DocumentNode.SelectNodes("//script") ?? Enumerable.Empty<HtmlNode>()
        );
        nodesToRemove.AddRange(
            doc.DocumentNode.SelectNodes("//style") ?? Enumerable.Empty<HtmlNode>()
        );
        nodesToRemove.AddRange(
            doc.DocumentNode.SelectNodes("//comment()") ?? Enumerable.Empty<HtmlNode>()
        );
        nodesToRemove.AddRange(
            doc.DocumentNode.SelectNodes("//iframe") ?? Enumerable.Empty<HtmlNode>()
        );
        nodesToRemove.AddRange(
            doc.DocumentNode.SelectNodes("//nav") ?? Enumerable.Empty<HtmlNode>()
        );
        nodesToRemove.AddRange(
            doc.DocumentNode.SelectNodes("//footer") ?? Enumerable.Empty<HtmlNode>()
        );
        nodesToRemove.AddRange(
            doc.DocumentNode.SelectNodes("//aside") ?? Enumerable.Empty<HtmlNode>()
        );

        foreach (var node in nodesToRemove)
        {
            node.Remove();
        }

        var contentBuilder = new StringBuilder();

        // Add title
        if (!string.IsNullOrEmpty(title))
        {
            contentBuilder.AppendLine($"# {title}");
            contentBuilder.AppendLine();
        }

        // Try to find main content area
        HtmlNode? mainContent = FindMainContentNode(doc);

        if (mainContent != null)
        {
            ExtractStructuredContent(mainContent, contentBuilder);
        }
        else
        {
            // Fallback to processing the entire body
            var body = doc.DocumentNode.SelectSingleNode("//body");
            if (body != null)
            {
                ExtractStructuredContent(body, contentBuilder);
            }
        }

        string cleanedContent = contentBuilder.ToString().Trim();

        // Truncate if necessary
        if (cleanedContent.Length > maxLength)
        {
            cleanedContent = cleanedContent.Substring(0, maxLength) + "... [content truncated]";
        }

        return (cleanedContent, title, metaDescription);
    }

    /// <summary>
    /// Attempts to find the main content node of the page using common patterns
    /// </summary>
    private HtmlNode? FindMainContentNode(HtmlDocument doc)
    {
        // Try to find content using common IDs and class names
        string[] contentSelectors =
        {
            "//main",
            "//article",
            "//div[@id='content']",
            "//div[@id='main-content']",
            "//div[@class='content']",
            "//div[@class='main-content']",
            "//div[contains(@class, 'content')]",
            "//div[contains(@class, 'article')]",
        };

        foreach (var selector in contentSelectors)
        {
            var node = doc.DocumentNode.SelectSingleNode(selector);
            if (node != null)
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts structured content from an HTML node and formats it in Markdown
    /// </summary>
    private void ExtractStructuredContent(HtmlNode node, StringBuilder output)
    {
        // Process headings
        for (int i = 1; i <= 6; i++)
        {
            var headings = node.SelectNodes($".//h{i}");
            if (headings != null)
            {
                foreach (var heading in headings)
                {
                    string text = CleanText(heading.InnerText);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        output.AppendLine($"{new string('#', i)} {text}");
                        output.AppendLine();
                    }
                }
            }
        }

        // Process paragraphs
        var paragraphs = node.SelectNodes(".//p");
        if (paragraphs != null)
        {
            foreach (var p in paragraphs)
            {
                string text = CleanText(p.InnerText);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    output.AppendLine(text);
                    output.AppendLine();
                }
            }
        }

        // Process lists
        var lists = node.SelectNodes(".//ul|.//ol");
        if (lists != null)
        {
            foreach (var list in lists)
            {
                var items = list.SelectNodes(".//li");
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        string text = CleanText(item.InnerText);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            output.AppendLine($"- {text}");
                        }
                    }
                    output.AppendLine();
                }
            }
        }

        // Process tables
        var tables = node.SelectNodes(".//table");
        if (tables != null)
        {
            foreach (var table in tables)
            {
                ProcessTable(table, output);
            }
        }
    }

    /// <summary>
    /// Processes an HTML table and formats it in Markdown
    /// </summary>
    private void ProcessTable(HtmlNode table, StringBuilder output)
    {
        var rows = table.SelectNodes(".//tr");
        if (rows == null || rows.Count == 0)
            return;

        bool hasHeader = table.SelectNodes(".//th") != null;

        // Process header row if exists
        if (hasHeader)
        {
            var headerRow = rows[0];
            var headerCells = headerRow.SelectNodes(".//th");

            if (headerCells != null)
            {
                output.Append("| ");
                foreach (var cell in headerCells)
                {
                    output.Append(CleanText(cell.InnerText));
                    output.Append(" | ");
                }
                output.AppendLine();

                // Add separator row
                output.Append("| ");
                for (int i = 0; i < headerCells.Count; i++)
                {
                    output.Append("--- | ");
                }
                output.AppendLine();

                // Process body rows
                for (int i = 1; i < rows.Count; i++)
                {
                    var cells = rows[i].SelectNodes(".//td");
                    if (cells != null)
                    {
                        output.Append("| ");
                        foreach (var cell in cells)
                        {
                            output.Append(CleanText(cell.InnerText));
                            output.Append(" | ");
                        }
                        output.AppendLine();
                    }
                }
            }
        }
        else
        {
            // Process all rows as body rows
            foreach (var row in rows)
            {
                var cells = row.SelectNodes(".//td");
                if (cells != null)
                {
                    output.Append("| ");
                    foreach (var cell in cells)
                    {
                        output.Append(CleanText(cell.InnerText));
                        output.Append(" | ");
                    }
                    output.AppendLine();
                }
            }
        }

        output.AppendLine();
    }

    /// <summary>
    /// Cleans text by removing extra whitespace and normalizing
    /// </summary>
    private string CleanText(string text)
    {
        // Decode HTML entities
        text = System.Net.WebUtility.HtmlDecode(text);

        // Remove excess whitespace
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text;
    }

    /// <summary>
    /// Checks if a domain is safe to access
    /// </summary>
    private bool IsSafeDomain(string host)
    {
        // Blocklist of potentially unsafe or private domains
        string[] blockedDomains =
        {
            "localhost",
            "127.0.0.1",
            "0.0.0.0",
            "169.254",
            "10.",
            "172.16",
            "172.17",
            "172.18",
            "172.19",
            "172.20",
            "172.21",
            "172.22",
            "172.23",
            "172.24",
            "172.25",
            "172.26",
            "172.27",
            "172.28",
            "172.29",
            "172.30",
            "172.31",
            "192.168",
        };

        return !blockedDomains.Any(domain =>
            host.Contains(domain, StringComparison.OrdinalIgnoreCase)
        );
    }
}

/// <summary>
/// Represents scraped web content
/// </summary>
public class ScrapedWebContent
{
    /// <summary>
    /// Whether the scraping was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if scraping failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The URL that was scraped
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The title of the webpage
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The meta description of the webpage
    /// </summary>
    public string MetaDescription { get; set; } = string.Empty;

    /// <summary>
    /// The cleaned content of the webpage
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// The length of the cleaned content
    /// </summary>
    public int ContentLength { get; set; }

    /// <summary>
    /// The source domain of the webpage
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// When the webpage was scraped
    /// </summary>
    public DateTime ScrapedAt { get; set; }

    /// <summary>
    /// Whether the content was truncated due to length
    /// </summary>
    public bool Truncated { get; set; }
}

/// <summary>
/// Represents links extracted from a webpage
/// </summary>
public class WebLinks
{
    /// <summary>
    /// Whether the link extraction was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if extraction failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The URL that was processed
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The title of the webpage
    /// </summary>
    public string PageTitle { get; set; } = string.Empty;

    /// <summary>
    /// List of links found on the webpage
    /// </summary>
    public List<string> Links { get; set; } = new List<string>();

    /// <summary>
    /// Number of internal links
    /// </summary>
    public int InternalLinksCount { get; set; }

    /// <summary>
    /// Number of external links
    /// </summary>
    public int ExternalLinksCount { get; set; }

    /// <summary>
    /// When the links were extracted
    /// </summary>
    public DateTime ScrapedAt { get; set; }
}
