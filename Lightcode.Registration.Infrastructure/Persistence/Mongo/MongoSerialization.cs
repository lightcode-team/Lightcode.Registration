using Lightcode.Registration.Domain.Entities;
using MongoDB.Bson.Serialization;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public static class MongoSerialization
{
    private static readonly Lock Sync = new();
    private static bool _registered;

    public static void EnsureRegistered()
    {
        lock (Sync)
        {
            if (_registered)
                return;

            RegisterTenant();
            RegisterWeatherForecast();
            RegisterAccountJsonSchema();
            _registered = true;
        }
    }

    private static void RegisterTenant()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(Tenant)))
            return;

        BsonClassMap.RegisterClassMap<Tenant>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdProperty(t => t.Id);
        });
    }

    private static void RegisterWeatherForecast()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(WeatherForecast)))
            return;

        BsonClassMap.RegisterClassMap<WeatherForecast>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdProperty(w => w.Id);
            cm.UnmapProperty(w => w.TemperatureF);
        });
    }

    private static void RegisterAccountJsonSchema()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(AccountJsonSchema)))
            return;

        BsonClassMap.RegisterClassMap<AccountJsonSchema>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdProperty(x => x.Id);
            cm.MapProperty(x => x.SchemaJson).SetSerializer(new SchemaJsonBsonSerializer());
            cm.MapProperty(x => x.ConfigJson).SetSerializer(new SchemaJsonBsonSerializer());
        });
    }
}
