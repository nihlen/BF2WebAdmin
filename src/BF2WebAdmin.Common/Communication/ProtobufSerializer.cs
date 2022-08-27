using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BF2WebAdmin.Common.Abstractions;
using BF2WebAdmin.Shared;
using ProtoBuf;

namespace BF2WebAdmin.Common.Communication;

public class ProtobufSerializer : IMessageSerializer
{
    public ProtobufSerializer(string protoPath = null)
    {
        // TODO: only save in debug mode - move to another file
        if (protoPath != null)
            SaveProto(protoPath);

        //SaveProto(@"C:\Projects\DotNet\BF2WebAdmin\src\BF2WebAdmin.Web\BF2WebAdmin.Web\App\messages.proto");
    }

    public byte[] Serialize<T>(T message)
    {
        using (var stream = new MemoryStream())
        {
            Serializer.Serialize(stream, message);
            return stream.ToArray();
        }
    }

    public T Deserialize<T>(byte[] source)
    {
        using (var stream = new MemoryStream(source))
        {
            return Serializer.Deserialize<T>(stream);
        }
    }

    private static void SaveProto(string path)
    {
        var sb = new StringBuilder();

        // Include bcl.proto
        //sb.Append("message TimeSpan { optional sint64 value = 1; optional TimeSpanScale scale = 2 [default = DAYS]; enum TimeSpanScale { DAYS = 0; HOURS = 1; MINUTES = 2; SECONDS = 3; MILLISECONDS = 4; 	TICKS = 5; MINMAX = 15; } } message DateTime { optional sint64 value = 1; optional TimeSpanScale scale = 2 [default = DAYS]; enum TimeSpanScale { DAYS = 0; HOURS = 1; MINUTES = 2; SECONDS = 3; MILLISECONDS = 4; 	TICKS = 5; MINMAX = 15; } } message NetObjectProxy { optional int32 existingObjectKey = 1; optional int32 newObjectKey = 2; optional int32 existingTypeKey = 3; optional int32 newTypeKey = 4; optional string typeName = 8; optional bytes payload = 10; } message Guid { optional fixed64 lo = 1; optional fixed64 hi = 2; } message Decimal { optional uint64 lo = 1; optional uint32 hi = 2; optional uint32 signScale = 3; }");

        sb.Append(Serializer.GetProto<IMessage>());
        //sb.Append(Serializer.GetProto<MessageEvent>());
        //sb.Append(Serializer.GetProto<MessageAction>());
        //sb.Append(Serializer.GetProto<MessageProto>());

        // Protobuf.js doesn't support imports so we inline it
        var result = Regex.Replace(sb.ToString(), @"(import|package)[^\n]*\n", string.Empty, RegexOptions.Compiled);
        //result = Regex.Replace(result, @"bcl\.", string.Empty, RegexOptions.Compiled);

        File.WriteAllText(path, result);
    }
}