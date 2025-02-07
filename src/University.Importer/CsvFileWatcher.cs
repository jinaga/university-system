using Jinaga;
using University.Model;
using CsvHelper;
using System.Globalization;

namespace University.Importer
{
    public class CsvFileWatcher
    {
        private readonly JinagaClient _j;
        private readonly Organization _university;

        public CsvFileWatcher(JinagaClient j, Organization university)
        {
            _j = j;
            _university = university;
        }

        public void StartWatching(string path)
        {
            var watcher = new FileSystemWatcher
            {
                Path = path,
                Filter = "*.csv",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };

            watcher.Created += OnNewFile;
            watcher.EnableRaisingEvents = true;
        }

        private void OnNewFile(object source, FileSystemEventArgs e)
        {
            ImportCsvFile(e.FullPath);
        }

        private void ImportCsvFile(string filePath)
        {
            try
            {
                using var reader = new StreamReader(filePath);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                var records = csv.GetRecords<CourseRecord>().ToList();

                foreach (var record in records)
                {
                    CreateFacts(record);
                }

                MoveFileToProcessed(filePath);
            }
            catch (Exception ex)
            {
                LogError(ex, filePath);
                MoveFileToError(filePath);
            }
        }

        private async void CreateFacts(CourseRecord record)
        {
            var course = await _j.Fact(new Course(_university, record.CourseCode, record.CourseName));
            var semester = await _j.Fact(new Semester(_university, record.Year, record.Term));
            var instructor = await _j.Fact(new Instructor(_university, record.Instructor));
            var offering = await _j.Fact(new Offering(course, semester, Guid.NewGuid()));
            await _j.Fact(new OfferingLocation(offering, record.Building, record.Room, new OfferingLocation[0]));
            await _j.Fact(new OfferingTime(offering, record.Days, record.Time, new OfferingTime[0]));
            await _j.Fact(new OfferingInstructor(offering, instructor, new OfferingInstructor[0]));
        }

        private void MoveFileToProcessed(string filePath)
        {
            var processedPath = Path.Combine("path/to/processed", Path.GetFileName(filePath));
            File.Move(filePath, processedPath);
        }

        private void LogError(Exception ex, string filePath)
        {
            // Log the error details
        }

        private void MoveFileToError(string filePath)
        {
            var errorPath = Path.Combine("path/to/error", Path.GetFileName(filePath));
            File.Move(filePath, errorPath);
        }
    }
}
