using System;
using System.Configuration;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;

namespace BF2WebAdmin.Common
{
    public class CountryResolver
    {
        public static CountryResponse GetCountryResponse(string ipAddress)
        {
            var cached = CacheManager.Get<CountryResponse>(ipAddress);
            if (cached != null)
                return cached;

            var dbPath = ConfigurationManager.AppSettings["GeoipDbPath"];
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