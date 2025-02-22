using Jinaga;
using Jinaga.Store.SQLite;

namespace University.Importer
{
    public static class JinagaClientFactory
    {
        public static JinagaClient CreateClient(string replicatorUrl)
        {
            return JinagaSQLiteClient.Create(options =>
            {
                options.HttpEndpoint = new Uri(replicatorUrl);
                options.SQLitePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "data",
                    "University.Importer",
                    "jinaga.db");
            });
        }
    }
}
