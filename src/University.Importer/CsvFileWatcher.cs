using System.Diagnostics;
using System.Globalization;

using CsvHelper;

using Jinaga;
using Jinaga.Extensions;

using University.Model;

namespace University.Importer
{
    public class CsvFileWatcher
    {
        private readonly JinagaClient _j;
        private readonly Organization _university;
        private readonly string _importDataPath;
        private readonly string _processedDataPath;
        private readonly string _errorDataPath;
        private readonly ActivitySource _activitySource = new ActivitySource("University.Importer");

        private FileSystemWatcher? _watcher = null;

        public CsvFileWatcher(JinagaClient j, Organization university, string importDataPath, string processedDataPath, string errorDataPath)
        {
            _j = j;
            _university = university;
            _importDataPath = importDataPath;
            _processedDataPath = processedDataPath;
            _errorDataPath = errorDataPath;
        }

        public void StartWatching()
        {
            if (_watcher != null)
            {
                return;
            }
            _watcher = new FileSystemWatcher
            {
                Path = _importDataPath,
                Filter = "*.csv",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };

            _watcher.Created += OnNewFile;
            _watcher.EnableRaisingEvents = true;
        }

        public void StopWatching()
        {
            if (_watcher == null)
            {
                return;
            }
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }

        private void OnNewFile(object source, FileSystemEventArgs e)
        {
            Task.Run(async () => await ImportCsvFile(e.FullPath));
        }

        private async Task ImportCsvFile(string filePath)
        {
            try
            {
                using var reader = new StreamReader(filePath);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                var records = csv.GetRecordsAsync<CourseRecord>();

                await foreach (var record in records)
                {
                    await CreateFacts(record);
                }

                MoveFileToProcessed(filePath);
            }
            catch (Exception ex)
            {
                LogError(ex, filePath);
                MoveFileToError(filePath);
            }
        }

        private async Task CreateFacts(CourseRecord record)
        {
            using var activity = _activitySource.StartActivity("CreateFacts");
            activity?.SetTag("courseCode", record.CourseCode);
            activity?.SetTag("courseName", record.CourseName);

            var locationsOfOffering = Given<Offering>.Match(offering => offering.Locations.Select(location => location));
            var timesOfOffering = Given<Offering>.Match(offering => offering.Times.Select(time => time));
            var instructorsOfOffering = Given<Offering>.Match(offering => offering.Successors().OfType<OfferingInstructor>(instructor => instructor.offering)
                .WhereNo((OfferingInstructor next) => next.prior));

            var course = await _j.Fact(new Course(_university, record.CourseCode, record.CourseName));
            var semester = await _j.Fact(new Semester(_university, record.Year, record.Term));
            var instructor = await _j.Fact(new Instructor(_university, record.Instructor));
            var offering = await _j.Fact(new Offering(course, semester, record.OfferingGuid));

            var locations = await _j.Query(locationsOfOffering, offering);
            var times = await _j.Query(timesOfOffering, offering);
            var instructors = await _j.Query(instructorsOfOffering, offering);
            if (locations.Count() != 1 || locations[0].building != record.Building || locations[0].room != record.Room)
            {
                await _j.Fact(new OfferingLocation(offering, record.Building, record.Room, locations.AsEnumerable().ToArray()));
            }
            if (times.Count() != 1 || times[0].days != record.Days || times[0].time != record.Time)
            {
                await _j.Fact(new OfferingTime(offering, record.Days, record.Time, times.ToArray()));
            }
            if (instructors.Count() != 1 || _j.Hash(instructors[0].instructor) != _j.Hash(instructor))
            {
                await _j.Fact(new OfferingInstructor(offering, instructor, instructors.ToArray()));
            }

            Console.WriteLine($"Imported {record.CourseCode} {record.CourseName}");
        }

        private void MoveFileToProcessed(string filePath)
        {
            var processedPath = Path.Combine(_processedDataPath, Path.GetFileName(filePath));
            File.Move(filePath, processedPath);
        }

        private void LogError(Exception ex, string filePath)
        {
            Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
        }

        private void MoveFileToError(string filePath)
        {
            var errorPath = Path.Combine(_errorDataPath, Path.GetFileName(filePath));
            File.Move(filePath, errorPath);
        }
    }
}