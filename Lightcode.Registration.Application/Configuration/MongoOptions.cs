namespace Lightcode.Registration.Application.Configuration;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    public string ConnectionString { get; set; } = "mongodb://127.0.0.1:27017";

    public string MasterDatabaseName { get; set; } = "SaasMasterDb";
}
