using System.Diagnostics.Metrics;
using Jinaga;
using Serilog;
using University.Common;
using University.Model;

namespace University.Firehose;

internal class Firehose : IService
{
    private readonly JinagaClient _j;
    private readonly Organization _university;
    private readonly Meter _meter;
    private readonly ILogger _logger;

    private CancellationTokenSource _finish = new();

    public Firehose(JinagaClient j, Organization university, Meter meter, ILogger logger)
    {
        _j = j;
        _university = university;
        _meter = meter;
        _logger = logger;
    }

    public Task Start()
    {
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

                // Keep running until the task is cancelled
                while (!_finish.Token.IsCancellationRequested)
                {
                    var course = courses[random.Next(courses.Count)];
                    var semester = semesters[random.Next(semesters.Count)];
                    var instructor = instructors[random.Next(instructors.Count)];
                    var days = possibleDays[random.Next(possibleDays.Length)];
                    var time = (8 + random.Next(12)).ToString() + ":00";
                    var building = possibleBuildings[random.Next(possibleBuildings.Length)];
                    var room = possibleRooms[random.Next(possibleRooms.Length)];
                    var offering = await _j.Fact(new Offering(course, semester, Guid.NewGuid()));
                    await _j.Fact(new OfferingLocation(offering, building, room, []));
                    await _j.Fact(new OfferingTime(offering, days, time, []));
                    await _j.Fact(new OfferingInstructor(offering, instructor, []));

                    counter.Add(1);

                    await Task.Delay(100, _finish.Token);
                }
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
        _finish.Cancel();
        return Task.CompletedTask;
    }
}
