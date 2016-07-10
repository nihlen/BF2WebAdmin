using System.Threading.Tasks;

namespace BF2WebAdmin.Server.Abstractions
{
    public interface IRconClient
    {
        Task<string> SendAsync(string command);
    }
}