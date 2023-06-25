using System.Text.Json;
using WinSplit;
using System.Text.Json.Serialization;


// MiddleForwardButtonClickActionの配列
// MiddleBackButtonClickActionの配列
// enum
public class AppSettings
{
    public enum ButtonAction
    {
        MINIMIZE_WINDOW,
        MAXIMIZE_WINDOW,
        CLOSE_WINDOW,
        SWAP_WINDOWS
    }

    public const double DefaultZoomRatio = 0.015;
    public const double DefaultLeftScreenRatio = 0.6;
    public const int DefaultMinWindowWidth = 661;
    public List<string> DefaultIgnoreWindowTitles = new List<string> { "Windows 入力エクスペリエンス", "設定", "メール", "Spotify Widget", "タスク マネージャー" };

    public const int DefaultMaxDisplayRows = 10;

    public const int DefaultRowHeight = 60;


    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ButtonAction MiddleForwardButtonClickAction { get; set; } = ButtonAction.CLOSE_WINDOW;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ButtonAction MiddleBackButtonClickAction { get; set; } = ButtonAction.SWAP_WINDOWS;

    public double ExtendRatio { get; set; } = DefaultZoomRatio;
    public double LeftScreenRatio { get; set; } = DefaultLeftScreenRatio;
    public int MinWindowWidth { get; set; } = DefaultMinWindowWidth;
    public List<string> IgnoreWindowTitles { get; set; } = new List<string> { "Windows 入力エクスペリエンス", "設定", "メール" };

    public int MaxDisplayRows { get; set; } = DefaultMaxDisplayRows;

    public int RowHeight { get; set; } = DefaultRowHeight;

    private static AppSettings _instance;
    public static AppSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = LoadSettings();
            }

            return _instance;
        }
    }

    private static AppSettings LoadSettings()
    {
        if (File.Exists("appsettings.json"))
        {
            var json = File.ReadAllText("appsettings.json");
            var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
            return JsonSerializer.Deserialize<AppSettings>(json, options);
        }
        else
        {
            return new AppSettings();
        }
    }

    public void SaveSettings()
    {
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText("appsettings.json", json);
    }
}