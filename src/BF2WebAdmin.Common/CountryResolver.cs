using System;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;
using Microsoft.Extensions.Configuration;

namespace BF2WebAdmin.Common
{
    public class CountryResolver
    {
        private static readonly IConfigurationRoot Config;

        static CountryResolver()
        {
            var builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            Config = builder.Build();
        }

        public static CountryResponse GetCountryResponse(string ipAddress)
        {
            var cached = CacheManager.Get<CountryResponse>(ipAddress);
            if (cached != null)
                return cached;

            var dbPath = Config["Geoip:DatabasePath"];
            //var dbPath = ConfigurationManager.AppSettings["GeoipDbPath"];
            if (dbPath == null)
                throw new Exception("No GeoipDbPath found in appsettings");

            using (var reader = new DatabaseReader(dbPath))
            {
                var response = reader.Country(ipAddress);
                CacheManager.Add(ipAddress, response, DateTime.UtcNow.AddDays(1));
                return response;
            }
        }
    }
}