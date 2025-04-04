using System.Dynamic;
using System.Net.Http.Headers;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using LoxoneNet.Loxone;
using LoxoneNet.SmartHome;
using Microsoft.Extensions.Logging;

namespace LoxoneNet;

internal class WebhookProcessor
{
    private const string AgentUserId = "1836.15267389";

    public static ServiceAccountCredential TokenSource = null!;
    public static bool Initialized;
    public static readonly DeviceList Devices = new DeviceList();
    public static LoxoneDevice? UnknownDevice;

    public static LoxoneWebSocketConnection LoxoneSocket => Program.LoxoneSocket;

    private static readonly object OfflineState = new
    {
        online = false,
        status = "SUCCESS",
    };

    public static WebhookResponse? ProcessWebhookRequest(WebhookRequest req, Settings settings)
    {
        var input = req.inputs[0];
        switch (input.intent)
        {
            case "action.devices.SYNC":
                return ProcessSync(req.requestId);
            case "action.devices.QUERY":
                return ProcessQuery(req);
            case "action.devices.EXECUTE":
                return ProcessExecute(req);
        }

        return null;
    }

    private static WebhookResponse ProcessExecute(WebhookRequest req)
    {
        var reqPayload = req.inputs[0].payload.Deserialize<ExecuteRequestPayload>()!;

        var response = new WebhookResponse();
        response.requestId = req.requestId;

        var payload = new ExecuteResponsePayload();
        response.payload = payload;

        var input = req.inputs[0];

        var commands = new List<ExecuteResponseCommand>();

        foreach (var reqCommand in reqPayload.commands)
        {
            bool? on = null;
            int? brightness = null;
            int? openPercent = null;
            foreach (var execution in reqCommand.execution)
            {
                if (execution.command == "action.devices.commands.OnOff")
                {
                    bool b = execution.@params.GetProperty("on").GetBoolean();
                    on = b;
                }
                else if (execution.command == "action.devices.commands.BrightnessAbsolute")
                {
                    brightness = execution.@params.GetProperty("brightness").GetInt32();
                }
                else if (execution.command == "action.devices.commands.OpenClose")
                {
                    int percent = execution.@params.GetProperty("openPercent").GetInt32();
                    openPercent = percent;
                }
                else if (execution.command == "action.devices.commands.ThermostatTemperatureSetpoint")
                {
                    double percent = execution.@params.GetProperty("thermostatTemperatureSetpoint").GetDouble();
                    Program.Log("SET TEMP TO: " + percent);
                }
            }

            if (on != null)
            {
                foreach (var reqDevice in reqCommand.devices)
                {
                    string deviceId = reqDevice.id;

                    if (Devices.TryGet(deviceId, out var light))
                    {
                        LoxoneSocket.SendCommand(light!.Command + (on.Value ? "on" : "off"));
                    }

                    commands.Add(new ExecuteResponseCommand
                    {
                        ids = new[]
                        {
                            deviceId
                        },
                        status = "SUCCESS",
                        states = new
                        {
                            on = on, 
                            online = true,
                        }
                    });
                }
            }

            if (brightness != null)
            {
                foreach (var reqDevice in reqCommand.devices)
                {
                    string deviceId = reqDevice.id;

                    if (Devices.TryGet(deviceId, out var device))
                    {
                        LoxoneSocket.SendCommand(device!.Command + brightness);
                    }

                    commands.Add(new ExecuteResponseCommand
                    {
                        ids = new[]
                        {
                            deviceId
                        },
                        status = "SUCCESS",
                        states = new
                        {
                            brightness = brightness.Value, 
                            online = true,
                        }
                    });
                }
            }

            if (openPercent != null)
            {
                foreach (var reqDevice in reqCommand.devices)
                {
                    string deviceId = reqDevice.id;

                    if (Devices.TryGet(deviceId, out var shutter))
                    {
                        LoxoneSocket.SendCommand(shutter!.Command + (100 - openPercent));
                    }

                    commands.Add(new ExecuteResponseCommand
                    {
                        ids = new[]
                        {
                            deviceId
                        },
                        status = "SUCCESS",
                        states = new
                        {
                            openPercent = openPercent, 
                            online = true,
                        }
                    });
                }
            }
        }

        //payload.commands = new[]
        //{
        //    new ExecuteResponseCommand
        //    {
        //        ids = new[]
        //        {
        //            "123"
        //        },
        //        status = "SUCCESS",
        //        states = new States
        //        {
        //            //on = true, 
        //            online = true,
        //        }
        //    },
        //    new ExecuteResponseCommand
        //    {
        //        ids = new[]
        //        {
        //            "456"
        //        },
        //        status = "ERROR",
        //        errorCode = "deviceTurnedOff",
        //    },
        //};

        payload.commands = commands.ToArray();

        return response;
    }

    private static WebhookResponse ProcessQuery(WebhookRequest req)
    {
        var reqPayload = req.inputs[0].payload.Deserialize<QueryRequestPayload>()!;

        var response = new WebhookResponse();
        response.requestId = req.requestId;

        var payload = new QueryResponsePayload
        {
            devices = new Dictionary<string, object>()
        };

        foreach (var device in reqPayload.devices)
        {
            object state = OfflineState;

            if (Devices.TryGet(device.id, out var dev))
            {
                switch (dev!.DeviceType)
                {
                    case DeviceType.Light:
                    {
                        if (dev.States.TryGetValue("active", out object? valObj))
                        {
                            int value = (int)(double)(valObj ?? 0d);
                            state = new
                            {
                                online = true,
                                status = "SUCCESS",
                                on = value == 1,
                            };
                        }
                    }
                    break;
                    case DeviceType.Led:
                    {
                        if (dev.States.TryGetValue("position", out object? valObj))
                        {
                            double value = (double)valObj;
                            Program.Log(LogLevel.Trace, "QUERY led: " + dev.Id + " " + value);
                            state = new
                            {
                                online = true,
                                status = "SUCCESS",
                                on = value > 0,
                                brightness = (int)(value),
                            };
                        }
                    }
                    break;
                    case DeviceType.Shutter:
                    {
                        if (dev.States.TryGetValue("position", out object? valObj))
                        {
                            double value = (double)valObj;
                            state = new
                            {
                                online = true,
                                status = "SUCCESS",
                                openPercent = (int)(100 - value * 100),
                            };
                        }
                    }
                    break;
                    case DeviceType.Thermostat:
                    {
                        if (dev.States.TryGetValue("value", out object? valObj))
                        {
                            double value = (double)valObj;
                            state = new
                            {
                                online = true,
                                status = "SUCCESS",
                                activeThermostatMode = "heatcool",
                                thermostatMode = "heatcool",
                                thermostatTemperatureSetpoint = value,
                                thermostatTemperatureAmbient = 25.1
                            };
                        }
                    }
                    break;
                }
            }

            payload.devices[device.id] = state;
        }

        //var state2 = new
        //{
        //    online = true,
        //    status = "SUCCESS",
        //    on = true,
        //    brightness = 80,
        //    color = new
        //    {
        //        name = "cerulean",
        //        spectrumRGB = 31655,
        //    },
        //};

        //payload.devices.Add("456", state2);

        response.payload = payload;

        return response;
    }

    private static WebhookResponse ProcessSync(string requestId)
    {
        var payload = new SyncResponsePayload
        {
            agentUserId = AgentUserId,
            //devices = new[]
            //{
            //    new Device
            //    {
            //        id = "KITCHEN_SOCKET",
            //        type = "action.devices.types.OUTLET",
            //        traits = new[]
            //        {
            //            "action.devices.traits.OnOff",
            //        },
            //        name = new Name
            //        {
            //            defaultNames = new[]
            //            {
            //                "My Outlet 1234",
            //            },
            //            name = "Night light",
            //            nicknames = new[]
            //            {
            //                "wall plug"
            //            }
            //        },
            //        willReportState = false,
            //        roomHint = "kitchen",
            //        deviceInfo = new DeviceInfo
            //        {
            //            manufacturer = "lights-out-inc",
            //            model = "hs1234",
            //            hwVersion = "3.2",
            //            swVersion = "11.4",
            //        },
            //        otherDeviceIds = new[]
            //        {
            //            new AlternateDeviceId
            //            {
            //                deviceId = "local-device-id",
            //            }
            //        },
            //        customData = new
            //        {
            //            fooValue = 74,
            //            barValue = true,
            //            bazValue = "foo",
            //        },
            //    },
            //    new Device
            //    {
            //        id = "456",
            //        type = "action.devices.types.LIGHT",
            //        traits = new[]
            //        {
            //            "action.devices.traits.OnOff",
            //            "action.devices.traits.Brightness",
            //            "action.devices.traits.ColorSetting",
            //        },
            //        name = new Name
            //        {
            //            defaultNames = new[]
            //            {
            //                "lights out inc. bulb A19 color hyperglow",
            //            },
            //            name = "lamp1",
            //            nicknames = new[]
            //            {
            //                "reading lamp"
            //            }
            //        },
            //        willReportState = false,
            //        roomHint = "office",
            //        attributes = new
            //        {
            //            colorModel = "rgb",
            //            colorTemperatureRange = new
            //            {
            //                temperatureMinK = 2000,
            //                temperatureMaxK = 9000,
            //            },
            //            commandOnlyColorSetting = false,
            //        },
            //        deviceInfo = new DeviceInfo
            //        {
            //            manufacturer = "lights out inc.",
            //            model = "hg11",
            //            hwVersion = "1.2",
            //            swVersion = "5.4",
            //        },
            //        customData = new
            //        {
            //            fooValue = 12,
            //            barValue = false,
            //            bazValue = "bar",
            //        },
            //    }
            //}
        };

        Program.Log("Sync called");

        var devices = new List<Device>();
        foreach (var tuple in Devices)
        {
            Program.Log("Device: " + tuple.DeviceName);
            Device device;
            switch (tuple.DeviceType)
            {
                case DeviceType.Light:
                    device = new Device
                    {
                        id = tuple.Id,
                        type = "action.devices.types.LIGHT",
                        traits = new[]
                        {
                            "action.devices.traits.OnOff",
                        },
                        name = new Name
                        {
                            name = tuple.DeviceName,
                        },
                        willReportState = true,
                        roomHint = tuple.Control.Room.GoogleName,
                    };
                    break;
                case DeviceType.Led:
                    device = new Device
                    {
                        id = tuple.Id,
                        type = "action.devices.types.LIGHT",
                        traits = new[]
                        {
                            "action.devices.traits.OnOff",
                            "action.devices.traits.Brightness",
                        },
                        name = new Name
                        {
                            name = tuple.DeviceName,
                        },
                        willReportState = false,
                        roomHint = tuple.Control.Room.GoogleName,
                    };
                    break;
                case DeviceType.Shutter:
                    device = new Device
                    {
                        id = tuple.Id,
                        type = "action.devices.types.SHUTTER", // shutter has no ui in google home app
                        //type = "action.devices.types.BLINDS",
                        traits = new[]
                        {
                            "action.devices.traits.OpenClose",
                        },
                        name = new Name
                        {
                            name = tuple.DeviceName ?? "shutter",
                        },
                        willReportState = false,
                        roomHint = tuple.Control.Room.GoogleName,
                    };
                    break;
                case DeviceType.Thermostat:
                    device = new Device
                    {
                        id = tuple.Id,
                        type = "action.devices.types.THERMOSTAT",
                        traits = new[]
                        {
                            "action.devices.traits.TemperatureSetting",
                        },
                        name = new Name
                        {
                            name = tuple.DeviceName ?? "thermostat",
                        },
                        attributes = new
                        {
                            availableThermostatModes = (string[])["off", "heat", "cool", "heatcool"],
                            thermostatTemperatureUnit = "C"
                        },
                        willReportState = false,
                        roomHint = tuple.Control.Room.GoogleName,
                    };
                    break;
                case DeviceType.Unknown:
                    throw new Exception("Unsupported device type: Unknown");
                default:
                    throw new Exception("Unsupported device type");
            }

            devices.Add(device);
        }

        Program.Log("Sync done");

        payload.devices = devices.ToArray();

        return new WebhookResponse
        {
            requestId = requestId,
            payload = payload,
        };
    }

    private static void SetRoomNames(LoxAPP3 app)
    {
        var pairing = File.ReadAllLines("rooms.txt").Select(x =>
        {
            var pair = x.Split(";");
            return new
            {
                LoxoneName = pair[0],
                GoogleName = pair[1],
            };
        }).ToDictionary(x => x.LoxoneName, x => x.GoogleName);

        foreach (var room in app.rooms.Values)
        {
            if (pairing.TryGetValue(room.name, out string googleName))
            {
                room.GoogleName = googleName;
            }
        }
    }

    public static async Task InitializeDevices(LoxAPP3 app, byte[] serviceAccountCredentialData)
    {
        Program.Log("Initialize devices");

        var credential = ServiceAccountCredential.FromServiceAccountData(new MemoryStream(serviceAccountCredentialData));
        credential.Scopes = new[] { "https://www.googleapis.com/auth/homegraph" };

        await credential.RequestAccessTokenAsync(CancellationToken.None);
        TokenSource = credential;

        SetRoomNames(app);

        void AddDevice(string id, string? deviceName, GuidAndName loxoneId)
        {
            Control control;
            if (app.controls.TryGetValue(loxoneId.id, out var parentControl))
            {
                control = parentControl;
                if (loxoneId.name != null)
                {
                    if (control.subControls != null)
                    {
                        if (control.subControls.TryGetValue(loxoneId, out var subControl))
                        {
                            control = subControl;
                        }
                        else
                        {
                            throw new Exception($"Loxone sub control not found: {loxoneId}");
                        }
                    }
                    else
                    {
                        // shading has no sub controls
                        //throw new Exception("Control should have a sub control with name=" + loxoneId.name);
                    }
                }
            }
            else
            {
                var x = app.controls.Values.Where(x => x.type == "LightControllerV2").ToArray();
                throw new Exception($"Loxone control not found: {loxoneId}");
            }

            var deviceType = DeviceType.Unknown;
            var statesToUpdate = new HashSet<string>();
            switch (control.type)
            {
                case "Switch":
                    deviceType = DeviceType.Light;
                    statesToUpdate.Add("active");
                    break;
                case "Dimmer":
                    deviceType = DeviceType.Led;
                    statesToUpdate.Add("position");
                    break;
                case "CentralJalousie":
                    deviceType = DeviceType.Shutter;
                    statesToUpdate.Add("position");
                    break;
                case "Jalousie":
                    deviceType = DeviceType.Shutter;
                    statesToUpdate.Add("position");
                    break;
                case "ValueSelector":
                    deviceType = DeviceType.Thermostat;
                    statesToUpdate.Add("position");
                    break;
                default:
                    Program.Log("Unknown control type: " + control.type);
                    break;
            }

            Devices.Add(new LoxoneDevice(app, id, deviceName, deviceType, loxoneId, control, parentControl)
            {
                StatesToUpdate = statesToUpdate
            });
        }

        Devices.Clear();

        var lines = await File.ReadAllLinesAsync("devices.txt");
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
            {
                continue;
            }

            var parts = line.Split(";");
            string id = parts[0];
            string deviceName = parts[1];
            if (deviceName == string.Empty)
            {
                deviceName = null;
            }

            string guid = parts[2];
            string name = parts[3];
            AddDevice(id, deviceName, new GuidAndName(guid, name));
        }

        Initialized = true;
    }

    public static void UpdateState(LoxoneDevice device)
    {
        string? json = null;
        bool online = false;
        if (device.DeviceType == DeviceType.Light)
        {
            bool on = false;
            if (device.States.TryGetValue("active", out object? valObj))
            {
                online = true;
                on = valObj is double d && d != 0;
            }

            var states = new Dictionary<string, object>();
            states.Add(device.Id, new
            {
                online = online,
                on = on,
            });

            json = JsonSerializer.Serialize(new
            {
                requestId = "req" + Random.Shared.Next(),
                agentUserId = AgentUserId,
                payload = new
                {
                    devices = new
                    {
                        states = states,
                    },
                }
            });
        }
        else if (device.DeviceType == DeviceType.Led)
        {
            double value = 0;
            if (device.States.TryGetValue("position", out object? valObj))
            {
                online = true;
                if (valObj is double d)
                {
                    value = d;
                }
            }

            var states = new Dictionary<string, object>();
            states.Add(device.Id, new
            {
                online = online,
                on = value > 0,
                brightness = (int)value,
            });

            json = JsonSerializer.Serialize(new
            {
                requestId = "req" + Random.Shared.Next(),
                agentUserId = AgentUserId,
                payload = new
                {
                    devices = new
                    {
                        states = states,
                    },
                }
            });
        }
        else if (device.DeviceType == DeviceType.Shutter)
        {
            double value = 0;
            if (device.States.TryGetValue("position", out object? valObj))
            {
                online = true;
                if (valObj is double d)
                {
                    value = d;
                }
            }

            var states = new Dictionary<string, object>();
            states.Add(device.Id, new
            {
                online = online,
                openPercent = (int)(100 - value * 100),
            });

            json = JsonSerializer.Serialize(new
            {
                requestId = "req" + Random.Shared.Next(),
                agentUserId = AgentUserId,
                payload = new
                {
                    devices = new
                    {
                        states = states,
                    },
                }
            });
        }
        else if (device.DeviceType == DeviceType.Thermostat)
        {
            double value = 0;
            if (device.States.TryGetValue("value", out object? valObj))
            {
                online = true;
                if (valObj is double d)
                {
                    value = d;
                }
            }

            var states = new Dictionary<string, object>();
            states.Add(device.Id, new
            {
                online = online,
                thermostatTemperatureSetpoint = value,
            });

            json = JsonSerializer.Serialize(new
            {
                requestId = "req" + Random.Shared.Next(),
                agentUserId = AgentUserId,
                payload = new
                {
                    devices = new
                    {
                        states = states,
                    },
                }
            });
        }

        if (json != null)
        {
            try
            {
                var client = new HttpClient();
                var message = new HttpRequestMessage(HttpMethod.Post,
                    "https://homegraph.googleapis.com/v1/devices:reportStateAndNotification");
                message.Content = new StringContent(json);
                message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenSource.Token.AccessToken);
                
                Program.Log(LogLevel.Debug, "update state: " + json);
                
                var result = client.Send(message);
                if (!result.IsSuccessStatusCode)
                {
                    throw new Exception("Report progress failed");
                }

                var content = result.Content;
                var task = Task.Run(() => content.ReadAsStringAsync());
                task.Wait();
                var result1 = task.Result;
                Program.Log(LogLevel.Debug, "update state result: " + result1);
            }
            catch (Exception ex)
            {
                Program.Log(LogLevel.Debug, "failed to update google state: " + ex.ToString());
            }
        }
    }
}