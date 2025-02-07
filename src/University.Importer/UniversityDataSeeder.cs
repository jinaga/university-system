using Jinaga;
using University.Model;

namespace University.Importer
{
    public static class UniversityDataSeeder
    {
        public static async Task<Organization> SeedData(JinagaClient j)
        {
            var creator = await j.Fact(new User(Environment.GetEnvironmentVariable("ENVIRONMENT_PUBLIC_KEY")));
            var university = await j.Fact(new Organization(creator, "6003"));

            List<Course> courses = new List<Course>
            {
                await j.Fact(new Course(university, "CS 101", "Introduction to Computer Science")),
                await j.Fact(new Course(university, "CS 201", "Data Structures and Algorithms")),
                await j.Fact(new Course(university, "CS 301", "Software Engineering")),
                await j.Fact(new Course(university, "CS 401", "Artificial Intelligence")),
                await j.Fact(new Course(university, "CS 501", "Machine Learning")),
                await j.Fact(new Course(university, "CS 601", "Quantum Computing"))
            };

            List<Instructor> instructors = new List<Instructor>
            {
                await j.Fact(new Instructor(university, "Dr. Smith")),
                await j.Fact(new Instructor(university, "Dr. Jones")),
                await j.Fact(new Instructor(university, "Dr. Lee")),
                await j.Fact(new Instructor(university, "Dr. Kim")),
                await j.Fact(new Instructor(university, "Dr. Patel")),
                await j.Fact(new Instructor(university, "Dr. Singh"))
            };

            List<Semester> semesters = new List<Semester>
            {
                await j.Fact(new Semester(university, 2022, "Spring")),
                await j.Fact(new Semester(university, 2022, "Summer")),
                await j.Fact(new Semester(university, 2022, "Fall")),
                await j.Fact(new Semester(university, 2023, "Spring")),
                await j.Fact(new Semester(university, 2023, "Summer")),
                await j.Fact(new Semester(university, 2023, "Fall"))
            };

            var random = new Random(29693);

            List<Offering> offerings = new List<Offering>();
            string[] possibleDays = new string[] { "MF", "TTr", "MW", "WF" };
            string[] possibleBuildings = new string[] { "Building A", "Building B", "Building C", "Building D" };
            string[] possibleRooms = new string[] { "101", "102", "103", "104" };
            for (int i = 0; i < 100; i++)
            {
                var course = courses[random.Next(courses.Count)];
                var semester = semesters[random.Next(semesters.Count)];
                var instructor = instructors[random.Next(instructors.Count)];
                var days = possibleDays[random.Next(possibleDays.Length)];
                var time = (8 + random.Next(12)).ToString() + ":00";
                var building = possibleBuildings[random.Next(possibleBuildings.Length)];
                var room = possibleRooms[random.Next(possibleRooms.Length)];
                var offering = await j.Fact(new Offering(course, semester, Guid.NewGuid()));
                await j.Fact(new OfferingLocation(offering, building, room, new OfferingLocation[0]));
                await j.Fact(new OfferingTime(offering, days, time, new OfferingTime[0]));
                await j.Fact(new OfferingInstructor(offering, instructor, new OfferingInstructor[0]));
                offerings.Add(offering);
            }

            return university;
        }
    }
}
