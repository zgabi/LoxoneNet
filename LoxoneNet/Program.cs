using System.Diagnostics;
using System.Dynamic;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using LoxoneNet.DSMR;
using LoxoneNet.SmartHome;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Directory = System.IO.Directory;

namespace LoxoneNet;

public class Program
{
    private const string ResourcesFolder = @"Resources";
    private const string AccountPem = @"Resources/letsencrypt_account.pem";
    private const string CertFileName = @"Resources/certificate.crt";
    private const string KeyFileName = @"Resources/private.key";
    private const string LockFile = @"/tmp/loxonelock";

    public static bool Running;
    private static bool _authorized;
    private static P1Reader? _p1Reader;
    private static Growatt.Growatt? _growatt;

    public static LoxoneWebSocketConnection? LoxoneSocket { get; private set; }
    public static Settings Settings { get; private set; }

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

    private static async Task<string> LetsEncryptRegisterAccount(string email)
    {
        var acme = new AcmeContext(WellKnownServers.LetsEncryptV2);
        var account = await acme.NewAccount(email, true);

        var pemKey = acme.AccountKey.ToPem();
        await File.WriteAllTextAsync(AccountPem, pemKey);
        return pemKey;
    }

    public static async Task Main(string[] args)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                File.Delete("/tmp/log.txt.old");
                File.Move("/tmp/log.txt", "/tmp/log.txt.old");
            }
            catch
            {
                // ignore
            }
        }

        var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").AddEnvironmentVariables().Build();
        var settings = config.GetRequiredSection("Settings").Get<Settings>()!;
        Settings = settings;

        Running = true;

        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        Log("starting: " + Console.IsInputRedirected + " pid: " + Process.GetCurrentProcess().Id);

        FileStream? lockFs = null;
        try
        {
            lockFs = new FileStream(LockFile, FileMode.Create, FileAccess.Write, FileShare.None);
        }
        catch (DirectoryNotFoundException)
        {
            // ignore
        }
        catch
        {
            Log("Failed to lock. Another instance running? Exiting.");
            return;
        }

        if (!string.IsNullOrEmpty(settings.DsmrReaderAddress))
        {
            _p1Reader = new P1Reader();
            _p1Reader.Start(settings.DsmrReaderAddress, settings.DsmrReaderPort);
        }

        if (!string.IsNullOrEmpty(settings.GrowattServerHost))
        {
            _growatt = new Growatt.Growatt();
            _growatt.Start(settings.GrowattServerHost);
        }

        try
        {
            try
            {
                Directory.CreateDirectory(ResourcesFolder);
            }
            catch
            {
            }

            if (!File.Exists(AccountPem))
            {
                var acc = await LetsEncryptRegisterAccount(settings.LetsEncryptEmail);
            }

            //var req = new HttpRequestMessage(HttpMethod.Get, LetsEncryptV2);

            //var headers = new WebHeaderCollection();
            //headers.Set("User-Agent", "Kincony");

            //req.Headers.Add("User-Agent", "KinconyClient/0.0.1");

            //var http = new HttpClient();
            //try
            //{
            //    var resp = http.Send(req);
            //}
            //catch (Exception ex)
            //{
            //    ;
            //}

            var pemKey = await File.ReadAllTextAsync(AccountPem);
            var accountKey = KeyFactory.FromPem(pemKey);
            var acme = new AcmeContext(WellKnownServers.LetsEncryptV2, accountKey);

            while (true)
            {
                try
                {
                    var account = await acme.Account();
                    break;
                }
                catch
                {
                }
            }

            //var serial = new SerialPort("COM4");
            //serial.Open();

            //var stream = serial.BaseStream;
            //stream.Write(Encoding.ASCII.GetBytes("RELAY-SET-255,1,1"));


            //var h = new HttpClient();
            //var resp = await h.PostAsync("http://mqtt:123@192.168.0.111/relay32/a7c6d04b8a5a21ae54def363/set",
            //    new StringContent("{\"relay2\":{\"on\":1}}"));



            //var c = new TcpClient();
            //c.Connect("192.168.0.111", 4196);

            //var stream = c.GetStream();

            //var data = new byte[] { 0x01, 0x05, 0x00, 0x00, 0xFF, 0x00, 0x8C, 0x3A };

            //var x = crc16_modus(data, 6);

            ////var data = new byte[]
            ////{
            ////    0x00, 0x01, // Transaction Identifier
            ////    0x00, 0x00, // Protocol Identifier
            ////    0x00, 0x06, // Message Length
            ////    0x01, // Unit Identifier
            ////    0x05, // Function Code
            ////    0x00, 0x00, // register address
            ////    0xFF, 0x00 // write data FF00-ON, 0000-OFF
            ////};
            ////stream.Write(data);

            bool shouldRenewCert = false;
            bool certFound = File.Exists(CertFileName) && File.Exists(KeyFileName);

            X509Certificate2 cert = null;
            if (certFound)
            {
                if (File.GetLastWriteTimeUtc(CertFileName) < DateTime.UtcNow.AddMonths(-1))
                {
                    shouldRenewCert = true;
                }

                string certPem = await File.ReadAllTextAsync(CertFileName);
                string keyPem = await File.ReadAllTextAsync(KeyFileName);

                cert = X509Certificate2.CreateFromPem(certPem);

                var rsa = RSA.Create();
                rsa.ImportFromPem(keyPem);
                cert = new X509Certificate2(cert.CopyWithPrivateKey(rsa).Export(X509ContentType.Pkcs12));
            }
            else
            {
                Log("Cert not found.");
                shouldRenewCert = true;
            }

            byte[] serviceAccountCredentialData = await File.ReadAllBytesAsync(@"Resources/service-account.json");

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Logging.AddDebug();
            builder.Logging.SetMinimumLevel(LogLevel.Trace);
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(settings.HttpsPort, listenOptions =>
                {
                    listenOptions.UseHttps(httpsOptions =>
                    {
                        httpsOptions.ServerCertificateSelector = (context, dnsName) =>
                        {
                            return cert;
                        };
                    });
                });

                if (settings.HttpPort != 0)
                {
                    options.Listen(IPAddress.Any, settings.HttpPort, listenOptions => { });
                }
            });

            var app = builder.Build();
            app.Use(async (HttpContext context, Func<Task> func) =>
            {
                try
                {
                    await ProcessRequest(context);
                }
                catch (Exception ex)
                {
                    Log("Process request failed. " + ex);
                }
            });
            
            await app.StartAsync().ConfigureAwait(false);
            Log("Web server started");

            if (shouldRenewCert)
            {
                Log("Renewing Let's Encrypt certificates.");
                
                var order = await acme.NewOrder(new[] { settings.Hostname });
                var authz = (await order.Authorizations()).First();
                var httpChallenge = await authz.Http();
                var keyAuthz = httpChallenge.KeyAuthz;
                _acmePath = "/.well-known/acme-challenge/" + httpChallenge.Token;
                _acmeAuth = keyAuthz;

                var challange = await httpChallenge.Validate();
                if (challange.Error != null)
                {
                    Log("Failed to validate the Let's Encrypt certificate challenge. " + challange.Error.Detail);
                }

                Log($"Waiting for ACME challenge. path: {_acmePath}, auth: {_acmeAuth}");
                while (!_authorized || ((await order.Resource()).Status != OrderStatus.Ready && (await order.Resource()).Status != OrderStatus.Pending))
                {
                    if (_authorized)
                    {
                        var order1 = await order.Resource();
                        if (order1.Status == OrderStatus.Ready || order1.Status == OrderStatus.Pending)
                        {
                            break;
                        }
                    }

                    await Task.Delay(100);
                }
         
                Log("ACME challenge received. " + challange.Status);

                var privateKey = KeyFactory.NewKey(KeyAlgorithm.RS256);
                var cert1 = await order.Generate(new CsrInfo
                {
                    CountryName = settings.CertCountry,
                    State = settings.CertState,
                    Locality = settings.CertLocality,
                    Organization = settings.CertOrganization,
                    OrganizationUnit = settings.CertOrganizationUnit,
                    CommonName = settings.Hostname,
                }, privateKey);

                string certPem = cert1.Certificate.ToPem();
                string keyPem = privateKey.ToPem();
                await File.WriteAllTextAsync(CertFileName, certPem);
                await File.WriteAllTextAsync(KeyFileName, keyPem);

                cert = X509Certificate2.CreateFromPem(certPem);

                var rsa = RSA.Create();
                rsa.ImportFromPem(keyPem);
                cert = new X509Certificate2(cert.CopyWithPrivateKey(rsa).Export(X509ContentType.Pkcs12));
            }

            Log("starting Loxone connection");
            var loxoneWs = new LoxoneWebSocketConnection(settings.LoxoneUrl, settings.LoxoneUserName, settings.LoxonePassword, serviceAccountCredentialData);
            loxoneWs.Start();
            LoxoneSocket = loxoneWs;

            Log("running");

            await app.WaitForShutdownAsync(CancellationToken.None).ConfigureAwait(false);

            Log("exiting");

            if (lockFs != null)
            {
                await lockFs.DisposeAsync();
            }

            File.Delete(LockFile);
        }
        catch (Exception ex)
        {
            Log("Fatal error: " + ex);
        }
    }

    private static string? _acmePath;
    private static string? _acmeAuth;

    private static async Task ProcessRequest(HttpContext context)
    {
        var request = context.Request;
        string? path = request.Path.Value;
        Log("accepted: " + context.Connection.RemoteIpAddress + ":" + context.Connection.LocalPort + " " + path);
        if (path == null)
        {
            return;
        }

        var response = context.Response;
        if (path == _acmePath)
        {
            response.ContentType = "text/plain";
            await response.WriteAsync(_acmeAuth);
            _authorized = true;
            return;
        }

        if (path.StartsWith("/spooky/"))
        {
            string spooky = path.Substring(8);
            spooky = HttpUtility.UrlDecode(spooky);
            Log("SPOOKY: " + spooky);
            return;
        }

        if (path.StartsWith("/loglevel/"))
        {
            var logLevel = (LogLevel)int.Parse(path.Substring(10));
            Settings.LogLevel = logLevel;
            Log("Set loglevel to: " + logLevel);
            return;
        }

        if (path.StartsWith("/dumploxone"))
        {
            foreach (var device in WebhookProcessor.Devices)
            {
                Log("Device: " + device.DeviceName);
                foreach (var pair in device.States)
                {
                    Log($"{pair.Key}: {pair.Value}");
                }
            }

            {
                var device = WebhookProcessor.UnknownDevice;
                if (device != null)
                {
                    Log("Device: " + device.DeviceName);
                    foreach (var pair in device.States)
                    {
                        Log($"{pair.Key}: {pair.Value}");
                    }
                }
            }

            return;
        }

        //            if (path.EndsWith("txt"))
        //            {
        //                response.ContentType = "text/plain";

        //                var dataStr = @"556595C23F43B35D2AE3F6842553A2EB703FBF9746BECFFD0DD3ACF9A4853F80
        //comodoca.com
        //857caca2a10209b";
        //                var data = Encoding.ASCII.GetBytes(dataStr);
        //                await response.WriteAsync(dataStr);
        //                return;
        //            }

        if (path == "/webhook")
        {
            if (!CheckAuthorization(context.Request.Headers.Authorization))
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                await response.CompleteAsync();
                return;
            }

            var sr = new StreamReader(request.Body);
            var body = await sr.ReadToEndAsync();
            var req = JsonSerializer.Deserialize<WebhookRequest>(body)!;

            Log(LogLevel.Debug, "webook request: " + body);

            var response1 = WebhookProcessor.ProcessWebhookRequest(req, Settings);
            response.ContentType = "application/json";

            string json = JsonSerializer.Serialize(response1, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            Log(LogLevel.Debug, "webhook response: " + json);
            
            await response.WriteAsync(json);
            return;
        }

        if (path == "/auth")
        {
            var sr = new StreamReader(request.Body);
            var body = await sr.ReadToEndAsync();

            string clientId = request.Query["client_id"].Single();
            if (string.IsNullOrEmpty(Settings.GoogleClientId) || Settings.GoogleClientId == clientId)
            {
                string redirectUri = request.Query["redirect_uri"].Single();
                string state = request.Query["state"].SingleOrDefault();
                string responseUrl = $"{redirectUri}?code={Settings.GoogleOAuthCode}&state={state}";
                
                Log(LogLevel.Debug, "auth redirect: " + responseUrl);

                response.Redirect(responseUrl);
                return;
            }

            Log(LogLevel.Debug, "auth clientId mismatch. received: " + clientId);
        }

        if (path == "/token")
        {
            string code = request.Form["code"].Single();
            string clientId = request.Form["client_id"].Single();
            string clientSecret = request.Form["client_secret"].Single();

            if (code == Settings.GoogleOAuthCode
                && (string.IsNullOrEmpty(Settings.GoogleClientId) || Settings.GoogleClientId == clientId)
                && (string.IsNullOrEmpty(Settings.GoogleClientSecret) || Settings.GoogleClientSecret == clientSecret))
            {
                string grantType = request.Form["grant_type"].Single();
                const int secondsInDay = 86400;

                TokenResponse response1;
                if (grantType == "authorization_code")
                {
                    response1 = new TokenResponse
                    {
                        token_type = "bearer",
                        access_token = Settings.GoogleAccessToken,
                        refresh_token = Settings.GoogleRefreshToken,
                        expires_in = secondsInDay,
                    };
                }
                else
                {
                    response1 = new TokenResponse
                    {
                        token_type = "bearer",
                        access_token = Settings.GoogleAccessToken,
                        expires_in = secondsInDay,
                    };
                }

                response.ContentType = "application/json";

                string json = JsonSerializer.Serialize(response1, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                Log(LogLevel.Debug, $"token response: {json}");

                await response.WriteAsync(json);
                return;
            }
            
            Log(LogLevel.Debug, $"token error. code: {code}, clientId: {clientId}, clientSecret: {clientSecret}");
        }

        response.StatusCode = 404;
        await response.WriteAsync("");
    }

    public static void Log(LogLevel requiredLogLevel, string message)
    {
        if (Settings.LogLevel <= requiredLogLevel)
        {
            Log(message);
        }
    }

    private static bool CheckAuthorization(StringValues values)
    {
        if (values.Count == 0)
        {
            return false;
        }
         
        var auth = values[0].AsSpan();
        if (!auth.StartsWith("Bearer "))
        {
            return false;
        }

        auth = auth.Slice(7);
        return auth.StartsWith(Settings.GoogleAccessToken);
    }

    private static string _toLog = string.Empty;

    public static void Log(string message)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                _toLog += $"{DateTime.Now}: {message}{Environment.NewLine}";
                File.AppendAllText("/tmp/log.txt", _toLog);
                _toLog = string.Empty;
            }
            catch
            {
            }
        }

        try
        {
            Console.WriteLine($"{DateTime.Now}: {message}");
        }
        catch { }
    }

    public static void LogIfNotAlive()
    {
        _p1Reader?.LogIfNotAlive();
        _growatt?.LogIfNotAlive();
    }
}
