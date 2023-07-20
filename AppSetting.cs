using System.Text.Json;
using EasyWindowSnapper;
using System.Text.Json.Serialization;



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
    public static List<string> DefaultIgnoreWindowTitles = new List<string> { "Windows 入力エクスペリエンス", "設定", "メール", "Spotify Widget", "タスク マネージャー" };

    public static List<string> DefaultNonImmediateResizeWindowClasses = new List<string> { "ExploreWClass", "CabinetWClass" };

    public const int DefaultMaxDisplayRows = 10;

    public const int DefaultRowHeight = 60;

    public const bool DefaultIsDarkMode = false;


    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ButtonAction MiddleForwardButtonClickAction { get; set; } = ButtonAction.CLOSE_WINDOW;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ButtonAction MiddleBackButtonClickAction { get; set; } = ButtonAction.SWAP_WINDOWS;

    public double ExtendRatio { get; set; } = DefaultZoomRatio;
    public double LeftScreenRatio { get; set; } = DefaultLeftScreenRatio;
    public List<string> IgnoreWindowTitles { get; set; } = DefaultIgnoreWindowTitles;
    // ウィンドウのリサイズ時に毎回サイズを変更しないウィンドウのクラス名

    public List<string> NonImmediateResizeWindowClasses { get; set; } = DefaultNonImmediateResizeWindowClasses;

    public int MaxDisplayRows { get; set; } = DefaultMaxDisplayRows;


    public int RowHeight { get; set; } = DefaultRowHeight;


    public bool IsDarkMode { get; set; } = DefaultIsDarkMode;

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