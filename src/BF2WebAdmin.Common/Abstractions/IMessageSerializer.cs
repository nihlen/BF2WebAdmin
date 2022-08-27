namespace BF2WebAdmin.Common.Abstractions;

public interface IMessageSerializer
{
    byte[] Serialize<T>(T message);
    T Deserialize<T>(byte[] source);
}