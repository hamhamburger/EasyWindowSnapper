using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinSplit;
using System;

using System.Windows.Forms;
using static AppSettings;

public class SettingsForm : Form
{
    private const double DefaultZoomRatio = 0.015;
    private const double DefaultLeftScreenRatio = 0.6;
    private const int DefaultMinWindowWidth = 661;
    private static readonly List<string> DefaultIgnoreWindowTitles = new List<string> { "Windows 入力エクスペリエンス", "設定", "メール" };

    private ComboBox _middleForwardButtonClickActionComboBox;

    private ComboBox _middleBackButtonClickActionComboBox;

    private NumericUpDown _zoomRatioNumericUpDown;
    private NumericUpDown _leftScreenRatioNumericUpDown;
    private NumericUpDown _minWindowWidthNumericUpDown;
    private TextBox _ignoreWindowTitlesTextBox;
    private Button _saveButton;
    private Button _resetButton;
    public class ComboBoxItem
    {
        public ButtonAction Value { get; set; }
        public string Text { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }
    public SettingsForm()
    {

        var culture = System.Threading.Thread.CurrentThread.CurrentUICulture;

        Text = "Settings";
        Size = new Size(600, 550);

        // 各コントロールの初期化
        var middleForwardClickActionLabel = new Label
        {
            Text = "進むボタンを押しながら中ボタンクリックの動作:",
            Location = new Point(20, 20),
            AutoSize = true,

        };
        _middleForwardButtonClickActionComboBox = new ComboBox
        {
            Location = new Point(20, 40),
            Width = 350,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };

        _middleForwardButtonClickActionComboBox.Items.Add(new ComboBoxItem { Value = ButtonAction.MINIMIZE_WINDOW, Text = "選択中のウィンドウを最小化する" });
        _middleForwardButtonClickActionComboBox.Items.Add(new ComboBoxItem { Value = ButtonAction.MAXIMIZE_WINDOW, Text = "選択中のウィンドウを最大化する" });
        _middleForwardButtonClickActionComboBox.Items.Add(new ComboBoxItem { Value = ButtonAction.CLOSE_WINDOW, Text = "選択中のウィンドウを閉じる" });


        var middleBackClickActionLabel = new Label
        {
            Text = "戻るボタンを押しながら中ボタンクリックの動作:",
            Location = new Point(20, 80),
            AutoSize = true,
        };
        _middleBackButtonClickActionComboBox = new ComboBox
        {
            Location = new Point(20, 100),
            Width = 350,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };

        _middleBackButtonClickActionComboBox.Items.Add(new ComboBoxItem { Value = ButtonAction.SWAP_WINDOWS, Text = "左右のウィンドウを交換する" });
        _middleBackButtonClickActionComboBox.Items.Add(new ComboBoxItem { Value = ButtonAction.MINIMIZE_WINDOW, Text = "カーソル下のウィンドウを最小化する" });
        _middleBackButtonClickActionComboBox.Items.Add(new ComboBoxItem { Value = ButtonAction.MAXIMIZE_WINDOW, Text = "カーソル下のウィンドウを最大化する" });
        _middleBackButtonClickActionComboBox.Items.Add(new ComboBoxItem { Value = ButtonAction.CLOSE_WINDOW, Text = "カーソル下のウィンドウを閉じる" });

        var zoomRatioLabel = new Label
        {
            Text = "戻る+ホイールでウィンドウの幅を何割ずつ調整するか :",
            Location = new Point(20, 140),
            AutoSize = true,
        };

        _zoomRatioNumericUpDown = new NumericUpDown
        {
            Location = new Point(20, 160),
            Width = 200,
            Minimum = 0,
            Maximum = 1,
            DecimalPlaces = 3,
            Increment = 0.001m,
            Value = (decimal)DefaultZoomRatio
        };

        var leftScreenRatioLabel = new Label
        {
            Text = "右にウィンドウが存在しない状態で左にスナップした時モニターの何割を使うか:",
            Location = new Point(20, 200),
            AutoSize = true,
        };
        _leftScreenRatioNumericUpDown = new NumericUpDown
        {
            Location = new Point(20, 220),
            Width = 200,
            Minimum = 0,
            Maximum = 1,
            DecimalPlaces = 2,
            Increment = 0.1m,
            Value = (decimal)DefaultLeftScreenRatio
        };

        var minWindowWidthLabel = new Label
        {
            Text = "最小ウィンドウ幅:",
            Location = new Point(20, 260),
            AutoSize = true,
        };
        _minWindowWidthNumericUpDown = new NumericUpDown
        {
            Location = new Point(20, 280),
            Width = 200,
            Minimum = 100,
            Maximum = 10000,
            DecimalPlaces = 0,
            Value = DefaultMinWindowWidth
        };

        var ignoreWindowTitlesLabel = new Label
        {
            Text = "無視するウィンドウのタイトル (カンマ区切りで記載して下さい):",
            Location = new Point(20, 320),
            AutoSize = true
        };
        _ignoreWindowTitlesTextBox = new TextBox
        {
            Location = new Point(20, 340),
            Width = 350,
            Text = string.Join(",", DefaultIgnoreWindowTitles)
        };

        _saveButton = new Button
        {
            Text = "Save",
            Size = new Size(100, 40),
            Location = new Point(400, 410)

        };
        _resetButton = new Button
        {
            Text = "Reset",
            Size = new Size(100, 40),
            // フォームの中央

            Location = new Point(290, 410)

        };


        if (culture.Name.StartsWith("en"))
        {
            // If the system's current UI culture is English, use English labels
            middleForwardClickActionLabel.Text = "Action for middle button click while back button pressed:";
            zoomRatioLabel.Text = "Zoom ratio for window width adjustment with back+wheel:";
            leftScreenRatioLabel.Text = "Ratio of left window in window snapping:";
            minWindowWidthLabel.Text = "Minimum window width:";
            ignoreWindowTitlesLabel.Text = "List of window titles to ignore (comma separated):";


            _middleForwardButtonClickActionComboBox.Items.Clear();
            _middleForwardButtonClickActionComboBox.Items.Add(new ComboBoxItem { Value = ButtonAction.MINIMIZE_WINDOW, Text = "Minimize selected window" });
            _middleForwardButtonClickActionComboBox.Items.Add(new ComboBoxItem { Value = ButtonAction.MAXIMIZE_WINDOW, Text = "Maximize selected window" });
            _middleForwardButtonClickActionComboBox.Items.Add(new ComboBoxItem { Value = ButtonAction.CLOSE_WINDOW, Text = "Close selected window" });

            _middleBackButtonClickActionComboBox.Items.Clear();
            _middleBackButtonClickActionComboBox.Items.Add(new ComboBoxItem { Value = ButtonAction.SWAP_WINDOWS, Text = "Swap left and right windows" });
            _middleBackButtonClickActionComboBox.Items.Add(new ComboBoxItem { Value = ButtonAction.MINIMIZE_WINDOW, Text = "Minimize selected window" });
            _middleBackButtonClickActionComboBox.Items.Add(new ComboBoxItem { Value = ButtonAction.MAXIMIZE_WINDOW, Text = "Maximize selected window" });
            _middleBackButtonClickActionComboBox.Items.Add(new ComboBoxItem { Value = ButtonAction.CLOSE_WINDOW, Text = "Close selected window" });


        }


        _saveButton.Click += SaveSettings;
        _resetButton.Click += ResetSettings;



        // フォームに各コントロールを追加
        Controls.Add(middleForwardClickActionLabel);
        Controls.Add(_middleForwardButtonClickActionComboBox);
        Controls.Add(middleBackClickActionLabel);
        Controls.Add(_middleBackButtonClickActionComboBox);
        Controls.Add(zoomRatioLabel);
        Controls.Add(_zoomRatioNumericUpDown);
        Controls.Add(leftScreenRatioLabel);
        Controls.Add(_leftScreenRatioNumericUpDown);
        Controls.Add(minWindowWidthLabel);
        Controls.Add(_minWindowWidthNumericUpDown);
        Controls.Add(ignoreWindowTitlesLabel);
        Controls.Add(_ignoreWindowTitlesTextBox);
        Controls.Add(_resetButton);
        Controls.Add(_saveButton);


        // 既存の設定をロード
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = AppSettings.Instance;

        var middleButtonClickAction = settings.MiddleForwardButtonClickAction;
        foreach (ComboBoxItem item in _middleForwardButtonClickActionComboBox.Items)
        {
            if (item.Value == middleButtonClickAction)
            {
                _middleForwardButtonClickActionComboBox.SelectedItem = item;
                break;
            }
        }

        foreach (ComboBoxItem item in _middleBackButtonClickActionComboBox.Items)
        {
            if (item.Value == settings.MiddleBackButtonClickAction)
            {
                _middleBackButtonClickActionComboBox.SelectedItem = item;
                break;
            }
        }

        _zoomRatioNumericUpDown.Value = (decimal)settings.ExtendRatio;
        _leftScreenRatioNumericUpDown.Value = (decimal)settings.LeftScreenRatio;
        _minWindowWidthNumericUpDown.Value = settings.MinWindowWidth;
        _ignoreWindowTitlesTextBox.Text = string.Join(",", settings.IgnoreWindowTitles);
    }
    private void ResetSettings(object sender, EventArgs e)
    {
        // Set default values to controls
        _middleForwardButtonClickActionComboBox.SelectedIndex = 0;
        _zoomRatioNumericUpDown.Value = (decimal)DefaultZoomRatio;
        _leftScreenRatioNumericUpDown.Value = (decimal)DefaultLeftScreenRatio;
        _minWindowWidthNumericUpDown.Value = DefaultMinWindowWidth;
        _ignoreWindowTitlesTextBox.Text = string.Join(",", DefaultIgnoreWindowTitles);
    }

    private void SaveSettings(object sender, EventArgs e)
    {
        try
        {
            var middleForward = _middleForwardButtonClickActionComboBox.SelectedItem as ComboBoxItem;
            var middleBack = _middleBackButtonClickActionComboBox.SelectedItem as ComboBoxItem;

            AppSettings.Instance.MiddleForwardButtonClickAction = middleForward.Value;
            AppSettings.Instance.MiddleBackButtonClickAction = middleBack.Value;

            AppSettings.Instance.ExtendRatio = (double)_zoomRatioNumericUpDown.Value;
            AppSettings.Instance.LeftScreenRatio = (double)_leftScreenRatioNumericUpDown.Value;
            AppSettings.Instance.MinWindowWidth = (int)_minWindowWidthNumericUpDown.Value;
            AppSettings.Instance.IgnoreWindowTitles = _ignoreWindowTitlesTextBox.Text.Split(',').ToList();

            // Save settings using the singleton instance
            AppSettings.Instance.SaveSettings();

            // Show a message that settings have been saved successfully
            MessageBox.Show("Settings saved successfully.");
        }
        catch (Exception ex)
        {
            // Log the error message and also notify the user
            Console.WriteLine($"Failed to save settings: {ex}");
            MessageBox.Show("Failed to save settings. Please check if the input values are in correct format.");
        }
    }
}