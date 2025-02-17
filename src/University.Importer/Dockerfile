FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["University.Importer/University.Importer.csproj", "University.Importer/"]
COPY ["University.Model/University.Model.csproj", "University.Model/"]
RUN dotnet restore "University.Importer/University.Importer.csproj"
COPY . .
WORKDIR "/src/University.Importer"
RUN dotnet build "University.Importer.csproj" -c Release -o /app/build
RUN dotnet publish "University.Importer.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENV IMPORT_DATA_PATH=/data/import
ENV PROCESSED_DATA_PATH=/data/processed
ENV ERROR_DATA_PATH=/data/error
ENTRYPOINT ["dotnet", "University.Importer.dll"]
