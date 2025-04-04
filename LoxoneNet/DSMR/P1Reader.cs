using System.Buffers.Text;
using System.Net.Sockets;

namespace LoxoneNet.DSMR;

internal class P1Reader
{
    private Thread _thread;
    private bool _running;
    private string _hostName;
    private ushort _port;

    public DateTime LastUpdateTime { get; private set; }

    public void Start(string hostName, ushort port)
    {
        _hostName = hostName;
        _port = port;
        _running = true;
        _thread = new Thread(ThreadFunc);
        _thread.IsBackground = true;
        _thread.Start();
    }

    private void Stop()
    {
        _running = false;
    }

    private static uint Crc16(Span<byte> data)
    {
        uint num = 0;
        for (int i = 0; i < data.Length; i++)
        {
            num ^= data[i];
            for (uint j = 0u; j < 8; j++)
            {
                num = (num & 1) != 1 ? num >> 1 : num >> 1 ^ 0xA001u;
            }
        }

        return num;
    }

    private void ThreadFunc()
    {
        Program.Log($"P1 reader started. Address: {_hostName}:{_port}");

        int count = 0;
        var buffer = new byte[65536];
        var data = new byte[65536];
        int dataPos = 0;
        while (_running)
        {
            try
            {
                var client = new TcpClient(_hostName, _port);
                Program.Log("P1 reader connected");
                var stream = client.GetStream();

                double import = 0;
                double export = 0;
                double importP = 0;
                double exportP = 0;
                while (client.Connected && _running)
                {
                    int read = stream.Read(buffer, count, buffer.Length - count);
                    if (read == 0)
                    {
                        break;
                    }

                    count += read;

                    var span = buffer.AsSpan(0, count);

                    while (true)
                    {
                        var idx = span.IndexOf((byte)'\n');
                        if (idx == -1)
                        {
                            break;
                        }

                        var line = span.Slice(0, idx + 1);
                        if (line[0] == '!')
                        {
                            bool crcOk = false;
                            if (line.Length == 7)
                            {
                                data[dataPos++] = (byte)'!';
                                var dataSpan = data.AsSpan(0, dataPos);
                                uint crcExpected = Crc16(dataSpan);
                                if (Utf8Parser.TryParse(span.Slice(1, 4), out int crc, out _, 'X'))
                                {
                                    crcOk = crc == crcExpected;
                                }
                            }

                            if (crcOk)
                            {
                                var loxone = Program.LoxoneSocket;
                                if (loxone != null)
                                {
                                    double p = importP - exportP;
                                    if (LastUpdateTime == default)
                                    {
                                        Program.Log($"Send P1 data to loxone: {import} {export}, {p}");
                                    }

                                    loxone.SendCommandUdp("GridImport" + import);
                                    loxone.SendCommandUdp("GridExport" + export);
                                    loxone.SendCommandUdp("GridPower" + p);
                                    LastUpdateTime = DateTime.UtcNow;
                                }
                            }

                            dataPos = 0;
                        }
                        else
                        {
                            span.Slice(0, idx + 1).CopyTo(data.AsSpan(dataPos));
                            dataPos += idx + 1;
                        }

                        span = span.Slice(idx + 1);

                        // 1-0:1.8.0(000000.000*kWh)
                        if (line.StartsWith("1-0:1.8.0("u8) && line.EndsWith("*kWh)\r\n"u8))
                        {
                            var numStr = line.Slice(10, line.Length - 10 - 7);
                            Utf8Parser.TryParse(numStr, out import, out _);
                        }

                        // 1-0:2.8.0(000000.000*kWh)
                        if (line.StartsWith("1-0:2.8.0("u8) && line.EndsWith("*kWh)\r\n"u8))
                        {
                            var numStr = line.Slice(10, line.Length - 10 - 7);
                            Utf8Parser.TryParse(numStr, out export, out _);
                        }

                        // 1-0:1.7.0(00.000*kW)
                        if (line.StartsWith("1-0:1.7.0("u8) && line.EndsWith("*kW)\r\n"u8))
                        {
                            var numStr = line.Slice(10, line.Length - 10 - 6);
                            Utf8Parser.TryParse(numStr, out importP, out _);
                        }

                        // 1-0:2.7.0(00.000*kW)
                        if (line.StartsWith("1-0:2.7.0("u8) && line.EndsWith("*kW)\r\n"u8))
                        {
                            var numStr = line.Slice(10, line.Length - 10 - 6);
                            Utf8Parser.TryParse(numStr, out exportP, out _);
                        }

                        //Console.Write(Encoding.ASCII.GetString(line));
                    }

                    int remaining = span.Length;
                    if (remaining > 0)
                    {
                        Buffer.BlockCopy(buffer, count - remaining, buffer, 0, remaining);
                        count = remaining;
                    }
                    else
                    {
                        count = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log("P1 exception: " + ex);
            }
    
            Thread.Sleep(1000);
        }
    }

    public void LogIfNotAlive()
    {
        var limit = DateTime.UtcNow.AddMinutes(-1);

        if (LastUpdateTime != default && LastUpdateTime < limit)
        {
            Program.Log("P1 data not received since: " + LastUpdateTime);
            LastUpdateTime = default;
        }
    }
}