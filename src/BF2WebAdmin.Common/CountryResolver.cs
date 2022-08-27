using System;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;

namespace BF2WebAdmin.Common
{
    public interface ICountryResolver
    {
        CountryResponse GetCountryResponse(string ipAddress);
    }

    public class CountryResolver : ICountryResolver
    {
        private readonly string _geoipDatabasePath;

        public CountryResolver(string geoipDatabasePath)
        {
            _geoipDatabasePath = geoipDatabasePath;
        }

        public CountryResponse GetCountryResponse(string ipAddress)
        {
            var cached = CacheManager.Get<CountryResponse>(ipAddress);
            if (cached != null)
                return cached;

            if (string.IsNullOrWhiteSpace(_geoipDatabasePath))
                throw new Exception("No GeoipDbPath found in appsettings");

            using var reader = new DatabaseReader(_geoipDatabasePath);
            if (!reader.TryCountry(ipAddress, out var response)) 
                return null;
            
            CacheManager.Add(ipAddress, response, DateTime.UtcNow.AddDays(1));
            return response;
        }
    }
}
