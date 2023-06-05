FROM mcr.microsoft.com/dotnet/runtime:3.1 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /src
COPY ["rate_news.csproj", "."]
RUN dotnet restore "./rate_news.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "rate_news.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "rate_news.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "rate_news.dll"]