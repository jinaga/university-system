using University.Importer;

var REPLICATOR_URL = Environment.GetEnvironmentVariable("REPLICATOR_URL");
var ENVIRONMENT_PUBLIC_KEY = Environment.GetEnvironmentVariable("ENVIRONMENT_PUBLIC_KEY");
var IMPORT_DATA_PATH = Environment.GetEnvironmentVariable("IMPORT_DATA_PATH");
var PROCESSED_DATA_PATH = Environment.GetEnvironmentVariable("PROCESSED_DATA_PATH");
var ERROR_DATA_PATH = Environment.GetEnvironmentVariable("ERROR_DATA_PATH");

if (REPLICATOR_URL == null || ENVIRONMENT_PUBLIC_KEY == null || IMPORT_DATA_PATH == null || PROCESSED_DATA_PATH == null || ERROR_DATA_PATH == null)
{
    if (REPLICATOR_URL == null)
    {
        Console.WriteLine("Please set the environment variable REPLICATOR_URL.");
    }
    if (ENVIRONMENT_PUBLIC_KEY == null)
    {
        Console.WriteLine("Please set the environment variable ENVIRONMENT_PUBLIC_KEY.");
    }
    if (IMPORT_DATA_PATH == null)
    {
        Console.WriteLine("Please set the environment variable IMPORT_DATA_PATH.");
    }
    if (PROCESSED_DATA_PATH == null)
    {
        Console.WriteLine("Please set the environment variable PROCESSED_DATA_PATH.");
    }
    if (ERROR_DATA_PATH == null)
    {
        Console.WriteLine("Please set the environment variable ERROR_DATA_PATH.");
    }
    return;
}

var j = JinagaClientFactory.CreateClient(REPLICATOR_URL);

Console.WriteLine("Importing courses...");

var university = await UniversityDataSeeder.SeedData(j, ENVIRONMENT_PUBLIC_KEY);

var watcher = new CsvFileWatcher(j, university, IMPORT_DATA_PATH, PROCESSED_DATA_PATH, ERROR_DATA_PATH);
watcher.StartWatching();

Console.WriteLine("Press Ctrl+C to exit.");
var exitEvent = new TaskCompletionSource<bool>();

Console.CancelKeyPress += (sender, eventArgs) => {
    eventArgs.Cancel = true;
    exitEvent.SetResult(true);
};

AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => {
    exitEvent.SetResult(true);
};

await exitEvent.Task;

watcher.StopWatching();
await j.DisposeAsync();
