namespace Mcp.Net.Examples.LLMConsole.UI;

public class ChatUI
{
    // For colorful console output
    private static readonly ConsoleColor DefaultColor = Console.ForegroundColor;
    private static readonly ConsoleColor UserColor = ConsoleColor.DarkCyan;
    private static readonly ConsoleColor AssistantColor = ConsoleColor.DarkGreen;
    private static readonly ConsoleColor ToolColor = ConsoleColor.Yellow;
    private static readonly ConsoleColor SystemColor = ConsoleColor.DarkMagenta;
    private static readonly ConsoleColor ErrorColor = ConsoleColor.Red;

    // Constants for chat UI layout - fixed width that will never change
    private const int CHAT_WIDTH = 70; // Total width including borders
    private const int CHAT_CONTENT_WIDTH = 66; // Content area width (CHAT_WIDTH - 4 for "│ " and " │")

    private int _messageCount = 0;

    private void ColorWrite(string text, ConsoleColor color)
    {
        var original = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = original;
    }

    private void ColorWriteLine(string text, ConsoleColor color)
    {
        ColorWrite(text, color);
        Console.WriteLine();
    }

    private void WritePaddedChatLine(string text, ConsoleColor textColor, ConsoleColor borderColor)
    {
        ColorWrite("│", borderColor);
        ColorWrite(" ", SystemColor);

        // If text is too long, truncate it
        if (text.Length > CHAT_CONTENT_WIDTH - 2) // -2 for the spaces before and after
        {
            // Truncate and add ellipsis
            string truncated = text.Substring(0, CHAT_CONTENT_WIDTH - 5) + "...";
            ColorWrite(truncated, textColor);
        }
        else
        {
            ColorWrite(text, textColor);

            // Calculate padding to align right border consistently
            int padding = CHAT_CONTENT_WIDTH - text.Length - 1; // -1 for the space we've already written
            ColorWrite(new string(' ', padding), DefaultColor);
        }

        ColorWriteLine("│", borderColor);
    }

    public void DrawChatInterface()
    {
        // Don't clear the console to preserve the banner
        // Just add some spacing to separate the banner from the chat
        Console.WriteLine("\n\n");

        string timestamp = DateTime.Now.ToString("yyyy.MM.dd");
        string sessionId = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();

        // Create horizontal lines with fixed width
        string horizontalLine = new string('─', CHAT_CONTENT_WIDTH);

        // Draw top border with fixed width
        ColorWrite("╭", SystemColor);
        ColorWrite(horizontalLine, SystemColor);
        ColorWriteLine("╮", SystemColor);

        // Header with fixed width and padding
        ColorWrite("│", SystemColor);
        ColorWrite(" ", DefaultColor);
        ColorWrite("MEGA AGENT INTERFACE", UserColor);
        ColorWrite(" • ", DefaultColor);
        ColorWrite($"SESSION: {sessionId}", AssistantColor);
        ColorWrite(" • ", DefaultColor);
        ColorWrite($"DATE: {timestamp}", ToolColor);

        // Calculate padding for fixed right border
        string header = $"MEGA AGENT INTERFACE • SESSION: {sessionId} • DATE: {timestamp}";
        int headerPadding = Math.Max(0, CHAT_CONTENT_WIDTH - header.Length - 1);
        ColorWrite(new string(' ', headerPadding), DefaultColor);

        ColorWriteLine("│", SystemColor);

        // Divider with fixed width
        ColorWrite("├", SystemColor);
        ColorWrite(horizontalLine, SystemColor);
        ColorWriteLine("┤", SystemColor);

        // Information lines with fixed width
        WritePaddedChatLine(
            "Welcome to the Mega Agent interface.",
            textColor: DefaultColor,
            SystemColor
        );
        WritePaddedChatLine(
            "Type your message below to communicate with the LLM.",
            DefaultColor,
            SystemColor
        );
        WritePaddedChatLine("Use Ctrl+C to exit the session.", DefaultColor, SystemColor);

        // Bottom border with fixed width
        ColorWrite("╰", SystemColor);
        ColorWrite(horizontalLine, SystemColor);
        ColorWriteLine("╯", SystemColor);

        Console.WriteLine();
    }

    public string GetUserInput()
    {
        // Always show USER header for all messages
        string userHeader = "USER";
        string horizontalLine = new string('─', 16); // Fixed width for user/assistant prompt

        // Draw header with USER text
        int paddingLeft = Math.Max(0, (horizontalLine.Length - userHeader.Length) / 2);
        int paddingRight = Math.Max(0, horizontalLine.Length - userHeader.Length - paddingLeft);

        ColorWrite("╭", UserColor);
        ColorWrite(new string('─', paddingLeft), UserColor);
        ColorWrite(userHeader, UserColor);
        ColorWrite(new string('─', paddingRight), UserColor);
        ColorWriteLine("", UserColor);

        ColorWrite("│", UserColor);
        ColorWrite(" ", UserColor);

        string input = Console.ReadLine() ?? string.Empty;

        Console.WriteLine();

        _messageCount++;
        return input;
    }

    public void DisplayAssistantMessage(string message)
    {
        string assistantHeader = "ASSISTANT";
        string horizontalLine = new string('─', 20);

        // Draw assistant header with fixed width
        ColorWrite("╭", AssistantColor);
        int paddingLeft = Math.Max(0, (horizontalLine.Length - assistantHeader.Length) / 2);
        int paddingRight = Math.Max(
            0,
            horizontalLine.Length - assistantHeader.Length - paddingLeft
        );
        ColorWrite(new string('─', paddingLeft), AssistantColor);
        ColorWrite(assistantHeader, AssistantColor);
        ColorWrite(new string('─', paddingRight), AssistantColor);
        ColorWriteLine("", AssistantColor);

        // Split by paragraphs to make it more readable
        var paragraphs = message.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(paragraph))
                continue;

            // For each line, ensure proper formatting
            var lines = paragraph.Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Get console width for proper line wrapping
                int consoleWidth = Math.Max(80, Console.WindowWidth); // Default to 80 if console width is unavailable
                int textWidth = consoleWidth - 2; // Account for left border and space

                // Use AssistantColor for the left border for the first line
                ColorWrite("│", AssistantColor);
                ColorWrite(" ", AssistantColor);

                if (line.Length <= textWidth)
                {
                    // Line fits within console width, display normally
                    ColorWrite(line, DefaultColor);
                    Console.WriteLine();
                }
                else
                {
                    // Line is too long, wrap it manually with proper left border
                    int position = 0;
                    bool isFirstLine = true;

                    while (position < line.Length)
                    {
                        // First line already has the border drawn
                        if (!isFirstLine)
                        {
                            ColorWrite("│", AssistantColor);
                            ColorWrite(" ", AssistantColor);
                        }

                        // Calculate how much text we can fit on this line
                        int remainingLength = line.Length - position;
                        int chunkSize = Math.Min(remainingLength, textWidth);

                        // Write the chunk of text
                        ColorWrite(line.Substring(position, chunkSize), DefaultColor);
                        Console.WriteLine();

                        // Move to next position
                        position += chunkSize;
                        isFirstLine = false;
                    }
                }
            }

            // Add blank line between paragraphs for readability
            if (paragraphs.Length > 1)
            {
                ColorWrite("│", AssistantColor);
                ColorWrite(" ", AssistantColor);
                Console.WriteLine();
            }
        }

        Console.WriteLine();
    }

    public void DisplayToolExecution(string toolName)
    {
        // Display a futuristic tool execution banner with fixed width
        string systemHeader = "SYSTEM";
        string horizontalLine = new string('─', 15);

        // Draw system header with fixed width
        ColorWrite("╭", SystemColor);
        int paddingLeft = Math.Max(0, (horizontalLine.Length - systemHeader.Length) / 2);
        int paddingRight = Math.Max(0, horizontalLine.Length - systemHeader.Length - paddingLeft);
        ColorWrite(new string('─', paddingLeft), SystemColor);
        ColorWrite(systemHeader, SystemColor);
        ColorWrite(new string('─', paddingRight), SystemColor);
        ColorWriteLine("", SystemColor);

        ColorWrite("│", SystemColor);
        ColorWrite(" ▶ ", SystemColor);
        ColorWrite("Executing tool: ", DefaultColor);
        ColorWrite(toolName, ToolColor);
        Console.WriteLine();

        Console.WriteLine();
    }

    /// <summary>
    /// Displays the results of a tool execution
    /// </summary>
    /// <param name="toolName">Name of the tool</param>
    /// <param name="results">Results returned from the tool</param>
    public void DisplayToolResults(string toolName, Dictionary<string, object> results)
    {
        if (results == null || results.Count == 0)
        {
            return;
        }

        // Display a results banner with fixed width
        string resultsHeader = "TOOL RESULTS";
        string horizontalLine = new string('─', 18);

        // Draw header with fixed width
        ColorWrite("╭", ToolColor);
        int paddingLeft = Math.Max(0, (horizontalLine.Length - resultsHeader.Length) / 2);
        int paddingRight = Math.Max(0, horizontalLine.Length - resultsHeader.Length - paddingLeft);
        ColorWrite(new string('─', paddingLeft), ToolColor);
        ColorWrite(resultsHeader, ToolColor);
        ColorWrite(new string('─', paddingRight), ToolColor);
        ColorWriteLine("", ToolColor);

        ColorWrite("│", ToolColor);
        ColorWrite(" ", ToolColor);
        ColorWrite("Tool: ", DefaultColor);
        ColorWrite(toolName, ToolColor);
        Console.WriteLine();

        // Limit to 5 results to prevent flooding the console
        int displayCount = 0;
        int maxResultsToDisplay = 5;
        int maxValueLength = 200; // Limit for individual result values

        foreach (var kvp in results)
        {
            if (displayCount >= maxResultsToDisplay)
            {
                ColorWrite("│", ToolColor);
                ColorWrite(" ", ToolColor);
                ColorWrite(
                    $"... and {results.Count - maxResultsToDisplay} more result(s)",
                    SystemColor
                );
                Console.WriteLine();
                break;
            }

            ColorWrite("│", ToolColor);
            ColorWrite(" • ", ToolColor);
            ColorWrite(kvp.Key + ": ", DefaultColor);

            // Convert the value to a string representation
            string valueStr;
            try
            {
                // For dictionary/object values, use JSON serialization for better display
                // Add explicit check for JsonElement which is what we're actually dealing with most often
                if (kvp.Value is System.Text.Json.JsonElement || 
                    kvp.Value is Dictionary<string, object> || 
                    kvp.Value?.GetType().IsClass == true)
                {
                    // Serialize the value first to get its string representation
                    valueStr = System.Text.Json.JsonSerializer.Serialize(
                        kvp.Value,
                        new System.Text.Json.JsonSerializerOptions
                        {
                            WriteIndented = false,
                            Encoder = System
                                .Text
                                .Encodings
                                .Web
                                .JavaScriptEncoder
                                .UnsafeRelaxedJsonEscaping,
                        }
                    );

                    // Special handling for content field, especially for JsonElement
                    if (kvp.Key == "content" && kvp.Value is System.Text.Json.JsonElement jsonElement)
                    {
                        try 
                        {
                            // Handle JsonElement content directly - this is the key case!
                            valueStr = new System.Text.StringBuilder("[").AppendLine().ToString();
                            
                            // Process each element in the array
                            foreach (var element in jsonElement.EnumerateArray())
                            {
                                // Check if it has a text property with JSON content
                                if (element.TryGetProperty("text", out var textProperty))
                                {
                                    string? textContent = textProperty.GetString();
                                    if (textContent != null)
                                    {
                                        // If it's a JSON string (like the ones we're seeing), beautify it
                                        if (textContent.StartsWith("{") && textContent.Contains("\\u"))
                                        {
                                            // Unescape the JSON string
                                            textContent = System.Text.RegularExpressions.Regex.Unescape(textContent);
                                            
                                            // Add type info and the beautified JSON
                                            var typeProperty = element.TryGetProperty("type", out var typeVal) ? 
                                                typeVal.GetString() : "unknown";
                                                
                                            valueStr += $"  Type: {typeProperty}\n  {textContent}\n";
                                        }
                                        else
                                        {
                                            // For non-JSON text, just show as is
                                            valueStr += $"  {textContent}\n";
                                        }
                                    }
                                }
                                else
                                {
                                    // For other elements, serialize normally
                                    valueStr += "  " + element.ToString() + "\n";
                                }
                            }
                            
                            valueStr += "]";
                        }
                        catch
                        {
                            // If direct JsonElement handling fails, try regex extraction
                            try
                            {
                                // Look for the pattern we know exists in serialized content field
                                var contentMatch = System.Text.RegularExpressions.Regex.Match(
                                    valueStr,
                                    @"""text"":""(.+?)"""
                                );

                                if (contentMatch.Success && contentMatch.Groups.Count > 1)
                                {
                                    // Extract and unescape the JSON text directly
                                    string jsonText = contentMatch.Groups[1].Value;
                                    jsonText = System.Text.RegularExpressions.Regex.Unescape(jsonText);
                                    valueStr = jsonText;
                                }
                            }
                            catch
                            {
                                // Last resort - direct string replacement
                                valueStr = valueStr
                                    .Replace("\\\"", "\"")
                                    .Replace("\\u0022", "\"") 
                                    .Replace("\\n", "\n")
                                    .Replace("\\r", "")
                                    .Replace("\\t", "\t")
                                    .Replace("\\\\", "\\");
                            }
                        }
                    }
                    // For string content (after serialization), handle with regex
                    else if (kvp.Key == "content")
                    {
                        try
                        {
                            // Look for the pattern we know exists in content field
                            var contentMatch = System.Text.RegularExpressions.Regex.Match(
                                valueStr,
                                @"""text"":""(.+?)"""
                            );

                            if (contentMatch.Success && contentMatch.Groups.Count > 1)
                            {
                                // Extract the actual JSON text value
                                string jsonText = contentMatch.Groups[1].Value;
                                
                                // Unescape it directly
                                jsonText = System.Text.RegularExpressions.Regex.Unescape(jsonText);
                                valueStr = jsonText;
                            }
                        }
                        catch
                        {
                            // If the direct extraction fails, try the string replacement approach as fallback
                            string cleaned = valueStr;

                            // These are verbatim strings to ensure we match literal backslashes
                            cleaned = cleaned.Replace(@"\""", "\"") // \" → "
                                      .Replace(@"\u0022", "\"")     // \u0022 → "
                                      .Replace(@"\n", "\n")         // \n → newline
                                      .Replace(@"\r", "")           // \r → nothing
                                      .Replace(@"\t", "\t")         // \t → tab
                                      .Replace(@"\\", @"\");        // \\ → \

                            valueStr = cleaned;
                        }
                    }

                    // If value is too long, truncate it
                    if (valueStr.Length > maxValueLength)
                    {
                        valueStr = valueStr.Substring(0, maxValueLength - 3) + "...";
                    }
                }
                else
                {
                    // Standard serialization for other objects
                    valueStr = System.Text.Json.JsonSerializer.Serialize(
                        kvp.Value,
                        new System.Text.Json.JsonSerializerOptions
                        {
                            WriteIndented = true,
                            Encoder = System
                                .Text
                                .Encodings
                                .Web
                                .JavaScriptEncoder
                                .UnsafeRelaxedJsonEscaping,
                        }
                    );
                }
            }
            catch
            {
                // Fallback if serialization fails
                valueStr = kvp.Value?.ToString() ?? "null";
            }

            // Limit the length
            if (valueStr.Length > maxValueLength)
            {
                valueStr = valueStr.Substring(0, maxValueLength - 3) + "...";
            }

            // Split the value by lines in case it contains newlines
            string[] lines = valueStr.Split('\n');

            // Get console width for proper line wrapping
            int consoleWidth = Math.Max(80, Console.WindowWidth); // Default to 80 if console width is unavailable
            int textWidth = consoleWidth - 6; // Account for left border, bullet point, key and spacing

            // Write the first line
            string firstLine = lines[0];
            int firstLineWidth = Math.Max(10, textWidth - (kvp.Key.Length + 4)); // Account for ": " and padding

            if (firstLine.Length <= firstLineWidth)
            {
                ColorWrite(firstLine, ToolColor);
                Console.WriteLine();
            }
            else
            {
                // First line is too long, show what we can
                ColorWrite(
                    firstLine.Substring(0, Math.Min(firstLineWidth, firstLine.Length)),
                    ToolColor
                );
                Console.WriteLine();

                // Continue the first line if needed
                if (firstLine.Length > firstLineWidth)
                {
                    int position = firstLineWidth;
                    while (position < firstLine.Length)
                    {
                        ColorWrite("│", ToolColor);
                        ColorWrite("   ", ToolColor); // Indent for continuation

                        int remainingLength = firstLine.Length - position;
                        int chunkSize = Math.Min(remainingLength, textWidth);

                        ColorWrite(firstLine.Substring(position, chunkSize), ToolColor);
                        Console.WriteLine();

                        position += chunkSize;
                    }
                }
            }

            // Write additional lines if present
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Start each line with the border and indentation
                ColorWrite("│", ToolColor);
                ColorWrite("   ", ToolColor); // Indent for continuation

                // Handle line wrapping for each additional line
                if (line.Length <= textWidth)
                {
                    ColorWrite(line, ToolColor);
                    Console.WriteLine();
                }
                else
                {
                    // Line is too long, wrap it
                    int position = 0;
                    while (position < line.Length)
                    {
                        if (position > 0)
                        {
                            ColorWrite("│", ToolColor);
                            ColorWrite("   ", ToolColor); // Indent for continuation
                        }

                        int remainingLength = line.Length - position;
                        int chunkSize = Math.Min(remainingLength, textWidth);

                        ColorWrite(line.Substring(position, chunkSize), ToolColor);
                        Console.WriteLine();

                        position += chunkSize;
                    }
                }
            }

            displayCount++;
        }

        Console.WriteLine();
    }

    public void DisplayToolError(string toolName, string errorMessage)
    {
        // Error banner with fixed width
        string errorHeader = "ERROR";
        string errorLine = new string('─', 15);

        ColorWrite("╭", ErrorColor);
        int errorPaddingLeft = Math.Max(0, (errorLine.Length - errorHeader.Length) / 2);
        int errorPaddingRight = Math.Max(
            0,
            errorLine.Length - errorHeader.Length - errorPaddingLeft
        );
        ColorWrite(new string('─', errorPaddingLeft), ErrorColor);
        ColorWrite(errorHeader, ErrorColor);
        ColorWrite(new string('─', errorPaddingRight), ErrorColor);
        ColorWriteLine("", ErrorColor);

        ColorWrite("│", ErrorColor);
        ColorWrite(" ", ErrorColor);
        ColorWrite($"Tool '{toolName}' not found", DefaultColor);
        Console.WriteLine();

        Console.WriteLine();
    }

    /// <summary>
    /// Displays an animated "thinking" indicator with color effects
    /// </summary>
    public async Task ShowThinkingAnimation(CancellationToken cancellationToken = default)
    {
        // Thinking animation characters and colors
        char[] spinnerChars = new[] { '⣾', '⣽', '⣻', '⢿', '⡿', '⣟', '⣯', '⣷' };
        ConsoleColor[] colors = new[]
        {
            ConsoleColor.Cyan,
            ConsoleColor.Blue,
            ConsoleColor.Magenta,
            ConsoleColor.DarkMagenta,
            ConsoleColor.DarkBlue,
            ConsoleColor.DarkCyan,
        };

        string label = " Thinking ";
        int position = 0;
        int colorIndex = 0;

        // Save current cursor position
        int cursorLeft = Console.CursorLeft;
        int cursorTop = Console.CursorTop;

        // CursorVisible is only supported on Windows
        bool cursorVisible = true;

        // Only change cursor visibility on Windows
        if (OperatingSystem.IsWindows())
        {
            cursorVisible = Console.CursorVisible;
            Console.CursorVisible = false;
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Clear the line
                Console.SetCursorPosition(0, cursorTop);
                Console.Write(new string(' ', Console.WindowWidth - 1));
                Console.SetCursorPosition(0, cursorTop);

                // Display spinner character with current color
                Console.ForegroundColor = colors[colorIndex];
                Console.Write(spinnerChars[position]);

                // Display label with pulsing effect
                for (int i = 0; i < label.Length; i++)
                {
                    // Calculate color based on position and pulse effect
                    int pulseOffset = Math.Abs((i - position) % label.Length);
                    int colorOffset = Math.Min(pulseOffset, colors.Length - 1);
                    Console.ForegroundColor = colors[(colorIndex + colorOffset) % colors.Length];
                    Console.Write(label[i]);
                }

                position = (position + 1) % spinnerChars.Length;
                colorIndex = (colorIndex + 1) % colors.Length;

                await Task.Delay(100, cancellationToken);
            }
        }
        catch (TaskCanceledException)
        {
            // Normal cancellation, nothing to do
        }
        finally
        {
            // Restore console state
            Console.ForegroundColor = DefaultColor;

            // Only restore cursor visibility on Windows
            if (OperatingSystem.IsWindows())
            {
                Console.CursorVisible = cursorVisible;
            }

            // Clear the animation line
            Console.SetCursorPosition(0, cursorTop);
            Console.Write(new string(' ', Console.WindowWidth - 1));
            Console.SetCursorPosition(0, cursorTop);
        }
    }
}