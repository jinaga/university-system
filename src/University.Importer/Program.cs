using Jinaga;
using University.Model;

var REPLICATOR_URL = Environment.GetEnvironmentVariable("REPLICATOR_URL");
var ENVIRONMENT_PUBLIC_KEY = Environment.GetEnvironmentVariable("ENVIRONMENT_PUBLIC_KEY");

if (REPLICATOR_URL == null || ENVIRONMENT_PUBLIC_KEY == null)
{
    if (REPLICATOR_URL == null)
    {
        Console.WriteLine("Please set the environment variable REPLICATOR_URL.");
    }
    if (ENVIRONMENT_PUBLIC_KEY == null)
    {
        Console.WriteLine("Please set the environment variable ENVIRONMENT_PUBLIC_KEY.");
    }
    return;
}

Console.WriteLine("Importing courses...");

var j = JinagaClient.Create(options =>
{
    options.HttpEndpoint = new Uri(REPLICATOR_URL);
});

var creator = await j.Fact(new User(ENVIRONMENT_PUBLIC_KEY));
var university = await j.Fact(new Organization(creator, "6003"));

List<Course> courses = [
    await j.Fact(new Course(university, "CS 101", "Introduction to Computer Science")),
    await j.Fact(new Course(university, "CS 201", "Data Structures and Algorithms")),
    await j.Fact(new Course(university, "CS 301", "Software Engineering")),
    await j.Fact(new Course(university, "CS 401", "Artificial Intelligence")),
    await j.Fact(new Course(university, "CS 501", "Machine Learning")),
    await j.Fact(new Course(university, "CS 601", "Quantum Computing"))
];