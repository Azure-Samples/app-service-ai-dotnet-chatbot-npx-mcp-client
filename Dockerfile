# Use the official ASP.NET Core runtime as the base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy project files for building
COPY ["MCPHostApp/MCPHostApp.csproj", "MCPHostApp/"]
RUN dotnet restore "MCPHostApp/MCPHostApp.csproj"

# Copy everything else for building
COPY . .

# Build the application
RUN dotnet build "MCPHostApp/MCPHostApp.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "MCPHostApp/MCPHostApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage/image
FROM base AS final

# Install Node.js and npm in the final image as well
RUN apt-get update && apt-get install -y \
    curl \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y nodejs \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=publish /app/publish .

# Create the /workspace/test-files directory structure to match dev container
RUN mkdir -p /workspace/test-files
COPY test-files/ /workspace/test-files/

# Start app
CMD ["dotnet", "MCPHostApp.dll"]
