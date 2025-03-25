FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Only copy project files needed for SimpleServer
COPY ["Mcp.Net.Core/Mcp.Net.Core.csproj", "Mcp.Net.Core/"]
COPY ["Mcp.Net.Server/Mcp.Net.Server.csproj", "Mcp.Net.Server/"]
COPY ["Mcp.Net.Examples.SimpleServer/Mcp.Net.Examples.SimpleServer.csproj", "Mcp.Net.Examples.SimpleServer/"]
COPY ["Mcp.Net.Examples.ExternalTools/Mcp.Net.Examples.ExternalTools.csproj", "Mcp.Net.Examples.ExternalTools/"]

# Restore only the SimpleServer project and its dependencies
RUN dotnet restore "Mcp.Net.Examples.SimpleServer/Mcp.Net.Examples.SimpleServer.csproj"

# Copy the source code
COPY . .

# Build the SimpleServer project
RUN dotnet build "Mcp.Net.Examples.SimpleServer/Mcp.Net.Examples.SimpleServer.csproj" -c Release -o /app/build

# Publish the project
FROM build AS publish
RUN dotnet publish "Mcp.Net.Examples.SimpleServer/Mcp.Net.Examples.SimpleServer.csproj" -c Release -o /app/publish

# Build the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Expose dynamic port mapping (cloud platforms typically set the PORT environment variable)
EXPOSE 8080

# Set the entry point
ENTRYPOINT ["dotnet", "Mcp.Net.Examples.SimpleServer.dll"]