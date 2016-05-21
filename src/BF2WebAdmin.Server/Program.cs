using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BF2WebAdmin.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            BF2Rcon.SendCommand(IPAddress.Parse("127.0.0.1"), 4711, "secret", "wa connect");
            Console.ReadKey();
        }
    }
}
