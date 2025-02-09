using Jinaga;
using University.Model;
using System.Text;

namespace University.Importer
{
    public static class UniversityDataSeeder
    {
        public static async Task<Organization> SeedData(JinagaClient j)
        {
            var creator = await j.Fact(new User(Environment.GetEnvironmentVariable("ENVIRONMENT_PUBLIC_KEY")));
            var university = await j.Fact(new Organization(creator, "6003"));

            return university;
        }
    }
}
