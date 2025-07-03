# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["LLMSessionGateway.API/LLMSessionGateway.API.csproj", "LLMSessionGateway.API/"]
COPY ["LLMSessionGateway.Application/LLMSessionGateway.Application.csproj", "LLMSessionGateway.Application/"]
COPY ["LLMSessionGateway.Core/LLMSessionGateway.Core.csproj", "LLMSessionGateway.Core/"]
COPY ["LLMSessionGateway.Infrastructure/LLMSessionGateway.Infrastructure.csproj", "LLMSessionGateway.Infrastructure/"]

RUN dotnet restore "LLMSessionGateway.API/LLMSessionGateway.API.csproj"

COPY . .
WORKDIR "/src/LLMSessionGateway.API"
RUN dotnet publish "LLMSessionGateway.API.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "LLMSessionGateway.API.dll"]