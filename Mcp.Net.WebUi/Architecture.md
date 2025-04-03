# Mcp.Net WebUI Architecture Document

This document describes the architecture of the Mcp.Net WebUI application, focusing on the interaction between the React frontend and .NET backend, as well as the flow of data and responsibilities of each component.

## High-Level Architecture

The application follows a client-server architecture with these main components:

```
┌─────────────────┐         ┌──────────────────────────────────────────┐
│                 │         │                                          │
│   React Client  │◀───────▶│             .NET Backend                 │
│  (TypeScript)   │  HTTP/  │  (WebAPI + SignalR + MCP Integration)    │
│                 │ SignalR │                                          │
└─────────────────┘         └──────────────────────────────────────────┘
                                               │
                                               ▼
                                    ┌─────────────────────┐
                                    │                     │
                                    │   LLM Provider      │
                                    │ (Anthropic/OpenAI)  │
                                    │                     │
                                    └─────────────────────┘
```

## Frontend Architecture

The React frontend is structured using a component-based architecture:

```
┌─────────────────────────────────────────────────────────────────┐
│                         React Application                        │
│                                                                  │
│  ┌──────────────┐  ┌───────────────┐  ┌────────────────────┐    │
│  │              │  │               │  │                    │    │
│  │ Chat Session │  │  Tool Selector│  │ Session Management │    │
│  │ Components   │  │  Components   │  │    Components      │    │
│  │              │  │               │  │                    │    │
│  └──────────────┘  └───────────────┘  └────────────────────┘    │
│          │                │                     │                │
│          └────────────────┼─────────────────────┘                │
│                           │                                      │
│  ┌──────────────────────────────────────────────────────┐       │
│  │                                                      │       │
│  │              API & SignalR Services                  │       │
│  │                                                      │       │
│  └──────────────────────────────────────────────────────┘       │
│                           │                                      │
└───────────────────────────┼──────────────────────────────────────┘
                            │
                            ▼
                 ┌─────────────────────────┐
                 │                         │
                 │      .NET Backend       │
                 │                         │
                 └─────────────────────────┘
```

### Key Frontend Components

1. **Chat Session Components**
   - `ChatContainer`: Main container for chat interface
   - `ChatInput`: Handles user text input
   - `ChatMessage`: Renders individual messages
   - `ThinkingIndicator`: Shows when the AI is "thinking"

2. **Tool Components**
   - `ToolSelector`: Interface for selecting and executing tools
   - `ToolExecutionDisplay`: Shows tool execution status and results

3. **Session Management Components**
   - `SessionSidebar`: Lists available chat sessions
   - `SessionConfigModal`: Configure session parameters
   - `ModelSelector`: Choose LLM provider and model

4. **API Services**
   - `api.ts`: REST API communication for session management
   - `signalr.ts`: Real-time communication for chat messages

## Backend Architecture

The .NET backend is structured using a layered architecture with clean separation of concerns:

```
┌───────────────────────────────────────────────────────────────────────────┐
│                          .NET WebUI Backend                               │
│                                                                           │
│  ┌───────────────┐    ┌────────────────┐    ┌────────────────────────┐   │
│  │               │    │                │    │                        │   │
│  │  Controllers  │    │  SignalR Hubs  │    │  DTOs & Models         │   │
│  │  (REST API)   │    │  (Real-time)   │    │  (Data Transfer)       │   │
│  │               │    │                │    │                        │   │
│  └───────┬───────┘    └────────┬───────┘    └────────────────────────┘   │
│          │                     │                                          │
│          └─────────────────────┼──────────────────────────────────┐      │
│                                │                                  │      │
│  ┌──────────────────────────────────────────────────────────────┐ │      │
│  │                         Domain Layer                         │ │      │
│  │                                                              │ │      │
│  │  ┌──────────────┐  ┌──────────────┐  ┌───────────────────┐  │ │      │
│  │  │              │  │              │  │                   │  │ │      │
│  │  │ Adapters     │  │ Chat         │  │ Infrastructure    │  │ │      │
│  │  │              │  │              │  │                   │  │ │      │
│  │  └──────────────┘  └──────────────┘  └───────────────────┘  │ │      │
│  │                                                              │ │      │
│  └───────────────────────────────┬──────────────────────────────┘ │      │
│                                  │                                │      │
│                                  │                                │      │
│  ┌──────────────────────────────────────────────────────────────┐ │      │
│  │                   Integration Layer                          │ │      │
│  │                                                              │ │      │
│  │  ┌──────────────────┐     ┌───────────────────────────┐     │ │      │
│  │  │                  │     │                           │     │ │      │
│  │  │  LLM             │     │  MCP Client               │     │ │      │
│  │  │  Integration     │     │  Integration              │     │ │      │
│  │  │                  │     │                           │     │ │      │
│  │  └──────────────────┘     └───────────────────────────┘     │ │      │
│  │                                                              │ │      │
│  └───────────────────────────────┬──────────────────────────────┘ │      │
│                                  │                                │      │
└──────────────────────────────────┼────────────────────────────────┘      │
                                   │                                       │
                                   ▼                                       │
                     ┌───────────────────────────────┐                     │
                     │                               │                     │
                     │  External Services (LLM, MCP) │                     │
                     │                               │                     │
                     └───────────────────────────────┘                     │
```

### Key Backend Components

#### 1. API Layer

1. **Controllers**
   - `ChatController`: REST API endpoints for chat management
   - `ToolsController`: REST API endpoints for tool operations

2. **SignalR Hubs**
   - `ChatHub`: Real-time communication for chat sessions

3. **DTOs**
   - `ChatMessageDto`: Data transfer object for chat messages
   - `ToolExecutionDto`: Data transfer object for tool execution
   - `SessionMetadataDto`: Data transfer object for session settings

#### 2. Domain Layer

1. **Adapters**
   - `ISignalRChatAdapter`: Interface for adapting chat sessions to SignalR
   - `SignalRChatAdapter`: Implementation that bridges between chat sessions and SignalR communication
   
   _Real-world analogue_: This is like a translator between the AI system and real-time web communication - it takes what the AI says and ensures it gets to the right web clients in real-time.

2. **Chat**
   - `IChatRepository`: Interface for chat storage operations
   - `ChatRepository`: Implementation that manages storage of chat sessions and messages
   - `IChatFactory`: Interface for creating chat components
   - `ChatFactory`: Implementation that creates configured chat components
   
   _Real-world analogue_: The ChatRepository is like a filing cabinet for conversations - it's where complete message history is stored and retrieved. The ChatFactory is like a factory that assembles all the components needed for a chat system to work.

3. **Infrastructure**
   - `SessionNotifier`: Service for sending notifications about session updates
   - `InMemoryChatHistoryManager`: Stores and retrieves chat history 
   
   _Real-world analogue_: The SessionNotifier is like a broadcast system that tells everyone when something changes. The InMemoryChatHistoryManager is a temporary storage system that would be replaced with a database in a production environment.

4. **Input**
   - `WebUserInputProvider`: Handles user input from the web
   
   _Real-world analogue_: This is like the person who listens for what the user says and makes sure it gets to the right place.

#### 3. Integration Layer

1. **LLM**
   - `LlmClientFactory`: Creates clients for different LLM providers
   - `StubChatClient`: Test implementation for LLM clients
   
   _Real-world analogue_: This is like the telephone switchboard that connects to different AI services like Claude or GPT.

2. **MCP Integration**
   - `ToolRegistry`: Registry of available tools
   - Integration with Mcp.Net client for tool discovery and execution
   
   _Real-world analogue_: This is like a toolbox where all the capabilities of the AI (like web search, calculations, etc.) are organized and made available.

## Communication Flows

### 1. User Sends a New Message

```
┌──────────┐        ┌─────────┐        ┌───────────┐        ┌────────────┐        ┌─────────┐        ┌──────────┐
│          │        │         │        │           │        │            │        │         │        │          │
│  React   │  HTTP/ │ Chat    │        │ SignalR   │        │ Chat       │        │ LLM     │        │ LLM      │
│  UI      │─SignalR→ Hub     │────────→ Adapter   │────────→ Session    │────────→ Client  │────────→ Provider  │
│          │        │         │        │           │        │            │        │         │        │          │
└──────────┘        └─────────┘        └───────────┘        └────────────┘        └─────────┘        └──────────┘
     │                   │                  │                     │                    │                   │
     │  Type message     │                  │                     │                    │                   │
     │   & press         │                  │                     │                    │                   │
     │   enter           │                  │                     │                    │                   │
     │                   │                  │                     │                    │                   │
     │ SendMessage()     │                  │                     │                    │                   │
     ├──────────────────>│                  │                     │                    │                   │
     │                   │ Get adapter for  │                     │                    │                   │
     │                   │  this session    │                     │                    │                   │
     │                   │─────────────────>│                     │                    │                   │
     │                   │                  │ Store user message  │                    │                   │
     │                   │                  │ & process input     │                    │                   │
     │                   │                  │─────────────────────>                    │                   │
     │                   │                  │                     │ Send message to LLM│                   │
     │                   │                  │                     │────────────────────>                   │
     │                   │                  │                     │                    │ API call to       │
     │                   │                  │                     │                    │ Claude/GPT/etc.   │
     │                   │                  │                     │                    │──────────────────>│
     │                   │                  │                     │                    │                   │
     │                   │                  │                     │                    │ LLM response      │
     │                   │                  │                     │                    │<──────────────────│
     │                   │                  │                     │ Return LLM response│                   │
     │                   │                  │                     │<────────────────────                   │
     │                   │                  │ Event: Message      │                    │                   │
     │                   │                  │ received            │                    │                   │
     │                   │<─────────────────│                     │                    │                   │
     │ ReceiveMessage()  │                  │                     │                    │                   │
     │<──────────────────│                  │                     │                    │                   │
     │                   │                  │                     │                    │                   │
     │ Display message   │                  │                     │                    │                   │
     │ in chat UI        │                  │                     │                    │                   │
     │                   │                  │                     │                    │                   │
```

**Description**:
1. User types a message in the React UI and presses Enter
2. React UI sends the message to the backend via SignalR's `SendMessage` method
3. `ChatHub` looks up or creates a `SignalRChatAdapter` for the session
4. The adapter stores the message in the repository and provides it to the chat session
5. `ChatSession` sends the message to the LLM client
6. LLM client makes an API call to the LLM provider (Claude/GPT)
7. The LLM provider returns a response
8. The response flows back through the chat session
9. `SignalRChatAdapter` raises an event when it receives the message
10. `ChatHub` receives the event and sends the message to the connected clients
11. React UI receives the message through SignalR and displays it in the chat interface

### 2. Tool Execution Flow

```
┌──────────┐        ┌─────────┐        ┌───────────┐        ┌────────────┐        ┌─────────┐        ┌──────────┐        ┌──────────┐
│          │        │         │        │           │        │            │        │         │        │          │        │          │
│  React   │        │ Chat    │        │ SignalR   │        │ Chat       │        │ LLM     │        │ Tool     │        │  MCP     │
│  UI      │────────→ Hub     │────────→ Adapter   │────────→ Session    │────────→ Client  │────────→ Registry │────────→  Client   │
│          │        │         │        │           │        │            │        │         │        │          │        │          │
└──────────┘        └─────────┘        └───────────┘        └────────────┘        └─────────┘        └──────────┘        └──────────┘
     │                   │                  │                     │                    │                   │                   │
     │                   │                  │                     │                    │ LLM response      │                   │
     │                   │                  │                     │                    │ with tool call    │                   │
     │                   │                  │                     │<────────────────────                   │                   │
     │                   │                  │                     │                    │                   │                   │
     │                   │                  │                     │ Process tool call  │                   │                   │
     │                   │                  │                     │─────────────────────────────────────────                   │
     │                   │                  │                     │                    │                   │ Find tool &       │
     │                   │                  │                     │                    │                   │ get parameters    │
     │                   │                  │                     │                    │                   │──────────────────>│
     │                   │                  │                     │                    │                   │                   │
     │                   │                  │                     │                    │                   │ Execute tool      │
     │                   │                  │                     │                    │                   │ via MCP           │
     │                   │                  │                     │                    │                   │<──────────────────│
     │                   │                  │                     │                    │                   │                   │
     │                   │                  │ Tool execution      │ Tool result        │                   │                   │
     │                   │                  │ event               │─────────────────────────────────────────                   │
     │                   │                  │<─────────────────────                    │                   │                   │
     │ ToolExecution     │                  │                     │                    │                   │                   │
     │ Update            │                  │                     │                    │                   │                   │
     │<──────────────────│                  │                     │                    │                   │                   │
     │                   │                  │                     │                    │                   │                   │
     │ Display tool      │                  │                     │                    │                   │                   │
     │ execution state   │                  │                     │                    │                   │                   │
     │                   │                  │                     │ Send tool results  │                   │                   │
     │                   │                  │                     │ back to LLM        │                   │                   │
     │                   │                  │                     │────────────────────>                   │                   │
     │                   │                  │                     │                    │                   │                   │
     │                   │                  │                     │                    │ Final response    │                   │
     │                   │                  │                     │<────────────────────                   │                   │
     │                   │                  │ Message received    │                    │                   │                   │
     │                   │                  │ event               │                    │                   │                   │
     │                   │<─────────────────│                     │                    │                   │                   │
     │ ReceiveMessage()  │                  │                     │                    │                   │                   │
     │<──────────────────│                  │                     │                    │                   │                   │
     │                   │                  │                     │                    │                   │                   │
     │ Display final     │                  │                     │                    │                   │                   │
     │ message with      │                  │                     │                    │                   │                   │
     │ tool results      │                  │                     │                    │                   │                   │
```

**Description**:
1. LLM responds with a need to use a tool
2. `ChatSession` processes the tool call request
3. `ToolRegistry` finds the tool and gets its parameters
4. Tool is executed via MCP client
5. The tool execution status is sent to the React UI through SignalR
6. UI displays the tool execution state
7. Tool results are sent back to the LLM
8. LLM provides a final response incorporating the tool results
9. The final response flows back to the UI and is displayed

### 3. Creating a New Chat Session

```
┌──────────┐        ┌─────────────┐        ┌──────────────┐        ┌────────────────┐
│          │        │             │        │              │        │                │
│  React   │   HTTP │  Chat       │        │  Chat        │        │ Chat           │
│  UI      │────────→  Controller │────────→  Factory     │────────→ Repository     │
│          │        │             │        │              │        │                │
└──────────┘        └─────────────┘        └──────────────┘        └────────────────┘
     │                    │                      │                        │
     │ Create new chat    │                      │                        │
     │ (HTTP POST)        │                      │                        │
     │───────────────────>│                      │                        │
     │                    │                      │                        │
     │                    │ Create session       │                        │
     │                    │ metadata             │                        │
     │                    │──────────────────────>                        │
     │                    │                      │                        │
     │                    │                      │ Store session          │
     │                    │                      │ metadata               │
     │                    │                      │────────────────────────>
     │                    │                      │                        │
     │                    │                      │ Session ID             │
     │                    │                      │<────────────────────────
     │                    │                      │                        │
     │                    │ Session ID           │                        │
     │                    │<──────────────────────                        │
     │                    │                      │                        │
     │ Session ID         │                      │                        │
     │<───────────────────│                      │                        │
     │                    │                      │                        │
     │ Navigate to        │                      │                        │
     │ new chat           │                      │                        │
     │                    │                      │                        │
```

**Description**:
1. User clicks "New Chat" in the React UI
2. React UI sends a POST request to the ChatController
3. ChatController uses ChatFactory to create session metadata
4. ChatFactory creates the metadata with default settings or user-specified settings
5. ChatRepository stores the session metadata 
   - _In a production environment, this would be in a database_
6. Session ID is returned through the chain back to the UI
7. UI navigates to the new chat session

## Real-World Component Mappings

| Technical Component | Real-World Analogue | Description |
|---------------------|---------------------|-------------|
| `ChatRepository` | Filing Cabinet | Stores and retrieves conversations (would be a database in production) |
| `ChatFactory` | Assembly Line | Creates and configures all the parts needed for a conversation |
| `SignalRChatAdapter` | Translator/Messenger | Translates between AI communications and web communications |
| `ChatHub` | Switchboard Operator | Routes messages between users and the system |
| `SessionNotifier` | Announcer | Broadcasts when something changes to everyone who needs to know |
| `WebUserInputProvider` | Listener | Listens for what users say and makes sure it gets to the right place |
| `LlmClientFactory` | Phone Switchboard | Connects to different AI services like Claude or GPT |
| `ToolRegistry` | Toolbox | Organizes all the tools the AI can use |
| `InMemoryChatHistoryManager` | Temporary Filing System | Stores conversation history (would be replaced with permanent storage in production) |

## Architectural Decisions and Design Patterns

1. **Repository Pattern**
   - `IChatRepository` and its implementation separate data access from business logic
   - Would be extended to use a real database in production

2. **Factory Pattern**
   - `IChatFactory` and `LlmClientFactory` centralize object creation
   - Makes it easy to switch configurations or implementations

3. **Adapter Pattern**
   - `SignalRChatAdapter` adapts the chat session to work with SignalR
   - Enables clean separation between core chat logic and specific communication technology

4. **Dependency Injection**
   - Used throughout the application for loose coupling
   - Makes testing and swapping implementations easier

5. **Event-Based Communication**
   - Components communicate through events for loose coupling
   - Prevents circular dependencies between components

## Future Enhancements

1. **Persistent Storage**
   - Replace `InMemoryChatHistoryManager` with a database implementation
   - Add data migration capabilities

2. **Authentication and Multi-User Support**
   - Add user authentication and authorization
   - Secure chat sessions to their owners

3. **Enhanced Tool Integration**
   - More sophisticated tool discovery and management
   - User-customizable tool settings

4. **Performance Optimizations**
   - Caching for frequently accessed data
   - Background processing for long-running operations

## Conclusion

The Mcp.Net WebUI architecture follows modern principles of separation of concerns, dependency injection, and clean interfaces. The React frontend communicates with the .NET backend through a combination of REST API (for session management) and SignalR (for real-time chat). The backend is organized into logical layers with clear responsibilities, making it maintainable and extensible.