FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["University.Firehose/University.Firehose.csproj", "University.Firehose/"]
COPY ["University.Model/University.Model.csproj", "University.Model/"]
COPY ["University.Common/University.Common.csproj", "University.Common/"]
RUN dotnet restore "University.Firehose/University.Firehose.csproj"
COPY . .
WORKDIR "/src/University.Firehose"
RUN dotnet build "University.Firehose.csproj" -c Release -o /app/build
RUN dotnet publish "University.Firehose.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
COPY University.Firehose/start.sh .
RUN chmod +x start.sh
ENTRYPOINT ["tail", "-f", "/dev/null"]
