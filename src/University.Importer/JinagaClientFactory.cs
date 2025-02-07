using Jinaga;

namespace University.Importer
{
    public static class JinagaClientFactory
    {
        public static JinagaClient CreateClient()
        {
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
                return null;
            }

            return JinagaClient.Create(options =>
            {
                options.HttpEndpoint = new Uri(REPLICATOR_URL);
            });
        }
    }
}
