FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["University.Indexer/University.Indexer.csproj", "University.Indexer/"]
COPY ["University.Model/University.Model.csproj", "University.Model/"]
COPY ["University.Common/University.Common.csproj", "University.Common/"]
RUN dotnet restore "University.Indexer/University.Indexer.csproj"
COPY . .
WORKDIR "/src/University.Indexer"
RUN dotnet build "University.Indexer.csproj" -c Release -o /app/build
RUN dotnet publish "University.Indexer.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "University.Indexer.dll"]
