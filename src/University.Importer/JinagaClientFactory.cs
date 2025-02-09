using Jinaga;

namespace University.Importer
{
    public static class JinagaClientFactory
    {
        public static JinagaClient CreateClient(string replicatorUrl)
        {
            return JinagaClient.Create(options =>
            {
                options.HttpEndpoint = new Uri(replicatorUrl);
            });
        }
    }
}
