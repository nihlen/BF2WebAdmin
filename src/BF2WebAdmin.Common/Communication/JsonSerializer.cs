using System;
using System.Text;
using BF2WebAdmin.Common.Abstractions;
using Newtonsoft.Json;

namespace BF2WebAdmin.Common.Communication;

public class JsonSerializer : IMessageSerializer
{
    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        //ContractResolver = new CamelCasePropertyNamesContractResolver(),
        TypeNameHandling = TypeNameHandling.Auto
    };

    public byte[] Serialize<T>(T message)
    {
        var json = JsonConvert.SerializeObject(message, SerializerSettings);
        return Encoding.UTF8.GetBytes(json);
    }

    public T Deserialize<T>(byte[] source)
    {
        try
        {
            var message = Encoding.UTF8.GetString(source);
            return JsonConvert.DeserializeObject<T>(message, SerializerSettings);
        }
        catch (Exception ex)
        {
            //Logger.LogError($"ERROR: Message is not valid JSON", ex);
            throw;
            return default(T);
        }
    }
}