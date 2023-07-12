using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Text.Json;
using System.Text.Json.Serialization;
using EasyWindowSnapper;
using System;

using System.Windows.Forms;
using static AppSettings;


public class SettingsForm : Form
{
    private static readonly List<string> DefaultIgnoreWindowTitles = new List<string> { "Windows 入力エクスペリエンス", "設定", "メール" };

    private ComboBox _middleForwardButtonClickActionComboBox;

    private ComboBox _middleBackButtonClickActionComboBox;

    private NumericUpDown _zoomRatioNumericUpDown;
    private NumericUpDown _leftScreenRatioNumericUpDown;

    private TextBox _ignoreWindowTitlesTextBox;

    private NumericUpDown _maxDisplayRowsNumericUpDown;
    private NumericUpDown _rowHeightNumericUpDown;

    private Button _saveButton;
    private Button _resetButton;

    private int buttonMargin = 10;
    private int buttonWidth = 100;
    private int buttonHeight = 40;
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
        Size = new Size(650, 650);

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
            Text = "戻る+ホイールでウィンドウの幅を何割ずつ拡大/縮小するか :",
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
            Text = "左のウィンドウをスナップしたときモニタの何割を使うか:",
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


        var maxDisplayRowsLabel = new Label
        {
            Text = "ウィンドウ一覧画面で表示する数(反映には再起動が必要です):",
            Location = new Point(20, 260),  // Changed from 380 to 260
            AutoSize = true,
        };
        _maxDisplayRowsNumericUpDown = new NumericUpDown
        {
            Location = new Point(20, 280),  // Changed from 400 to 280
            Width = 200,
            Minimum = 1,
            Maximum = 100,
            DecimalPlaces = 0,
            Value = DefaultMaxDisplayRows
        };

        var rowHeightLabel = new Label
        {
            Text = "1行の高さ:",
            Location = new Point(20, 320),
            AutoSize = true,
        };
        _rowHeightNumericUpDown = new NumericUpDown
        {
            Location = new Point(20, 340),
            Width = 200,
            Minimum = 10,
            Maximum = 200,
            DecimalPlaces = 0,
            Value = DefaultRowHeight
        };

        var ignoreWindowTitlesLabel = new Label
        {
            Text = "ウィンドウ一覧画面に表示しないウィンドウのタイトル (カンマ区切りで記載して下さい):",
            Location = new Point(20, 380),
            AutoSize = true
        };
        _ignoreWindowTitlesTextBox = new TextBox
        {
            Location = new Point(20, 400),
            Width = 450,
            
            Text = string.Join(",", DefaultIgnoreWindowTitles)
        };


        _saveButton = new Button
        {
            Text = "Save",
            Size = new Size(buttonWidth, buttonHeight),
            Location = new Point(600 - buttonMargin - buttonWidth, 550 - buttonMargin - buttonHeight)
        };

        _resetButton = new Button
        {
            Text = "Reset",
            Size = new Size(buttonWidth, buttonHeight),
            Location = new Point(600 - buttonMargin - 2 * buttonWidth, 550 - buttonMargin - buttonHeight)
        };


        if (culture.Name.StartsWith("en"))
        {
            middleForwardClickActionLabel.Text = "Action for middle button click while back button pressed:";
            zoomRatioLabel.Text = "Zoom ratio for window width adjustment with back+wheel:";
            leftScreenRatioLabel.Text = "Ratio of left window in window snapping:";
            ignoreWindowTitlesLabel.Text = "List of window titles to ignore (comma separated):";
            maxDisplayRowsLabel.Text = "Number of rows displayed in the window list (requires restart):";  // Added this line
            rowHeightLabel.Text = "Row height:";  // Added this line

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
        Controls.Add(ignoreWindowTitlesLabel);
        Controls.Add(_ignoreWindowTitlesTextBox);
        Controls.Add(maxDisplayRowsLabel);
        Controls.Add(_maxDisplayRowsNumericUpDown);
        Controls.Add(rowHeightLabel);
        Controls.Add(_rowHeightNumericUpDown);
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
        _ignoreWindowTitlesTextBox.Text = string.Join(",", settings.IgnoreWindowTitles);
        _maxDisplayRowsNumericUpDown.Value = settings.MaxDisplayRows;
        _rowHeightNumericUpDown.Value = settings.RowHeight;
    }
    private void ResetSettings(object sender, EventArgs e)
    {
        // Set default values to controls
        _middleForwardButtonClickActionComboBox.SelectedIndex = 0;
        _zoomRatioNumericUpDown.Value = (decimal)DefaultZoomRatio;
        _leftScreenRatioNumericUpDown.Value = (decimal)DefaultLeftScreenRatio;
        _ignoreWindowTitlesTextBox.Text = string.Join(",", DefaultIgnoreWindowTitles);
        _maxDisplayRowsNumericUpDown.Value = DefaultMaxDisplayRows;
        _rowHeightNumericUpDown.Value = DefaultRowHeight;
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
            AppSettings.Instance.IgnoreWindowTitles = _ignoreWindowTitlesTextBox.Text.Split(',').ToList();
            AppSettings.Instance.MaxDisplayRows = (int)_maxDisplayRowsNumericUpDown.Value;
            AppSettings.Instance.RowHeight = (int)_rowHeightNumericUpDown.Value;

            AppSettings.Instance.SaveSettings();

            MessageBox.Show("Settings saved successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save settings: {ex}");
            MessageBox.Show("Failed to save settings. Please check if the input values are in correct format.");
        }
    }
}