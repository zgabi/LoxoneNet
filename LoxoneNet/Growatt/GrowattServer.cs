namespace LoxoneNet.Growatt;

internal class GrowattServer
{
    private string _userName;
    private string _password;
    private Thread _thread;
    private bool _running;

    public void Start(string userName, string password)
    {
        _userName = userName;
        _password = password;
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
        while (_running)
        {
            var loxone = Program.LoxoneSocket;
            try
            {
                if (loxone != null)
                {
                    var sh = new Ealse.Growatt.Api.Session(_userName, _password);
                    var plants = await sh.GetPlantList();
                    var plant = plants[0];
                    var weather = await sh.GetWeatherByPlant(plant.Id);
                    var plantData = await sh.GetPlantData(plant.Id);
                    var devices = await sh.GetDevicesByPlantList(plantData.Id);
                    var device = devices[0];
                    loxone.SendCommandUdp("GrowattTotal" + device.EnergyTotal);
                    loxone.SendCommandUdp("GrowattMonth" + device.EnergyMonth);
                    loxone.SendCommandUdp("GrowattToday" + device.EnergyToday);
                    //var deviceInfo = await sh.GetDatalogDeviceInfo(plant.Id, device.DatalogSn);
                }
            }
            catch
            {
            }

            Thread.Sleep(loxone == null ? 100 : 5 * 60000);
        }
    }
}