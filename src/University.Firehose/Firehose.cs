using System.Diagnostics;
using System.Diagnostics.Metrics;
using Jinaga;
using Serilog;
using University.Common;
using University.Model;

namespace University.Firehose;

internal class Firehose : IService
{
    // Define the ActivitySource for OpenTelemetry tracing
    private static readonly ActivitySource _activitySource = new ActivitySource("University.Firehose");

    private readonly JinagaClient _j;
    private readonly Organization _university;
    private readonly Meter _meter;
    private readonly ILogger _logger;

    private CancellationTokenSource _finish = new();
    private int _targetRatePerSecond = 1;  // Default rate
    private int _currentCount = 0;
    private bool _displayEnabled = false;

    public Firehose(JinagaClient j, Organization university, Meter meter, ILogger logger)
    {
        _j = j;
        _university = university;
        _meter = meter;
        _logger = logger;
    }

    // Method to set the target rate
    public void SetTargetRate(int ratePerSecond)
    {
        _targetRatePerSecond = Math.Max(1, ratePerSecond); // Ensure minimum of 1/second
    }

    public Task Start()
    {
        _finish = new CancellationTokenSource();
        _displayEnabled = true;
        
        // Start display task
        Task.Run(async () => {
            try
            {
                // Capture the current line number
                var lineNo = Console.CursorTop;

                while (_displayEnabled && !_finish.Token.IsCancellationRequested)
                {
                    if (_displayEnabled)
                    {
                        // Set the cursor position before outputting our progress
                        Console.SetCursorPosition(0, lineNo);
                        Console.Write($"Offerings created: {_currentCount}/second (Target: {_targetRatePerSecond}/second)");
                    }
                    _currentCount = 0; // Reset counter each second
                    await Task.Delay(1000, _finish.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, no need to log
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in display task.");
            }
        }, _finish.Token);
        
        // Main firehose task
        Task.Run(async () =>
        {
            try
            {
                var studentUser = await _j.Fact(new User("---PUBLIC KEY---"));
                var student = await _j.Fact(new Student(studentUser));
                var application = await _j.Fact(new Application(student, _university, DateTime.Parse("2022-02-04")));
                var enrollment = await _j.Fact(new Enrollment(application));

                List<Course> courses = [
                    await _j.Fact(new Course(_university, "CS 101", "Introduction to Computer Science")),
                    await _j.Fact(new Course(_university, "CS 201", "Data Structures and Algorithms")),
                    await _j.Fact(new Course(_university, "CS 301", "Software Engineering")),
                    await _j.Fact(new Course(_university, "CS 401", "Artificial Intelligence")),
                    await _j.Fact(new Course(_university, "CS 501", "Machine Learning")),
                    await _j.Fact(new Course(_university, "CS 601", "Quantum Computing"))
                ];
                List<Instructor> instructors = [
                    await _j.Fact(new Instructor(_university, "Dr. Smith")),
                    await _j.Fact(new Instructor(_university, "Dr. Jones")),
                    await _j.Fact(new Instructor(_university, "Dr. Lee")),
                    await _j.Fact(new Instructor(_university, "Dr. Kim")),
                    await _j.Fact(new Instructor(_university, "Dr. Patel")),
                    await _j.Fact(new Instructor(_university, "Dr. Singh"))
                ];

                List<Semester> semesters = [
                    await _j.Fact(new Semester(_university, 2022, "Spring")),
                    await _j.Fact(new Semester(_university, 2022, "Summer")),
                    await _j.Fact(new Semester(_university, 2022, "Fall")),
                    await _j.Fact(new Semester(_university, 2023, "Spring")),
                    await _j.Fact(new Semester(_university, 2023, "Summer")),
                    await _j.Fact(new Semester(_university, 2023, "Fall"))
                ];

                var random = new Random();

                string[] possibleDays = ["MF", "TTr", "MW", "WF"];
                string[] possibleBuildings = ["Building A", "Building B", "Building C", "Building D"];
                string[] possibleRooms = ["101", "102", "103", "104"];

                var counter = _meter.CreateCounter<int>("university.offering.created");
                var stopwatch = new Stopwatch();

                // Keep running until the task is cancelled
                while (!_finish.Token.IsCancellationRequested)
                {
                    stopwatch.Restart();
                    
                    using (var activity = _activitySource.StartActivity("CreateOffering"))
                    {
                        var course = courses[random.Next(courses.Count)];
                        activity?.SetTag("course.code", course.code);
                        var semester = semesters[random.Next(semesters.Count)];
                        activity?.SetTag("semester", $"{semester.year} {semester.term}");
                        
                        var instructor = instructors[random.Next(instructors.Count)];
                        activity?.SetTag("instructor", instructor.name);
                        var days = possibleDays[random.Next(possibleDays.Length)];
                        var time = (8 + random.Next(12)).ToString() + ":00";
                        var building = possibleBuildings[random.Next(possibleBuildings.Length)];
                        var room = possibleRooms[random.Next(possibleRooms.Length)];
                        var offering = await _j.Fact(new Offering(course, semester, Guid.NewGuid()));
                        await _j.Fact(new OfferingLocation(offering, building, room, []));
                        await _j.Fact(new OfferingTime(offering, days, time, []));
                        await _j.Fact(new OfferingInstructor(offering, instructor, []));

                        _currentCount++;
                        counter.Add(1);
                        
                        // Mark activity as successful
                        activity?.SetStatus(ActivityStatusCode.Ok);
                    }
                    
                    stopwatch.Stop();
                    
                    // Calculate remaining delay time
                    int targetDelayMs = 1000 / _targetRatePerSecond;
                    int elapsedMs = (int)stopwatch.ElapsedMilliseconds;
                    int remainingDelayMs = Math.Max(0, targetDelayMs - elapsedMs);
                    
                    if (remainingDelayMs > 0)
                    {
                        await Task.Delay(remainingDelayMs, _finish.Token);
                    }
                    else if (elapsedMs > targetDelayMs)
                    {
                        // Log if we're consistently taking longer than our target rate allows
                        _logger.Warning("Creating offering took {ElapsedMs}ms, which exceeds the target delay of {TargetDelayMs}ms", 
                            elapsedMs, targetDelayMs);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, no need to log
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in firehose task.");
            }
        }, _finish.Token);
        return Task.CompletedTask;
    }

    public Task Stop()
    {
        _displayEnabled = false;
        _finish.Cancel();
        Console.WriteLine(); // Move to next line after stopping
        return Task.CompletedTask;
    }
}
