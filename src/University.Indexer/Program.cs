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

var settings = new ConnectionSettings(new Uri(ELASTICSEARCH_URL))
    .DefaultIndex("offerings");
var client = new ElasticClient(settings);

// Ensure index exists with proper mappings
var existsResponse = await client.IndexExistsAsync("offerings");
if (!existsResponse.Exists)
{
    await client.CreateIndexAsync("offerings", c => c
        .Mappings(m => m
            .Map<SearchRecord>(mm => mm
                .Properties(p => p
                    .Keyword(k => k.Name(n => n.Id))
                    .Keyword(k => k.Name(n => n.CourseCode))
                    .Text(t => t.Name(n => n.CourseName))
                    .Keyword(k => k.Name(n => n.Days))
                    .Keyword(k => k.Name(n => n.Time))
                    .Keyword(k => k.Name(n => n.Instructor))
                    .Keyword(k => k.Name(n => n.Location))
                )
            )
        )
    );
}

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
    
    var response = await client.IndexDocumentAsync(searchRecord);
    if (response.IsValid)
    {
        await j.Fact(new SearchIndexRecord(offering, recordId));
        Console.WriteLine($"Indexed {offering.course.code} {offering.course.name}");
    }
    else
    {
        Console.WriteLine($"Failed to index {offering.course.code}: {response.DebugInformation}");
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
