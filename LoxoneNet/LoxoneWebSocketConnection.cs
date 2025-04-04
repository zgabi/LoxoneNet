using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Formats.Asn1;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using LoxoneNet.Loxone;
using LoxoneNet.Loxone.Converters;
using Microsoft.Extensions.Logging;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace LoxoneNet;

public class LoxoneWebSocketConnection
{
    private readonly string _url;
    private readonly IPAddress _ipAddress;
    private readonly string _userName;
    private readonly string _password;
    private readonly byte[] _serviceAccountCredentialData;

    private const int KeepAliveInterval = 3000;

    private readonly Random _random = new Random();
    private readonly byte[] _buf = new byte[1024 * 1024];

    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);
    
    private readonly ConcurrentQueue<ReadOnlyMemory<byte>> _commands = new ConcurrentQueue<ReadOnlyMemory<byte>>();
    
    private Thread? _thread;
    private LoxAPP3 _loxApp = null!;

    public LoxoneWebSocketConnection(string url, string userName, string password, byte[] serviceAccountCredentialData)
    {
        _url = url;
        
        var addresses = Dns.GetHostAddresses(url);
        _ipAddress = addresses[0];
        _userName = userName;
        _password = password;
        _serviceAccountCredentialData = serviceAccountCredentialData;
    }

    public async Task<RSA> GetPublicKey()
    {
        var httpClient = new HttpClient();
            string response = await httpClient.GetStringAsync($"http://{_url}/jdev/sys/getPublicKey");

        var res = JsonSerializer.Deserialize<LoxoneResponse>(response)!.LL.value;
        string base64 = res.GetString()!;
        if (base64.StartsWith("-----BEGIN CERTIFICATE-----"))
        {
            base64 = base64.Substring(27);
        }

        if (base64.EndsWith("-----END CERTIFICATE-----"))
        {
            base64 = base64.Substring(0, base64.Length - 25);
        }

        var asn = new AsnReader(Convert.FromBase64String(base64), AsnEncodingRules.DER);
        asn = asn.ReadSequence();
        asn.ReadSequence();
        //string objectIdentifier = asn.ReadObjectIdentifier(Asn1Tag.ObjectIdentifier);
        //asn.ReadNull();
        var data2 = asn.ReadBitString(out int bitCount);
        asn = new AsnReader(data2, AsnEncodingRules.DER);
        asn = asn.ReadSequence();
        var modulus = asn.ReadInteger();
        var exponent = asn.ReadInteger();

        return RSA.Create(new RSAParameters
        {
            Modulus = modulus.ToByteArray(true, true),
            Exponent = exponent.ToByteArray(true, true),
        });
    }

    public async Task<LoxoneMessageHeader> ReceiveHeaderAsync(ClientWebSocket ws)
    {
        var buf = _buf;
        var res = await ws.ReceiveAsync(buf, CancellationToken.None);

        var header = new LoxoneMessageHeader(buf.AsSpan(0, res.Count));

        if ((header.Flags & 0x80) == 0x80)
        {
            res = await ws.ReceiveAsync(buf, CancellationToken.None);
            header = new LoxoneMessageHeader(buf.AsSpan(0, res.Count));
        }

        return header;
    }

    public async Task<ReadOnlyMemory<byte>> ReceiveMessage(ClientWebSocket ws, int expectedLength)
    {
        var buf = _buf;
        var res = await ws.ReceiveAsync(buf, CancellationToken.None);

        Debug.Assert(res.Count == expectedLength);
        return buf.AsMemory(0, res.Count);
    }

    public async Task<ReadOnlyMemory<byte>> DownloadFile(ClientWebSocket ws, string cmd)
    {
        await ws.SendAsync(Encoding.ASCII.GetBytes(cmd), WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, CancellationToken.None);

        LoxoneMessageHeader header;
        while (true)
        {
            header = await ReceiveHeaderAsync(ws);
            if (header.Identifier == LoxoneMessageType.BinaryFile)
            {
                break;
            }

            await ProcessEvent(ws, header);
        }

        return await ReceiveMessage(ws, header.Length);
    }

    private ReadOnlyMemory<byte> EncryptCommand(ReadOnlyMemory<byte> cmd, Aes aes)
    {
        int length = cmd.Length + 10; // length of 'salt/{4 byte salt}/'
        Span<byte> span = stackalloc byte[length];

        Encoding.ASCII.GetBytes("salt/0000/", span);
        Utf8Formatter.TryFormat((uint)_random.Next(), span.Slice(5, 4), out _, new StandardFormat('x'));
        cmd.Span.CopyTo(span.Slice(10));

        int maxLength = (length + 2) / 3 * 4 + 15; // max length of base64 encoded span + maximum 15 bytes padding
        Span<byte> spanEnc = stackalloc byte[maxLength];
        length = aes.EncryptCbc(span, aes.IV, spanEnc, PaddingMode.Zeros);
        Base64.EncodeToUtf8InPlace(spanEnc, length, out int base64Length);

        int toReplace = 0;
        for (int i = 0; i < base64Length; i++)
        {
            byte b = spanEnc[i];
            if (b == '+' || b == '/')
            {
                toReplace++;
            }
        }

        var encrypted = new byte[14 + base64Length + toReplace * 2];
        Encoding.ASCII.GetBytes("jdev/sys/fenc/", encrypted);
        int pos = 14;
        for (int i = 0; i < base64Length; i++)
        {
            byte b = spanEnc[i];
            if (b == '+' || b == '/')
            {
                encrypted[pos++] = (byte)'%';
                encrypted[pos++] = (byte)'2';
                encrypted[pos++] = b == '+' ? (byte)'B' : (byte)'F';
            }
            else
            {
                encrypted[pos++] = b;
            }
        }

        return encrypted;
    }

    private ReadOnlyMemory<byte> DecryptCommand(ReadOnlyMemory<byte> cmd, Aes aes)
    {
        int length = cmd.Length * 3 / 4;
        Span<byte> data = stackalloc byte[length];
        Base64.DecodeFromUtf8(cmd.Span, data, out int consumed, out int written);
        Debug.Assert(consumed == cmd.Length);

        var data2 = aes.DecryptCbc(data.Slice(0, written), aes.IV, PaddingMode.Zeros);
        return data2.AsMemory().TrimEnd((byte)0);
    }

    private async Task SendCommandAsync(ClientWebSocket ws, ReadOnlyMemory<byte> cmd, Aes? aes = null)
    {
        if (aes != null)
        {
            cmd = EncryptCommand(cmd, aes);
        }

        await ws.SendAsync(cmd, WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, CancellationToken.None);
    }

    private async Task<ReadOnlyMemory<byte>> ReceiveResponseAsync(ClientWebSocket ws, Task<LoxoneMessageHeader>? receiveHeaderTask, Aes? aes = null)
    {
        LoxoneMessageHeader header;
        while (true)
        {
            if (receiveHeaderTask != null)
            {
                header = await receiveHeaderTask;
                receiveHeaderTask = null;
            }
            else
            {
                header = await ReceiveHeaderAsync(ws);
            }

            if (header.Identifier == LoxoneMessageType.TextMessage)
            {
                break;
            }

            await ProcessEvent(ws, header);
        }

        var res = await ReceiveMessage(ws, header.Length);

        if (aes != null)
        {
            res = DecryptCommand(res, aes);
        }

        return res;
    }

    private async Task<LoxoneResponseLL> GetResponseAsync(ClientWebSocket ws, string cmd, Aes? aes = null)
    {
        var data = Encoding.ASCII.GetBytes(cmd);
        await SendCommandAsync(ws, data, aes);
        var res = await ReceiveResponseAsync(ws, null, aes);
        return JsonSerializer.Deserialize<LoxoneResponse>(res.Span)!.LL;
    }

    private ValueTask SendKeepAliveAsync(ClientWebSocket ws)
    {
        return ws.SendAsync(Encoding.ASCII.GetBytes("keepalive"), WebSocketMessageType.Text,
            WebSocketMessageFlags.EndOfMessage, CancellationToken.None);
    }

    public static void ProcessFile(ReadOnlySpan<byte> span)
    {
        var hash = SHA256.HashData(span);
        File.WriteAllBytes(ByteArrayToHex(hash) + ".txt", span.ToArray());
    }

    public void ProcessValueEvents(ReadOnlySpan<byte> span)
    {
        while (span.Length > 0)
        {
            var guid = new Guid(span.Slice(0, 16));
            double value = BinaryPrimitives.ReadDoubleLittleEndian(span.Slice(16, 8));

            string? name = _loxApp.FindGuid(guid, Guid.Empty, value);
            if (name != null)
            {
                Program.Log(LogLevel.Trace, name);
            }

            span = span.Slice(24);
        }
    }

    public void ProcessTextEvents(ReadOnlySpan<byte> span)
    {
        while (span.Length > 0)
        {
            var guid = new Guid(span.Slice(0, 16));
            var guidIcon = new Guid(span.Slice(16, 16));
            int length = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(32, 4));
            string str = Encoding.UTF8.GetString(span.Slice(36, length));

            string? name = _loxApp.FindGuid(guid, guidIcon, str);
            if (name != null)
            {
                Program.Log(LogLevel.Trace, name);
            }

            while (length % 4 != 0)
            {
                length++;
            }

            span = span.Slice(36 + length);
        }
    }

    public static void ProcessDayTimeEvents(ReadOnlySpan<byte> span)
    {
        while (span.Length > 0)
        {
            var guid = new Guid(span.Slice(0, 16));
            double dDefValue = BinaryPrimitives.ReadDoubleLittleEndian(span.Slice(16, 8));
            int nrEntries = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(24, 4));

            span = span.Slice(28);
            for (int i = 0; i < nrEntries; i++)
            {
                int nMode = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(0, 4));
                int nFrom = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(4, 4));
                int nTo = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(8, 4));
                int bNeedActivate = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(12, 4));
                double dValue = BinaryPrimitives.ReadDoubleLittleEndian(span.Slice(16, 8));

                Program.Log(LogLevel.Trace, $"DT event: {guid},{dDefValue}: {nMode},{nFrom},{nTo},{bNeedActivate},{dValue}");
                span = span.Slice(24);
            }
        }
    }

    public static void ProcessWeatherEvents(ReadOnlySpan<byte> span)
    {
        while (span.Length > 0)
        {
            var guid = new Guid(span.Slice(0, 16));
            uint lastUpdate = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(16, 4));
            int nrEntries = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(20, 4));

            span = span.Slice(24);
            for (int i = 0; i < nrEntries; i++)
            {
                int timestamp = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(0, 4));
                int weatherType = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(4, 4));
                int windDirection = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(8, 4));
                int solarRadiation = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(12, 4));
                int relativeHumidity = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(16, 4));
                double temperature = BinaryPrimitives.ReadDoubleLittleEndian(span.Slice(20, 8));
                double perceivedTemperature = BinaryPrimitives.ReadDoubleLittleEndian(span.Slice(28, 8));
                double dewPoint = BinaryPrimitives.ReadDoubleLittleEndian(span.Slice(36, 8));
                double precipitation = BinaryPrimitives.ReadDoubleLittleEndian(span.Slice(44, 8));
                double windSpeed = BinaryPrimitives.ReadDoubleLittleEndian(span.Slice(52, 8));
                double barometicPressure = BinaryPrimitives.ReadDoubleLittleEndian(span.Slice(60, 8));

                Program.Log(LogLevel.Trace, $"weather: {guid},{lastUpdate}: {timestamp},{weatherType},{windDirection},{solarRadiation},{relativeHumidity},{temperature},{perceivedTemperature},{dewPoint},{precipitation},{windSpeed},{barometicPressure}");
                span = span.Slice(68);
            }
        }
    }

    public async Task ProcessEvent(ClientWebSocket ws, LoxoneMessageHeader messageHeader)
    {
        var identifier = messageHeader.Identifier;
        int length = messageHeader.Length;

        if (identifier == LoxoneMessageType.KeepAlive)
        {
            Debug.Assert(length == 0);
            return;
        }

        //Console.WriteLine($"{DateTime.Now} data received ({length} bytes), type: {identifier}");
        var res = await ReceiveMessage(ws, length);

        switch (identifier)
        {
            case LoxoneMessageType.TextMessage:
                Program.Log(LogLevel.Trace, "textmessage:" + Encoding.ASCII.GetString(res.Span));
                break;
            case LoxoneMessageType.BinaryFile:
                ProcessFile(res.Span);
                break;
            case LoxoneMessageType.ValueState:
                ProcessValueEvents(res.Span);
                break;
            case LoxoneMessageType.TextState:
                ProcessTextEvents(res.Span);
                break;
            case LoxoneMessageType.DayTimerState:
                ProcessDayTimeEvents(res.Span);
                break;
            case LoxoneMessageType.OutOfService:
                break;
            case LoxoneMessageType.WeatherState:
                ProcessWeatherEvents(res.Span);
                break;
            default:
                break;
        }

    }

    public void Start()
    {
        _thread = new Thread(ThreadFunc);
        _thread.IsBackground = true;
        _thread.Start();
    }

    private static string HashPassword(string userName, string password, byte[] key, string userSalt, string hashAlg)
    {
        int userNameLength = Encoding.UTF8.GetByteCount(userName);
        int passLength = Encoding.UTF8.GetByteCount(password);
        
        // contains: {password}:{userSalt}
        Span<byte> passwordSpan = stackalloc byte[passLength + userSalt.Length + 1];
        Encoding.UTF8.GetBytes(password, passwordSpan);
        passwordSpan[passLength] = (byte)':';
        Encoding.ASCII.GetBytes(userSalt, passwordSpan.Slice(passLength + 1));

        const int maxHashLength = 32; // max length of the supported hashes in bytes. (SHA1: 20 bytes, SHA256: 32 bytes)

        // contains: {userName}:{passwordHash}
        Span<byte> userNameSpan = stackalloc byte[userNameLength + 2 * maxHashLength + 1];
        Encoding.UTF8.GetBytes(userName, userNameSpan);
        userNameSpan[userNameLength] = (byte)':';

        Span<byte> hashSpan = stackalloc byte[maxHashLength];
        var userNameSpanHash = userNameSpan.Slice(userNameLength + 1);

        int hashLength;
        switch (hashAlg)
        {
            case "SHA1":
                hashLength = SHA1.HashData(passwordSpan, hashSpan);
                ByteArrayToHex(hashSpan.Slice(0, hashLength), userNameSpanHash, true);
                HMACSHA1.HashData(key, userNameSpan.Slice(0, userNameLength + 2 * hashLength + 1), hashSpan);
                break;
            case "SHA256":
                hashLength = SHA256.HashData(passwordSpan, hashSpan);
                ByteArrayToHex(hashSpan.Slice(0, hashLength), userNameSpanHash, true);
                HMACSHA256.HashData(key, userNameSpan.Slice(0, userNameLength + 2 * hashLength + 1), hashSpan);
                break;
            default:
                throw new Exception("HashAlg is not supported:" + hashAlg);
        }

        return ByteArrayToHex(hashSpan.Slice(0, hashLength));
    }

    private async Task<byte[]> GetKeyExchangeAsync(ClientWebSocket ws, RSA rsa, Aes aes)
    {
        var buf = new byte[2 * (aes.Key.Length + aes.IV.Length) + 1];
        ByteArrayToHex(aes.Key, buf);
        buf[aes.Key.Length * 2] = (byte)':';
        ByteArrayToHex(aes.IV, buf.AsSpan(aes.Key.Length * 2 + 1));

        var encData = rsa.Encrypt(buf, RSAEncryptionPadding.Pkcs1);
        var res = await GetResponseAsync(ws, "jdev/sys/keyexchange/" + Convert.ToBase64String(encData));
        
        var data = res.value.GetBytesFromBase64();
        //var dec = aes.DecryptCbc(data, aes.IV, PaddingMode.Zeros);
        //data = HexToByteArray(dec);

        return data;
    }

    private async Task GetJwtAsync(ClientWebSocket ws, Aes aes, string userName, string password)
    {
        var res = await GetResponseAsync(ws, "jdev/sys/getkey2/" + userName);
        var key = HexToByteArray(res.value.GetProperty("key").GetString()!);
        string userSalt = res.value.GetProperty("salt").GetString()!;
        string hashAlg = res.value.GetProperty("hashAlg").GetString()!;

        string hash = HashPassword(userName, password, key, userSalt, hashAlg);

        var guid = Guid.NewGuid();
        res = await GetResponseAsync(ws, $"jdev/sys/getjwt/{hash}/{userName}/4/{guid}/MyDotNetClient", aes);

        if (res.code != "200")
        {
            throw new Exception("Authentication failed");
        }
    }

    private void ParseLoxoneApp(ReadOnlySpan<byte> loxAppBytes)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            Converters = { new GuidConverter(), new GuidAndNameConverter() },
        };

        _loxApp = JsonSerializer.Deserialize<LoxAPP3>(loxAppBytes, options)!;
        //File.WriteAllBytes(@"LoxAPP3_2.json", JsonSerializer.SerializeToUtf8Bytes(_loxApp, options));

        _loxApp.Initialize();
    }

    private async void ThreadFunc()
    {
        while (true)
        {
            try
            {
                await LoxoneLoop();
            }
            catch (Exception ex)
            {
                Program.Log("Loxone exception: " + ex);
                await Task.Delay(5000);
            }
        }
    }

    private async Task LoxoneLoop()
    {
        while (true)
        {
            var rsa = await GetPublicKey();
            var aes = Aes.Create();
            
            var ws = new ClientWebSocket();
            ws.Options.KeepAliveInterval = TimeSpan.FromTicks(0xFFFFFFFEL * 10000);
            //ws.Options.Proxy = new WebProxy(new Uri("http://127.0.0.1:8888"));
            await ws.ConnectAsync(new Uri($"ws://{_url}/ws/rfc6455"), CancellationToken.None);
            await GetKeyExchangeAsync(ws, rsa, aes);
            await GetJwtAsync(ws, aes, _userName, _password);
            
            var loxAppBytes = await DownloadFile(ws, "data/LoxAPP3.json");
            ParseLoxoneApp(loxAppBytes.Span);
            await WebhookProcessor.InitializeDevices(_loxApp, _serviceAccountCredentialData);

            await GetResponseAsync(ws, "jdev/sys/appinitinfo");
            await GetResponseAsync(ws, "jdev/sps/enablebinstatusupdate");
           
            var receiveTask = ReceiveHeaderAsync(ws);
            var sleepTask = Task.Delay(KeepAliveInterval);

            var semaphoreTask = _semaphore.WaitAsync();

            while (ws.State == WebSocketState.Open)
            {
                var finishedTask = await Task.WhenAny(receiveTask, sleepTask, semaphoreTask);
                if (finishedTask == receiveTask)
                {
                    await ProcessEvent(ws, receiveTask.Result);

                    receiveTask = ReceiveHeaderAsync(ws);
                }
                else if (finishedTask == sleepTask)
                {
                    await SendKeepAliveAsync(ws);
                    sleepTask = Task.Delay(KeepAliveInterval);
                }
                else
                {
                    semaphoreTask = _semaphore.WaitAsync();
                    while (_commands.TryDequeue(out var command))
                    {
                        Program.Log("command: " + Encoding.UTF8.GetString(command.Span));
                        await SendCommandAsync(ws, command);
                        var res = await ReceiveResponseAsync(ws, receiveTask);
                        Program.Log("command response: " + Encoding.ASCII.GetString(res.Span).TrimEnd());

                        await Task.Delay(200);

                        receiveTask = ReceiveHeaderAsync(ws);
                    }
                }

                Program.LogIfNotAlive();
            }
        }
    }

    private static string ByteArrayToHex(ReadOnlySpan<byte> data)
    {
        var chs = new char[data.Length * 2];
        for (int i = 0; i < data.Length; i++)
        {
            var b = data[i];
            b.TryFormat(chs.AsSpan(i * 2), out _, "x2");
        }

        return new string(chs);
    }

    private static void ByteArrayToHex(ReadOnlySpan<byte> data, Span<byte> destination, bool upperCase = false)
    {
        char symbol = upperCase ? 'X' : 'x';
        for (int i = 0; i < data.Length; i++)
        {
            var b = data[i];
            Utf8Formatter.TryFormat(b, destination.Slice(i * 2), out _, new StandardFormat(symbol, 2));
        }
    }

    private static byte[] HexToByteArray(string str)
    {
        var result = new byte[str.Length / 2];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = byte.Parse(str.AsSpan(i * 2, 2), NumberStyles.HexNumber);
        }

        return result;
    }

    private static byte[] HexToByteArray(ReadOnlySpan<byte> str)
    {
        var result = new byte[str.Length / 2];
        for (int i = 0; i < result.Length; i++)
        {
            Utf8Parser.TryParse(str.Slice(i * 2, 2), out byte b, out _, 'x');
            result[i] = b;
        }

        return result;
    }

    public void SendCommand(ReadOnlyMemory<byte> command)
    {
        _commands.Enqueue(command);
        _semaphore.Release();
    }

    public void SendCommand(string command)
    {
        var data = Encoding.UTF8.GetBytes(command);
        SendCommand(data);
    }

    public void SendCommandUdp(string command)
    {
        var udp = new UdpClient();
        udp.Connect(new IPEndPoint(_ipAddress, 1235));
        var data = Encoding.ASCII.GetBytes(command);
        udp.Send(data, data.Length);
    }

    public struct LoxoneMessageHeader
    {
        public byte BinType { get; } = 3; // fix 0x03

        public LoxoneMessageType Identifier { get; private set; }
        
        public byte Flags { get; private set; }
        
        public byte Reserved { get; private set; }

        public int Length { get; set; }

        public LoxoneMessageHeader(ReadOnlySpan<byte> headerBytes)
        {
            if (headerBytes.Length != 8)
            {
                throw new Exception("Invalid LoxoneMessageHeader length");
            }

            if (headerBytes[0] != 3)
            {
                throw new Exception("Invalid LoxoneMessageHeader BinType");

            }

            Identifier = (LoxoneMessageType)headerBytes[1];
            Flags = headerBytes[2];
            Reserved = headerBytes[3];
            Length = BinaryPrimitives.ReadInt32LittleEndian(headerBytes.Slice(4));
        }
    }

    public enum LoxoneMessageType : byte
    {
        TextMessage = 0,
        BinaryFile = 1,
        ValueState = 2,
        TextState = 3,
        DayTimerState = 4,
        OutOfService = 5,
        KeepAlive = 6,
        WeatherState = 7
    }
}