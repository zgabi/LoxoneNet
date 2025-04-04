using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace LoxoneNet.Growatt;

internal class Growatt
{
    private string _serverHost;
    private Thread _thread;
    private bool _running;
    public DateTime LastUpdateTime { get; private set; }

    public void Start(string serverHost)
    {
        _serverHost = serverHost;
        _running = true;
        _thread = new Thread(ThreadFunc);
        _thread.IsBackground = true;
        _thread.Start();
    }

    private void Stop()
    {
        _running = false;
    }

    private void ThreadFunc()
    {
        Task.Run(Loop);
    }


    private async Task Loop()
    {
        const int port = 5279;
        Program.Log($"Growatt reader started. Address: {_serverHost}:{port}");
        while (_running)
        {
            TcpListener? listener = null;
            var lastProcessTime = DateTime.UtcNow;
            try
            {
                listener = new TcpListener(new IPEndPoint(IPAddress.Any, port));
                listener.Start();
                using var socket = await listener.AcceptSocketAsync();
                Program.Log("Growatt socket accepted");
                var client = new TcpClient(_serverHost, port);
                var clientSocket = client.GetStream().Socket;
                Program.Log("Growatt server connected. KeepAlive: " + clientSocket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive));
                clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                var bufClient = new byte[65536];
                var bufServer = new byte[65536];
                var bufData = new byte[65536];
                int clientIdx = 0;
                int serverIdx = 0;
                while (socket.Connected)
                {
                    bool processed = Receive(socket, bufServer, ref serverIdx, clientSocket, false);

                    Process(bufServer, ref serverIdx, bufData, false);

                    processed |= Receive(clientSocket, bufClient, ref clientIdx, socket, true);

                    Process(bufClient, ref clientIdx, bufData, true);

                    var now = DateTime.UtcNow;
                    if (!processed)
                    {
                        Thread.Sleep(100);
                    }
                    else
                    {
                        lastProcessTime = now;
                    }

                    if (lastProcessTime.AddMinutes(5) < now)
                    {
                        Program.Log("Growatt disconnect.");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log("Growatt exception: " + ex);
            }
            finally
            {
                listener?.Stop();
            }

            Thread.Sleep(1000);
        }
    }

    private static bool Receive(Socket source, byte[] buffer, ref int bufferIdx, Socket target, bool fromServer)
    {
        if (source.Available > 0)
        {
            int received = source.Receive(buffer.AsSpan(bufferIdx));
            if (received == 0)
            {
                return false;
            }

            var data = buffer.AsSpan(bufferIdx, received);
            target.Send(data);
            bufferIdx += received;

            string sourceName = fromServer ? "server" : "Lan box";
            Program.Log(LogLevel.Trace, $"Received {received} bytes from {sourceName}:");
            Program.Log(LogLevel.Trace, string.Join(' ', data.ToArray().Select(x => x.ToString("X2"))));

            return true;
        }

        return false;
    }

    private void Process(byte[] data, ref int bufferIdx, Span<byte> output, bool fromServer)
    {
        Span<byte> span = data.AsSpan(0, bufferIdx);
        while (span.Length >= 6)
        {
            ushort transactionId = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(0));
            ushort protocol = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(2));
            ushort length = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(4));

            if (span.Length < 8 + length)
            {
                break;
            }

            int pos = length + 6;
            uint crc = Crc16Modbus(span[..pos]);
            uint crcExpected = BinaryPrimitives.ReadUInt16BigEndian(span[pos..]);

            if (crc != crcExpected)
            {
                ;
            }

            ushort command = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(6));

            span.Slice(8, length - 2).CopyTo(output);
            var body = output.Slice(0, length - 2);
            Decrypt(body);

            if (command == 0x5104 && !fromServer && body.Length >= 191)
            {
                var pkt = new Growatt5104(body);
                var loxone = Program.LoxoneSocket;
                if (loxone != null)
                {
                    if (LastUpdateTime == default)
                    {
                        Program.Log($"Send Growatt data to loxone: {pkt.pvenergytoday} {pkt.pvpowerout}");
                    }

                    loxone.SendCommandUdp("InverterMeter" + pkt.pvenergytotal / 10.0);
                    loxone.SendCommandUdp("InverterPower" + pkt.pvpowerout / 10000.0);
                    LastUpdateTime = DateTime.UtcNow;
                }
            }
            else
            {
                Program.Log(LogLevel.Trace, ToLiteral(Encoding.ASCII.GetString(body)));
            }

            span = span.Slice(8 + length);
        }

        if (span.Length > 0)
        {
            ;
        }

        bufferIdx = 0;
    }

    private static void Decrypt(Span<byte> body)
    {
        var key = "Growatt"u8;
        for (int i = 0; i < body.Length; i++)
        {
            body[i] ^= key[i % key.Length];
        }
    }

    static string ToLiteral(string input)
    {
        StringBuilder literal = new StringBuilder(input.Length + 2);
        literal.Append("\"");
        foreach (var c in input)
        {
            switch (c)
            {
                case '\"': literal.Append("\\\""); break;
                case '\\': literal.Append(@"\\"); break;
                case '\0': literal.Append(@"\0"); break;
                case '\a': literal.Append(@"\a"); break;
                case '\b': literal.Append(@"\b"); break;
                case '\f': literal.Append(@"\f"); break;
                case '\n': literal.Append(@"\n"); break;
                case '\r': literal.Append(@"\r"); break;
                case '\t': literal.Append(@"\t"); break;
                case '\v': literal.Append(@"\v"); break;
                default:
                    // ASCII printable character
                    if (c >= 0x20 && c <= 0x7e)
                    {
                        literal.Append(c);
                        // As UTF16 escaped character
                    }
                    else
                    {
                        literal.Append(@"\u");
                        literal.Append(((int)c).ToString("x4"));
                    }
                    break;
            }
        }
        literal.Append("\"");
        return literal.ToString();
    }

    public static uint Crc16Modbus(Span<byte> modbusData)
    {
        uint num = 65535u;
        for (int i = 0; i < modbusData.Length; i++)
        {
            num ^= modbusData[i];
            for (uint num3 = 0u; num3 < 8; num3++)
            {
                num = (((num & 1) != 1) ? (num >> 1) : ((num >> 1) ^ 0xA001u));
            }
        }

        return num;
    }

    public void LogIfNotAlive()
    {
        var limit = DateTime.UtcNow.AddMinutes(-3);

        if (LastUpdateTime != default && LastUpdateTime < limit)
        {
            Program.Log("Growatt data not received since: " + LastUpdateTime);
            LastUpdateTime = default;
        }
    }
}

ref struct Growatt5104
{
    private readonly Span<byte> _body;

    public string datalogserial => Encoding.ASCII.GetString(_body.Slice(0, 10));
    public string pvserial => Encoding.ASCII.GetString(_body.Slice(30, 10));
    public DateTime date
    {
        get
        {
            var bytes = _body.Slice(60);
            long l = MemoryMarshal.Cast<byte, long>(bytes)[0] & 0xffffffffffffL;
            if (l == 0)
            {
                return default;
            }

            return new DateTime(2000 + bytes[0], bytes[1], bytes[2], bytes[3], bytes[4], bytes[5]);
        }
    }

    public ushort recortype1 => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(67));
    public ushort recortype2 => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(69));
    public ushort pvstatus => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(71));
    public uint pvpowerin => BinaryPrimitives.ReadUInt32BigEndian(_body.Slice(73));
    public ushort pv1voltage => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(77));
    public ushort pv1current => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(79));
    public uint pv1watt => BinaryPrimitives.ReadUInt32BigEndian(_body.Slice(81));
    public ushort pv2voltage => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(85));
    public ushort pv2current => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(87));
    public uint pv2watt => BinaryPrimitives.ReadUInt32BigEndian(_body.Slice(89));
    public uint pvpowerout => BinaryPrimitives.ReadUInt32BigEndian(_body.Slice(93));
    public ushort pvfrequentie => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(97));
    public ushort pvgridvoltage => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(99));
    public ushort pvgridcurrent => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(101));
    public uint pvgridpower => BinaryPrimitives.ReadUInt32BigEndian(_body.Slice(103));
    public ushort pvgridvoltage2 => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(107));
    public ushort pvgridcurrent2 => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(109));
    public uint pvgridpower2 => BinaryPrimitives.ReadUInt32BigEndian(_body.Slice(111));
    public ushort pvgridvoltage3 => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(115));
    public ushort pvgridcurrent3 => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(117));
    public uint pvgridpower3 => BinaryPrimitives.ReadUInt32BigEndian(_body.Slice(119));
    public uint pvenergytoday => BinaryPrimitives.ReadUInt32BigEndian(_body.Slice(123));
    public uint pvenergytotal => BinaryPrimitives.ReadUInt32BigEndian(_body.Slice(127));
    public uint totworktime => BinaryPrimitives.ReadUInt32BigEndian(_body.Slice(131));
    public ushort pvtemperature => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(135));
    public ushort isof => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(137));
    public ushort gfcif => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(139));
    public ushort dcif => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(141));
    public ushort vpvfault => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(143));
    public ushort vacfault => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(145));
    public ushort facfault => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(147));
    public ushort tmpfault => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(149));
    public ushort faultcode => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(151));
    public ushort pvipmtemperature => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(153));
    public ushort pbusvolt => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(155));
    public ushort nbusvolt => BinaryPrimitives.ReadUInt16BigEndian(_body.Slice(157));
    public uint epv1today => BinaryPrimitives.ReadUInt32BigEndian(_body.Slice(171));
    public uint epv1total => BinaryPrimitives.ReadUInt32BigEndian(_body.Slice(175));
    public uint epv2today => BinaryPrimitives.ReadUInt32BigEndian(_body.Slice(179));
    public uint epv2total => BinaryPrimitives.ReadUInt32BigEndian(_body.Slice(183));
    public uint epvtotal => BinaryPrimitives.ReadUInt32BigEndian(_body.Slice(187));

    public Growatt5104(Span<byte> body)
    {
        _body = body;
    }
}