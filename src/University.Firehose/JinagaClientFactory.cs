using Jinaga;
using Jinaga.Store.SQLite;

namespace University.Firehose
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
                    "University.Firehose",
                    "jinaga.db");
            });
        }
    }
}
