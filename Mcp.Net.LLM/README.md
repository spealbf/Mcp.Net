# Mcp.Net.LLM

A demo application showing how to integrate Mcp.Net with OpenAI and Anthropic LLMs for tool calling.

## Features

- Interactive console-based chat UI
- Support for OpenAI (GPT-4o) and Anthropic (Claude) models
- Dynamic tool discovery and registration
- Tool selection interface
- Real-time tool execution visualization
- Proper event-based UI architecture

## Quick Start

### Prerequisites

1. You need an API key for either:
   - OpenAI: [Get one here](https://platform.openai.com/api-keys)
   - Anthropic: [Get one here](https://console.anthropic.com/)

2. Set the API key as an environment variable:
   ```bash
   # For Anthropic (default)
   export ANTHROPIC_API_KEY="your-api-key"
   
   # Or for OpenAI
   export OPENAI_API_KEY="your-api-key"
   ```

### Optional API Keys for External Tools

The LLM project also includes external tools that require additional API keys to function:

#### Google Search API

To use the `googleSearch/search` tool, you'll need:
- Google API Key 
- Custom Search Engine ID

Set these credentials as environment variables:
```bash
export GOOGLE_API_KEY="your-google-api-key"
export GOOGLE_SEARCH_ENGINE_ID="your-search-engine-id"
```

Without these environment variables, the tool will return an informative error message with instructions.

#### Twilio SMS API

To use the `twilioSms/sendSmsToUkNumber` tool, you'll need:
- Twilio Account SID
- Twilio Auth Token
- Twilio Phone Number

Set these credentials as environment variables:
```bash
export TWILIO_ACCOUNT_SID="your-account-sid"
export TWILIO_AUTH_TOKEN="your-auth-token"
export TWILIO_PHONE_NUMBER="your-twilio-phone-number"
```

Without these credentials, the tool will return an informative error message.

### Running the MCP Server

First, run the SimpleServer in a terminal window:

```bash
dotnet run --project Mcp.Net.Examples.SimpleServer/Mcp.Net.Examples.SimpleServer.csproj
```

This will start a server on `http://localhost:5000/` with several demo tools ready to use.

### Running the LLM Client

In a new terminal window, run the LLM chat application:

```bash
dotnet run --project Mcp.Net.LLM/Mcp.Net.LLM.csproj
```

The client will:
1. Connect to the SimpleServer at `http://localhost:5000/`
2. Present a tool selection interface
3. Start a chat session with your chosen LLM

### Command Line Options

```bash
# Use OpenAI instead of Claude (default)
dotnet run --project Mcp.Net.LLM/Mcp.Net.LLM.csproj --provider=openai

# Specify a different model
dotnet run --project Mcp.Net.LLM/Mcp.Net.LLM.csproj --model=gpt-4

# Skip the tool selection screen and enable all tools
dotnet run --project Mcp.Net.LLM/Mcp.Net.LLM.csproj --all-tools

# Enable debug logging
dotnet run --project Mcp.Net.LLM/Mcp.Net.LLM.csproj --debug
```

## Using the Chat Interface

1. After starting the app, select which tools you want to enable
2. Press any key to start the chat session
3. Type your message and press Enter
4. The LLM will respond and may use tools as needed
5. You'll see real-time tool execution notifications
6. Continue the conversation naturally

## Example Prompts

Try these examples to see tool calling in action:

### Basic Tools (No API Keys Required)
- "What's 255 × 597?"
- "Generate a name for a Warhammer 40k Inquisitor"
- "Simulate a battle between Space Marines and Orks"
- "What's the factorial of 12?"
- "Reverse the string 'hello world'"
- "Encode 'hello world' to base64"

### External Tools (Require API Keys)
- "Search the web for information about the MCP protocol" (requires Google API key)
- "Send a message to my phone saying 'Hello from MCP'" (requires Twilio credentials)

Even without the API keys, the LLM will attempt to use these tools and receive appropriate error messages that it can handle.

## Architecture

The application follows clean architecture principles:
- Event-based UI design separates the chat session logic from UI concerns
- Dependency injection for improved testability and flexibility
- Properly abstracted interfaces for chat clients (OpenAI, Anthropic)
- Tool registration through the MCP protocol
- Asynchronous messaging with proper cancellation support

This design makes it easy to replace the console UI with other interfaces (web, desktop) while keeping the core chat session logic intact.

## Supported LLM Providers

- **Anthropic**: Claude models with tool use capability
  - Default: claude-3-5-sonnet-20240620
  
- **OpenAI**: GPT models with function calling capability
  - Default: gpt-4o