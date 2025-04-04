namespace LoxoneNet.Loxone;

class MsInfo
{
    public string serialNr { get; set; } = null!;

    public string msName { get; set; } = null!;

    public string projectName { get; set; } = null!;

    public string localUrl { get; set; } = null!;

    public string remoteUrl { get; set; } = null!;

    public int tempUnit { get; set; }

    public string currency { get; set; } = null!;

    public string squareMeasure { get; set; } = null!;

    public string location { get; set; } = null!;

    public double latitude { get; set; }

    public double longitude { get; set; }

    public int altitude { get; set; }

    public string languageCode { get; set; } = null!;

    public string heatPeriodStart { get; set; } = null!;

    public string heatPeriodEnd { get; set; } = null!;

    public string coolPeriodStart { get; set; } = null!;

    public string coolPeriodEnd { get; set; } = null!;

    public string catTitle { get; set; } = null!;

    public string roomTitle { get; set; } = null!;

    public MiniserverType miniserverType { get; set; }

    public LoxoneUser currentUser { get; set; } = null!;
}