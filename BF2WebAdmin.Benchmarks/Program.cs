// See https://aka.ms/new-console-template for more information

using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

//var summary = BenchmarkRunner.Run(typeof(Program).Assembly);
var summary = BenchmarkRunner.Run(typeof(SocketServerBenchmarks));

[MemoryDiagnoser]
public class SocketServerBenchmarks
{
    private static ReadOnlySpan<byte> NewLine => new[] { (byte)'\n' };
    //private static ReadOnlySpan<byte> NewLine => new[] { (byte)'\r', (byte)'\n' };
    private static byte NewLineByte = (byte)'\n';
    private Stream _stream;
    //private StreamReader _streamReader;
    //private PipeReader _pipeReader;
    //private byte _delimiterByte;
    //private string _output;

    //[IterationSetup]
    //public void IterationSetup()
    //{
    //    //Console.WriteLine("IterationSetup");
    //    _stream = new MemoryStream(Encoding.UTF8.GetBytes("TestMessage\t123\t123\t123\t123\t123\t123\t123\t123\n"));
    //    _streamReader = new StreamReader(_stream);
    //    _pipeReader = PipeReader.Create(_stream);
    //}

    //[IterationCleanup]
    //public void IterationCleanup()
    //{
    //    //Console.WriteLine("IterationCleanup");
    //    _stream?.Dispose();
    //    _streamReader?.Dispose();
    //    _pipeReader?.Complete();
    //}

    [Params(3_000)]
    public int LineNumber { get; set; }

    [ParamsSource(nameof(LineCharMultiplierValues))]
    public int LineCharMultiplier { get; set; }
    //public IEnumerable<int> LineCharMultiplierValues => new[] { 100 };
    public IEnumerable<int> LineCharMultiplierValues => new[] { 1, 2, 4, 8, 20, 50, 100 };
    //public IEnumerable<int> LineCharMultiplierValues => Enumerable.Range(1, 15).Concat(new[] { 20, 30, 50, 80, 100 });


    [IterationSetup]
    public void IterationSetup()
    {
        _stream = PrepareStream();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _stream.Dispose();
    }

    public Stream PrepareStream()
    {
        var stream = new MemoryStream();

        using var sw = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
        foreach (var no in Enumerable.Range(1, LineNumber))
        {
            foreach (var _ in Enumerable.Range(1, LineCharMultiplier))
            {
                sw.Write($"ABC{no:D7}");
            }
            sw.WriteLine();
        }
        sw.Flush();

        stream.Seek(0, SeekOrigin.Begin);

        return stream;
    }

    [Benchmark]
    public async Task<string> StreamReader_ReadLine()
    {
        _stream.Seek(0, SeekOrigin.Begin);

        string str = null;

        using var sr = new StreamReader(_stream, Encoding.UTF8);
        while ((str = await sr.ReadLineAsync()) is not null)
        {
            // simulate string processing
            str = str.AsSpan().Slice(0, 5).ToString();
        }

        return str;
    }






    [Benchmark]
    public async Task<string> PipeReader_ReadLine()
    {
        _stream.Seek(0, SeekOrigin.Begin);

        // Reference: https://github.com/davidfowl/TcpEcho/blob/master/src/Server/Program.cs

        string str = null;

        var pr = PipeReader.Create(_stream, new StreamPipeReaderOptions(leaveOpen: true));
        while (true)
        {
            var result = await pr.ReadAsync();
            var buffer = result.Buffer;

            while (TryReadLine(ref buffer, out var line))
            {
                // simulate string processing
                str = line.Slice(0, 5).ToString();
            }

            // Tell the PipeReader how much of the buffer has been consumed
            pr.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
            {
                break;
            }
        }

        // Mark the PipeReader as complete.
        await pr.CompleteAsync();

        return str;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
    {
        // Reference: https://dev.to/joni2nja/evaluating-readline-using-system-io-pipelines-performance-in-c-part-2-pmf

        //if (buffer.IsSingleSegment)
        //{
        //    var span = buffer.FirstSpan;
        //    int consumed;
        //    while (span.Length > 0)
        //    {
        //        var newLine = span.IndexOf(NewLine);

        //        //if (newLine == -1) break;
        //        if (newLine == -1)
        //        {
        //            line = default;
        //            return false;
        //        }

        //        var lineSpan = span.Slice(0, newLine);
        //        line = new ReadOnlySequence<byte>(lineSpan.ToArray());
        //        //str = Encoding.UTF8.GetString(line);

        //        //// simulate string processing
        //        //str = str.AsSpan().Slice(0, 5).ToString();

        //        consumed = lineSpan.Length + NewLine.Length;
        //        span = span.Slice(consumed);
        //        buffer = buffer.Slice(consumed);
        //    }
        //}


        // Reference: https://github.com/davidfowl/TcpEcho/blob/master/src/Server/Program.cs

        // Look for a EOL in the buffer.
        var position = buffer.PositionOf(NewLineByte);
        //var position = buffer.PositionOf((byte)'\n');

        if (position == null)
        {
            line = default;
            return false;
        }

        // Skip the line + the \n.
        line = buffer.Slice(0, position.Value);
        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
        return true;
    }






    [Benchmark]
    public async Task<string> PipeReader_ReadLineFastPath()
    {
        _stream.Seek(0, SeekOrigin.Begin);

        // Reference: https://github.com/davidfowl/TcpEcho/blob/master/src/Server/Program.cs

        string str = null;
        //string str;


        var pr = PipeReader.Create(_stream, new StreamPipeReaderOptions(leaveOpen: true));
        while (true)
        {
            var result = await pr.ReadAsync();
            var buffer = result.Buffer;

            if (buffer.IsSingleSegment)
            {
                str = ProcessLine(ref buffer);
                //ProcessLineFast(ref buffer);
            }
            else
            {
                while (TryReadLine(ref buffer, out var line))
                {
                    // simulate string processing
                    str = line.Slice(0, 5).ToString();
                }
                //while (TryReadLine(ref buffer, out var line))
                //{
                //    // simulate string processing
                //    str = line.Slice(0, 5).ToString();
                //}
            }

            // Tell the PipeReader how much of the buffer has been consumed
            pr.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
            {
                break;
            }
        }

        // Mark the PipeReader as complete.
        await pr.CompleteAsync();

        return str;
    }

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //private static string ProcessLineFast(ref ReadOnlySequence<byte> buffer)
    //{
    //    string str = null;

    //    var span = buffer.FirstSpan;
    //    int consumed;
    //    while (span.Length > 0)
    //    {
    //        var newLine = span.IndexOf(NewLine);

    //        if (newLine == -1) break;

    //        var line = span.Slice(0, newLine);
    //        str = Encoding.UTF8.GetString(line);

    //        // simulate string processing
    //        str = str.AsSpan().Slice(0, 5).ToString();

    //        consumed = line.Length + NewLine.Length;
    //        span = span.Slice(consumed);
    //        buffer = buffer.Slice(consumed);
    //    }

    //    return str;
    //}







    [Benchmark]
    public async Task<string> ReadLineUsingPipelineVer2Async()
    {
        _stream.Seek(0, SeekOrigin.Begin);

        var reader = PipeReader.Create(_stream, new StreamPipeReaderOptions(leaveOpen: true));
        string str;

        while (true)
        {
            ReadResult result = await reader.ReadAsync();
            ReadOnlySequence<byte> buffer = result.Buffer;

            str = ProcessLine(ref buffer);

            reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted) break;
        }

        await reader.CompleteAsync();
        return str;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ProcessLine(ref ReadOnlySequence<byte> buffer)
    {
        string str = null;

        if (buffer.IsSingleSegment)
        {
            var span = buffer.FirstSpan;
            int consumed;
            while (span.Length > 0)
            {
                var newLine = span.IndexOf(NewLine);

                if (newLine == -1) break;

                var line = span.Slice(0, newLine);
                str = Encoding.UTF8.GetString(line);

                // simulate string processing
                str = str.AsSpan().Slice(0, 5).ToString();

                consumed = line.Length + NewLine.Length;
                span = span.Slice(consumed);
                buffer = buffer.Slice(consumed);
            }
        }
        else
        {
            //var sequenceReader = new SequenceReader<byte>(buffer);

            //while (!sequenceReader.End)
            //{
            //    while (sequenceReader.TryReadTo(out ReadOnlySequence<byte> line, NewLine))
            //    {
            //        str = Encoding.UTF8.GetString(line);

            //        // simulate string processing
            //        str = str.AsSpan().Slice(0, 5).ToString();
            //    }

            //    buffer = buffer.Slice(sequenceReader.Position);
            //    sequenceReader.Advance(buffer.Length);
            //}

            // Look for a EOL in the buffer.
            //SequencePosition? position = buffer.PositionOf((byte)'\n');
            var position = buffer.PositionOf(NewLine[0]);

            if (position == null)
            {
                str = default;
                return str;
            }

            // Skip the line + the \n.
            //str = buffer.Slice(0, position.Value);
            //str = Encoding.UTF8.GetString(buffer.Slice(0, position.Value));
            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
            return str;
        }

        return str;
    }























    [Benchmark]
    public async Task<string> ReadLineUsingPipelineVer2Async2()
    {
        _stream.Seek(0, SeekOrigin.Begin);

        var reader = PipeReader.Create(_stream, new StreamPipeReaderOptions(leaveOpen: true));
        string str = null;

        while (true)
        {
            ReadResult result = await reader.ReadAsync();
            ReadOnlySequence<byte> buffer = result.Buffer;

            if (buffer.IsSingleSegment)
            {
                str = ProcessLine2(ref buffer);
            }
            else
            {
                str = ProcessLine2(ref buffer);
                //while (TryReadLine(ref buffer, out var line))
                //{
                //    // simulate string processing
                //    str = line.Slice(0, 5).ToString();
                //}
            }

            reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted) break;
        }

        await reader.CompleteAsync();
        return str;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ProcessLine2(ref ReadOnlySequence<byte> buffer)
    {
        string str = null;

        var span = buffer.FirstSpan;
        int consumed;
        while (span.Length > 0)
        {
            var newLine = span.IndexOf(NewLine);

            if (newLine == -1) break;

            var line = span.Slice(0, newLine);
            str = Encoding.UTF8.GetString(line);

            // simulate string processing
            str = str.AsSpan().Slice(0, 5).ToString();

            consumed = line.Length + NewLine.Length;
            span = span.Slice(consumed);
            buffer = buffer.Slice(consumed);
        }

        return str;
    }














    [Benchmark]
    public async Task<string> ReadLineUsingPipelineVer2AsyncA()
    {
        _stream.Seek(0, SeekOrigin.Begin);

        var reader = PipeReader.Create(_stream, new StreamPipeReaderOptions(leaveOpen: true));
        string str;

        while (true)
        {
            ReadResult result = await reader.ReadAsync();
            ReadOnlySequence<byte> buffer = result.Buffer;

            str = ProcessLineA(ref buffer);

            reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted) break;
        }

        await reader.CompleteAsync();
        return str;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ProcessLineA(ref ReadOnlySequence<byte> buffer)
    {
        string str = null;

        if (buffer.IsSingleSegment)
        {
            var span = buffer.FirstSpan;
            int consumed;
            while (span.Length > 0)
            {
                var newLine = span.IndexOf(NewLine);

                if (newLine == -1) break;

                var line = span.Slice(0, newLine);
                str = Encoding.UTF8.GetString(line);

                // simulate string processing
                str = str.AsSpan().Slice(0, 5).ToString();

                consumed = line.Length + NewLine.Length;
                span = span.Slice(consumed);
                buffer = buffer.Slice(consumed);
            }
        }
        else
        {
            var sequenceReader = new SequenceReader<byte>(buffer);

            while (!sequenceReader.End)
            {
                while (sequenceReader.TryReadTo(out ReadOnlySpan<byte> line, NewLine))
                //while (sequenceReader.TryReadTo(out var line, NewLine))
                {
                    str = Encoding.UTF8.GetString(line);

                    // simulate string processing
                    str = str.AsSpan().Slice(0, 5).ToString();
                }

                buffer = buffer.Slice(sequenceReader.Position);
                sequenceReader.Advance(buffer.Length);
            }
        }

        return str;
    }




}

[MemoryDiagnoser]
public class GameWriterBenchmarks
{
    private Encoding _encoding;
    private BinaryWriter _writer;
    private string _message = "TestMessage\t123\t123\t123\t123\t123\t123\t123\t123";
    private byte _delimiterByte;

    public GameWriterBenchmarks()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _encoding = Encoding.GetEncoding(1252);
        _writer = BinaryWriter.Null;
        _delimiterByte = _encoding.GetBytes("\n").Single();
    }

    [Benchmark]
    public void EasyEncode()
    {
        var bytes = _encoding.GetBytes(_message + "\n");
        _writer.Write(bytes);
    }

    [Benchmark]
    public void BetterEncode1()
    {
        var delimitedMessage = $"{_message}\n";
        var numCharactersNeeded = _encoding.GetByteCount(delimitedMessage);
        using var memory = MemoryPool<byte>.Shared.Rent(numCharactersNeeded);
        _encoding.GetBytes(delimitedMessage.AsSpan(), memory.Memory.Span);
        _writer.Write(memory.Memory.Span);
    }

    [Benchmark]
    public void BetterEncode3()
    {
        var delimitedMessage = $"{_message}\n";
        var bufferSize = _encoding.GetMaxByteCount(delimitedMessage.Length);
        using var memory = MemoryPool<byte>.Shared.Rent(bufferSize);
        var encodedBytes = _encoding.GetBytes(delimitedMessage.AsSpan(), memory.Memory.Span);
        _writer.Write(memory.Memory.Span[..encodedBytes]);
    }

    [Benchmark]
    public void BetterEncode4()
    {
        var bufferSize = _encoding.GetMaxByteCount(_message.Length + 1);
        using var memory = MemoryPool<byte>.Shared.Rent(bufferSize);
        var encodedBytes = _encoding.GetBytes(_message.AsSpan(), memory.Memory.Span);
        _writer.Write(memory.Memory.Span[..encodedBytes]);
        _writer.Write(_delimiterByte);
    }

    [Benchmark]
    public void BetterEncode5()
    {
        var bufferSize = _encoding.GetMaxByteCount(_message.Length + 1);
        using var memory = MemoryPool<byte>.Shared.Rent(bufferSize);
        var encodedBytes = _encoding.GetBytes(_message.AsSpan(), memory.Memory.Span);
        memory.Memory.Span[encodedBytes] = _delimiterByte;
        _writer.Write(memory.Memory.Span[..(encodedBytes + 1)]);
    }
}
















//using BenchmarkDotNet.Attributes;
//using BenchmarkDotNet.Running;
//using System;
//using System.Buffers;
//using System.Collections.Generic;
//using System.IO;
//using System.IO.Pipelines;
//using System.Linq;
//using System.Runtime.CompilerServices;
//using System.Text;
//using System.Threading.Tasks;


//// https://github.com/jo-ninja/ReadLinesBenchmarks/blob/master/ReadLinesBenchmarks/Program.cs
//[DisassemblyDiagnoser(printSource: true)]
//[MemoryDiagnoser]
//public class ReadLinesBenchmarks
//{
//    private static ReadOnlySpan<byte> NewLine => new[] { (byte)'\r', (byte)'\n' };

//    private Stream _stream;

//    [Params(20/*, 300_000*/)]
//    public int LineNumber { get; set; }

//    [ParamsSource(nameof(LineCharMultiplierValues))]
//    public int LineCharMultiplier { get; set; }
//    public IEnumerable<int> LineCharMultiplierValues => new[] { 1, 2, 8, 16, 100 };
//    //public IEnumerable<int> LineCharMultiplierValues => Enumerable.Range(1, 15).Concat(new[] { 20, 30, 50, 80, 100 });

//    [GlobalSetup]
//    public void GlobalSetup()
//    {
//        _stream = PrepareStream();
//    }

//    [GlobalCleanup]
//    public void GlobalCleanup()
//    {
//        _stream.Dispose();
//    }

//    public Stream PrepareStream()
//    {
//        var stream = new MemoryStream();

//        using var sw = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
//        foreach (var no in Enumerable.Range(1, LineNumber))
//        {
//            foreach (var _ in Enumerable.Range(1, LineCharMultiplier))
//            {
//                sw.Write($"ABC{no:D7}");
//            }
//            sw.WriteLine();
//        }
//        sw.Flush();

//        return stream;
//    }

//    [Benchmark(Baseline = true)]
//    public async Task<string> ReadLineUsingStringReaderAsync()
//    {
//        _stream.Seek(0, SeekOrigin.Begin);

//        var sr = new StreamReader(_stream, Encoding.UTF8);
//        string str;
//        while ((str = await sr.ReadLineAsync()) is not null)
//        {
//            // simulate string processing
//            str = str.AsSpan().Slice(0, 5).ToString();
//        }

//        return str;
//    }

//    [Benchmark]
//    public async Task<string> ReadLineUsingPipelineAsync()
//    {
//        _stream.Seek(0, SeekOrigin.Begin);

//        var reader = PipeReader.Create(_stream, new StreamPipeReaderOptions(leaveOpen: true));
//        string str;
//        while (true)
//        {
//            ReadResult result = await reader.ReadAsync();
//            ReadOnlySequence<byte> buffer = result.Buffer;

//            while ((str = ReadLine(ref buffer)) is not null)
//            {
//                // simulate string processing
//                str = str.AsSpan().Slice(0, 5).ToString();
//            }

//            reader.AdvanceTo(buffer.Start, buffer.End);

//            if (result.IsCompleted) break;
//        }

//        await reader.CompleteAsync();
//        return str;
//    }

//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    private static string ReadLine(ref ReadOnlySequence<byte> buffer)
//    {
//        var reader = new SequenceReader<byte>(buffer);

//        if (reader.TryReadTo(out ReadOnlySpan<byte> line, NewLine))
//        //if (reader.TryReadTo(out var line, NewLine))
//        {
//            buffer = buffer.Slice(reader.Position);
//            return Encoding.UTF8.GetString(line);
//        }

//        return default;
//    }

//    [Benchmark]
//    public async Task<string> ReadLineUsingPipelineVer2Async()
//    {
//        _stream.Seek(0, SeekOrigin.Begin);

//        var reader = PipeReader.Create(_stream, new StreamPipeReaderOptions(leaveOpen: true));
//        string str;

//        while (true)
//        {
//            ReadResult result = await reader.ReadAsync();
//            ReadOnlySequence<byte> buffer = result.Buffer;

//            str = ProcessLine(ref buffer);

//            reader.AdvanceTo(buffer.Start, buffer.End);

//            if (result.IsCompleted) break;
//        }

//        await reader.CompleteAsync();
//        return str;
//    }

//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    private static string ProcessLine(ref ReadOnlySequence<byte> buffer)
//    {
//        string str = null;

//        if (buffer.IsSingleSegment)
//        {
//            var span = buffer.FirstSpan;
//            int consumed;
//            while (span.Length > 0)
//            {
//                var newLine = span.IndexOf(NewLine);

//                if (newLine == -1) break;

//                var line = span.Slice(0, newLine);
//                str = Encoding.UTF8.GetString(line);

//                // simulate string processing
//                str = str.AsSpan().Slice(0, 5).ToString();

//                consumed = line.Length + NewLine.Length;
//                span = span.Slice(consumed);
//                buffer = buffer.Slice(consumed);
//            }
//        }
//        else
//        {
//            var sequenceReader = new SequenceReader<byte>(buffer);

//            while (!sequenceReader.End)
//            {
//                while (sequenceReader.TryReadTo(out ReadOnlySpan<byte> line, NewLine))
//                //while (sequenceReader.TryReadTo(out var line, NewLine))
//                {
//                    str = Encoding.UTF8.GetString(line);

//                    // simulate string processing
//                    str = str.AsSpan().Slice(0, 5).ToString();
//                }

//                buffer = buffer.Slice(sequenceReader.Position);
//                sequenceReader.Advance(buffer.Length);
//            }
//        }

//        return str;
//    }
//}

//class Program
//{
//    static void Main(string[] args)
//    {
//#if DEBUG
//            var summary = BenchmarkRunner.Run<ReadLinesBenchmarks>(new BenchmarkDotNet.Configs.DebugInProcessConfig());
//#else
//        var summary = BenchmarkRunner.Run<ReadLinesBenchmarks>();
//#endif
//    }
//}
