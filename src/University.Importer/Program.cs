using University.Importer;

var j = JinagaClientFactory.CreateClient();
if (j == null) return;

Console.WriteLine("Importing courses...");

var university = await UniversityDataSeeder.SeedData(j);

var watcher = new CsvFileWatcher(j, university);
watcher.StartWatching("path/to/watch");