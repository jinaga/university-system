using Jinaga;
using University.Model;

namespace University.Importer
{
    public static class UniversityDataSeeder
    {
        public static async Task<Organization> SeedData(JinagaClient j, string environmentPublicKey)
        {
            var creator = await j.Fact(new User(environmentPublicKey));
            var university = await j.Fact(new Organization(creator, "6003"));

            return university;
        }
    }
}
