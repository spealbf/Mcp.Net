using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using HtmlAgilityPack;
using Mcp.Net.Core.Attributes;

namespace Mcp.Net.Examples.SimpleServer
{
    public class WebScraperTool
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly HtmlParser _htmlParser = new HtmlParser();

        static WebScraperTool()
        {
            // Set default headers to mimic a browser
            _httpClient.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36"
            );
            _httpClient.DefaultRequestHeaders.Add(
                "Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8"
            );
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        [McpTool("web_scrape", "Scrape and sanitize content from a website")]
        public static async Task<WebScrapingResult> ScrapeWebsite(
            [McpParameter(required: true, description: "URL of the website to scrape")] string url,
            [McpParameter(
                required: false,
                description: "Extract specific type of content (article, main, all)"
            )]
                string contentType = "article",
            [McpParameter(required: false, description: "Maximum content length to return")]
                int maxLength = 50000,
            [McpParameter(required: false, description: "Extract text only (remove HTML tags)")]
                bool textOnly = false,
            [McpParameter(required: false, description: "Extract images (urls only)")]
                bool extractImages = false
        )
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("URL cannot be empty");

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
                throw new ArgumentException("Invalid URL format");

            try
            {
                // Fetch the HTML
                string html = await _httpClient.GetStringAsync(uri);

                // Two-step processing
                // 1. Use HtmlAgilityPack for initial cleaning and extraction
                var initialContent = ExtractContentWithHtmlAgilityPack(html, contentType);

                // 2. Use AngleSharp for better content extraction and sanitization
                string finalContent;
                List<string> imageUrls = new List<string>();

                if (textOnly)
                {
                    // Extract text only
                    var config = Configuration.Default;
                    var context = BrowsingContext.New(config);
                    var document = await context.OpenAsync(req => req.Content(initialContent));
                    finalContent = document.Body?.TextContent ?? "";

                    // Clean up whitespace in text-only mode
                    finalContent = Regex.Replace(finalContent, @"\s+", " ");
                    finalContent = Regex.Replace(finalContent, @"\n\s*\n", "\n\n");
                }
                else
                {
                    // Clean and sanitize HTML
                    finalContent = await SanitizeWithAngleSharp(initialContent);
                }

                // Extract image URLs if requested
                if (extractImages)
                {
                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(html);

                    var images = htmlDoc.DocumentNode.SelectNodes("//img[@src]");
                    if (images != null)
                    {
                        foreach (var img in images)
                        {
                            string src = img.GetAttributeValue("src", "");
                            if (!string.IsNullOrEmpty(src))
                            {
                                // Make relative URLs absolute
                                if (src.StartsWith("/"))
                                {
                                    string baseUrl = $"{uri.Scheme}://{uri.Host}";
                                    src = baseUrl + src;
                                }
                                else if (!src.StartsWith("http"))
                                {
                                    // Handle relative URLs without leading slash
                                    string baseUrl = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
                                    if (!baseUrl.EndsWith("/"))
                                        baseUrl = baseUrl.Substring(
                                            0,
                                            baseUrl.LastIndexOf('/') + 1
                                        );
                                    src = baseUrl + src;
                                }

                                imageUrls.Add(src);
                            }
                        }
                    }
                }

                // Truncate if needed
                if (finalContent.Length > maxLength)
                {
                    finalContent = finalContent.Substring(0, maxLength) + "...(content truncated)";
                }

                var result = new WebScrapingResult
                {
                    Url = url,
                    Title = ExtractTitle(html),
                    ContentType = contentType,
                    Content = finalContent,
                    MetaDescription = ExtractMetaDescription(html),
                    ImageUrls = imageUrls,
                    ScrapedAt = DateTime.UtcNow,
                };

                return result;
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Failed to fetch content from URL: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error scraping website: {ex.Message}");
            }
        }

        private static string ExtractContentWithHtmlAgilityPack(string html, string contentType)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            // Remove scripts, styles, and comments
            var nodesToRemove = new List<HtmlNode>();
            foreach (var node in htmlDoc.DocumentNode.Descendants())
            {
                string nodeName = node.Name.ToLowerInvariant();
                if (
                    nodeName == "script"
                    || nodeName == "style"
                    || nodeName == "iframe"
                    || nodeName == "noscript"
                    || nodeName == "svg"
                    || node.NodeType == HtmlNodeType.Comment
                )
                {
                    nodesToRemove.Add(node);
                }
            }

            foreach (var node in nodesToRemove)
            {
                node.Remove();
            }

            // Extract main content based on contentType
            HtmlNode? mainContentNode = null;

            if (contentType.ToLowerInvariant() == "article")
            {
                // Try to find article content
                mainContentNode =
                    htmlDoc.DocumentNode.SelectSingleNode("//article")
                    ?? htmlDoc.DocumentNode.SelectSingleNode("//main")
                    ?? htmlDoc.DocumentNode.SelectSingleNode("//div[@id='content']")
                    ?? htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'content')]")
                    ?? htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'article')]")
                    ?? htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'post')]");
            }
            else if (contentType.ToLowerInvariant() == "main")
            {
                // Get the main content
                mainContentNode =
                    htmlDoc.DocumentNode.SelectSingleNode("//main")
                    ?? htmlDoc.DocumentNode.SelectSingleNode("//div[@role='main']")
                    ?? htmlDoc.DocumentNode.SelectSingleNode("//div[@id='main']")
                    ?? htmlDoc.DocumentNode.SelectSingleNode("//div[@class='main']");
            }

            // If we couldn't find a specific content element or contentType is "all", use the body
            if (mainContentNode == null || contentType.ToLowerInvariant() == "all")
            {
                mainContentNode = htmlDoc.DocumentNode.SelectSingleNode("//body");
            }

            // Also remove hidden elements
            var hiddenElements = htmlDoc.DocumentNode.SelectNodes(
                "//*[contains(@style, 'display: none') or contains(@style, 'display:none') or contains(@style, 'visibility: hidden') or contains(@style, 'visibility:hidden')]"
            );
            if (hiddenElements != null)
            {
                foreach (var node in hiddenElements)
                {
                    node.Remove();
                }
            }

            return mainContentNode?.InnerHtml ?? string.Empty;
        }

        private static async Task<string> SanitizeWithAngleSharp(string html)
        {
            var config = Configuration.Default;
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content(html));

            // Remove data attributes
            foreach (var element in document.QuerySelectorAll("*"))
            {
                // Create a list of attribute names to remove
                var attributesToRemove = new List<string>();
                foreach (var attr in element.Attributes)
                {
                    if (
                        attr.Name.StartsWith("data-")
                        || attr.Name.StartsWith("on")
                        || attr.Name == "style"
                        || attr.Name == "class"
                        || attr.Name == "id"
                    )
                    {
                        attributesToRemove.Add(attr.Name);
                    }
                }

                // Remove the attributes
                foreach (var attrName in attributesToRemove)
                {
                    element.RemoveAttribute(attrName);
                }
            }

            // Extract text with basic formatting
            var content = document.Body?.InnerHtml ?? "";

            // Clean up whitespace
            content = Regex.Replace(content, @"\s+", " ");
            content = Regex.Replace(content, @"\n\s*\n", "\n\n");

            return content;
        }

        private static string ExtractTitle(string html)
        {
            var match = Regex.Match(html, @"<title>(.*?)</title>");
            return match.Success ? match.Groups[1].Value.Trim() : "Unknown Title";
        }

        private static string ExtractMetaDescription(string html)
        {
            var match = Regex.Match(
                html,
                @"<meta\s+name=[""']description[""']\s+content=[""'](.*?)[""']",
                RegexOptions.IgnoreCase
            );
            if (match.Success)
                return match.Groups[1].Value.Trim();

            match = Regex.Match(
                html,
                @"<meta\s+content=[""'](.*?)[""']\s+name=[""']description[""']",
                RegexOptions.IgnoreCase
            );
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }
    }

    public class WebScrapingResult
    {
        public required string Url { get; set; }
        public required string Title { get; set; }
        public required string ContentType { get; set; }
        public required string Content { get; set; }
        public string MetaDescription { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = new List<string>();
        public DateTime ScrapedAt { get; set; }
    }
}
