using Jinaga;
using Jinaga.Extensions;
using University.Indexer;
using University.Model;
using Nest;

var REPLICATOR_URL = Environment.GetEnvironmentVariable("REPLICATOR_URL");
var ENVIRONMENT_PUBLIC_KEY = Environment.GetEnvironmentVariable("ENVIRONMENT_PUBLIC_KEY");
var ELASTICSEARCH_URL = Environment.GetEnvironmentVariable("ELASTICSEARCH_URL");

if (REPLICATOR_URL == null || ENVIRONMENT_PUBLIC_KEY == null || ELASTICSEARCH_URL == null)
{
    if (REPLICATOR_URL == null)
    {
        Console.WriteLine("Please set the environment variable REPLICATOR_URL.");
    }
    if (ENVIRONMENT_PUBLIC_KEY == null)
    {
        Console.WriteLine("Please set the environment variable ENVIRONMENT_PUBLIC_KEY.");
    }
    if (ELASTICSEARCH_URL == null)
    {
        Console.WriteLine("Please set the environment variable ELASTICSEARCH_URL.");
    }
    return;
}

Console.WriteLine("Indexing course offerings...");

var elasticsearchClient = new ElasticsearchClientProxy(ELASTICSEARCH_URL);

await elasticsearchClient.Initialize();

var j = JinagaClient.Create(options =>
{
    options.HttpEndpoint = new Uri(REPLICATOR_URL);
});

var creator = await j.Fact(new User(ENVIRONMENT_PUBLIC_KEY));
var university = await j.Fact(new Organization(creator, "6003"));
var currentSemester = await j.Fact(new Semester(university, 2022, "Spring"));

var offeringsToIndex = Given<Semester>.Match(semester =>
    from offering in semester.Successors().OfType<Offering>(offering => offering.semester)
    where offering.Successors().No<OfferingDelete>(deleted => deleted.offering)
    where offering.Successors().No<SearchIndexRecord>(record => record.offering)
    select offering);
var indexInsertSubscription = j.Subscribe(offeringsToIndex, currentSemester, async offering =>
{
    // Create and index a record for the offering
    var recordId = j.Hash(offering);
    var searchRecord = new SearchRecord
    {
        Id = recordId,
        CourseCode = offering.course.code,
        CourseName = offering.course.name,
        Days = "TBA",
        Time = "TBA",
        Instructor = "TBA",
        Location = "TBA"
    };
    bool indexed = await elasticsearchClient.IndexRecord(searchRecord);

    if (indexed)
    {
        await j.Fact(new SearchIndexRecord(offering, recordId));
        Console.WriteLine($"Indexed {offering.course.code} {offering.course.name}");
    }
});

// Keep the application running
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (TaskCanceledException)
{
    // Task was canceled, exit gracefully
}

indexInsertSubscription.Stop();
Console.WriteLine("Stopped indexing course offerings.");
