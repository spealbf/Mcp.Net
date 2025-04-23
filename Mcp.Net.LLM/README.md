# Mcp.Net.LLM

Core library that provides abstractions and implementations for integrating Large Language Models into Mcp.Net applications.

## Features

- Support for OpenAI (GPT-4o) and Anthropic (Claude) models
- Common interface for different LLM providers
- Tool integration with Mcp.Net framework
- Event-based architecture for UI integration
- Error handling and logging

## Usage

This library is used as a dependency by:

1. `Mcp.Net.Examples.LLMConsole` - A console-based demo app
2. `Mcp.Net.WebUi` - The web-based UI project

## Components

### Core Components

- **ChatSession**: Central class that manages interaction with LLM and tool execution
- **LLM Clients**: Implementations for different providers (OpenAI, Anthropic)
- **Tool Registry**: Manages available tools and their registration with LLMs
- **Events & Interfaces**: Clear separation between business logic and UI

### LLM Providers

The library supports the following LLM providers:

- **Anthropic**: Claude models with tool use capability
  - Default: claude-3-7-sonnet-latest
  
- **OpenAI**: GPT models with function calling capability
  - Default: gpt-4o

## Architecture

The library follows clean architecture principles:

- **Interfaces**: Clear separation of concerns through well-defined interfaces
- **Event-based Communication**: UI components subscribe to events from ChatSession
- **Provider Abstractions**: Unified API for different LLM providers
- **Tool Integration**: Standard approach to tool registration and execution

## Example Implementations

For examples of using this library, see:

1. `Mcp.Net.Examples.LLMConsole` for console-based implementation
2. `Mcp.Net.WebUi` for web-based implementation