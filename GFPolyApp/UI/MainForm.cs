using System.Diagnostics;
using System.Drawing.Printing;
using System.Text;
using GFPolyApp.Core;
using GFPolyApp.Storage;
using GFPolyLib;

namespace GFPolyApp.UI;

/// <summary>Главное окно демонстрационного приложения библиотеки GFPolyLib.</summary>
public sealed partial class MainForm : Form
{
    private const int DisplayLimit = 4000;      // символов результата, выводимых на экран
    private const int LiveCheckLimit = 200_000; // до этой длины запись проверяется во время ввода

    private readonly GfDatabase? _db;
    private readonly string? _screenshotDir;
    private readonly List<OperationResult> _results = new();
    private GaloisField? _field;
    private CancellationTokenSource? _cts;
    private bool _busy;
    private string _sourceA = "keyboard", _sourceB = "keyboard";
    private string? _lastFile;

    // элементы управления
    private readonly MenuStrip _menu = new();
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _stField = new() { Spring = false };
    private readonly ToolStripStatusLabel _stMessage = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _stDb = new();
    private readonly ToolStripProgressBar _stProgress = new() { Style = ProgressBarStyle.Marquee, Visible = false, Width = 120 };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly ErrorProvider _errors = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };
    private readonly ToolTip _tips = new() { AutoPopDelay = 15000 };
    private readonly System.Windows.Forms.Timer _validateTimer = new() { Interval = 400 };

    private readonly TextBox _tbP = new() { Width = 260 };
    private readonly NumericUpDown _nudK = new() { Minimum = 1, Maximum = GaloisField.MaxExtensionDegree, Value = 1, Width = 70 };
    private readonly TextBox _tbModulus = new() { Width = 260, Enabled = false, PlaceholderText = "подбирается автоматически" };
    private readonly Button _btnField = new() { Text = "Создать поле", AutoSize = true };
    private readonly Label _lblFieldInfo = new() { AutoSize = true, MaximumSize = new Size(560, 0), ForeColor = Color.DimGray };

    private readonly TextBox _tbA = MakeInput();
    private readonly TextBox _tbB = MakeInput();
    private readonly TextBox _tbC = new() { Width = 220 };
    private readonly NumericUpDown _nudRandA = new() { Minimum = 0, Maximum = 2_000_000, Value = 10, Width = 90 };
    private readonly NumericUpDown _nudRandB = new() { Minimum = 0, Maximum = 2_000_000, Value = 5, Width = 90 };
    private readonly Button _btnRandA = new() { Text = "Случайный A", AutoSize = true };
    private readonly Button _btnRandB = new() { Text = "Случайный B", AutoSize = true };

    private readonly Button _btnAdd = OpButton("A + B");
    private readonly Button _btnSub = OpButton("A − B");
    private readonly Button _btnMul = OpButton("A × B");
    private readonly Button _btnDiv = OpButton("A ÷ B");
    private readonly Button _btnEval = OpButton("A(c)");
    private readonly Button _btnCancel = new() { Text = "Прервать", Width = 110, Height = 30, Enabled = false };
    private readonly ComboBox _cbMul = Combo("Автоматически", "Школьный O(nm)", "Карацубы O(n^1,585)");
    private readonly ComboBox _cbDiv = Combo("Автоматически", "«Уголком» O((n−m+1)m)", "Ньютона O(M(n))");
    private readonly CheckBox _chkLog = new() { Text = "Записывать операции в журнал хранилища", AutoSize = true, Checked = true };
    private readonly TextBox _tbResult = new()
    {
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = true, Dock = DockStyle.Fill,
        Font = new Font("Consolas", 10f), BackColor = Color.White,
    };

    private readonly DataGridView _gridPoly = MakeGrid();
    private readonly DataGridView _gridOps = MakeGrid();
    private readonly Label _lblDbStats = new() { AutoSize = true, ForeColor = Color.DimGray };

    private readonly ComboBox _cbBenchOp = Combo("Умножение: школьный и Карацубы", "Деление: «уголком» и Ньютона");
    private readonly NumericUpDown _nudBenchMax = new() { Minimum = 64, Maximum = 200_000, Value = 4000, Increment = 500, Width = 90 };
    private readonly NumericUpDown _nudBenchSteps = new() { Minimum = 2, Maximum = 20, Value = 8, Width = 60 };
    private readonly Button _btnBench = new() { Text = "Выполнить замер", AutoSize = true };
    private readonly SimpleChart _chart = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _gridBench = MakeGrid();

    public MainForm(GfDatabase? db, string? dbError, string? screenshotDir)
    {
        _db = db;
        _screenshotDir = screenshotDir;
        Text = "GFPolyLib — операции над многочленами в поле Галуа GF(q)";
        Font = new Font("Segoe UI", 9.5f);
        MinimumSize = new Size(1000, 700);
        Size = new Size(1180, 800);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        BuildMenu();
        BuildStatus();
        _tabs.TabPages.Add(BuildCalcPage());
        _tabs.TabPages.Add(BuildStoragePage());
        _tabs.TabPages.Add(BuildBenchPage());
        Controls.Add(_tabs);
        Controls.Add(_menu);
        Controls.Add(_status);
        MainMenuStrip = _menu;

        _validateTimer.Tick += (_, _) => { _validateTimer.Stop(); LiveValidate(); };
        _tabs.SelectedIndexChanged += (_, _) => { if (_tabs.SelectedIndex == 1) RefreshStorage(); };
        FormClosing += OnFormClosing;

        _tbP.Text = "7";
        UpdateFieldInfo();
        UpdateState();
        if (_db is null)
            Shown += (_, _) => MessageBox.Show(this,
                "Не удалось открыть хранилище данных. Вычисления доступны, но журнал и сохранение многочленов отключены.\n\n" + dbError,
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        if (_screenshotDir is not null) Shown += async (_, _) => await RunScreenshotScenarioAsync(_screenshotDir);
    }

    // =================================================================== разметка

    private static TextBox MakeInput() => new()
    {
        Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 62, Dock = DockStyle.Fill, MaxLength = 0,
        Font = new Font("Consolas", 10f),
    };

    private static Button OpButton(string text) => new()
    {
        Text = text, Width = 92, Height = 34, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), Enabled = false,
    };

    private static ComboBox Combo(params string[] items)
    {
        var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
        cb.Items.AddRange(items);
        cb.SelectedIndex = 0;
        return cb;
    }

    private static DataGridView MakeGrid() => new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White,
        AutoGenerateColumns = false,
    };

    private void BuildMenu()
    {
        var file = new ToolStripMenuItem("&Файл");
        file.DropDownItems.Add(Item("&Открыть исходные данные…", Keys.Control | Keys.O, (_, _) => OpenTaskFile()));
        file.DropDownItems.Add(Item("&Сохранить исходные данные…", Keys.Control | Keys.S, (_, _) => SaveTaskFile()));
        file.DropDownItems.Add(Item("Сохранить &результаты…", Keys.Control | Keys.Shift | Keys.S, (_, _) => SaveResults()));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Item("Предварительный &просмотр…", Keys.None, (_, _) => Print(preview: true)));
        file.DropDownItems.Add(Item("&Печать…", Keys.Control | Keys.P, (_, _) => Print(preview: false)));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Item("&Выход", Keys.Alt | Keys.F4, (_, _) => Close()));

        var storage = new ToolStripMenuItem("&Хранилище");
        storage.DropDownItems.Add(Item("Сохранить многочлен &A…", Keys.None, (_, _) => SaveToStorage(_tbA, "A")));
        storage.DropDownItems.Add(Item("Сохранить многочлен &B…", Keys.None, (_, _) => SaveToStorage(_tbB, "B")));
        storage.DropDownItems.Add(Item("&Открыть таблицы хранилища", Keys.Control | Keys.D2, (_, _) => _tabs.SelectedIndex = 1));
        storage.DropDownItems.Add(new ToolStripSeparator());
        storage.DropDownItems.Add(Item("&Очистить журнал…", Keys.None, (_, _) => ClearJournal()));
        storage.Enabled = _db is not null;

        var results = new ToolStripMenuItem("&Результаты");
        results.DropDownItems.Add(Item("&Очистить окно результатов", Keys.None, (_, _) =>
        {
            if (_results.Count > 0 && Ask("Очистить окно результатов? Журнал в хранилище сохранится.")) ClearResults();
        }));

        var help = new ToolStripMenuItem("&Справка");
        help.DropDownItems.Add(Item("&Руководство пользователя", Keys.F1, (_, _) => ShowHelp()));
        help.DropDownItems.Add(Item("&О программе", Keys.None, (_, _) => MessageBox.Show(this,
            $"GFPolyLib Demo 2.0\nБиблиотека GFPolyLib {GaloisField.Version}\n\n" +
            "Курсовой проект по дисциплине «Технологии и методы программирования».\n" +
            "Выполнил: студент гр. 3431 Машир А.Ю.\nРуководитель: проф., к.т.н. Фомичева С.Г.\n\nГУАП, 2026",
            "О программе", MessageBoxButtons.OK, MessageBoxIcon.Information)));

        _menu.Items.AddRange(new ToolStripItem[] { file, storage, results, help });
    }

    private static ToolStripMenuItem Item(string text, Keys keys, EventHandler click)
    {
        var item = new ToolStripMenuItem(text, null, click);
        if (keys != Keys.None) item.ShortcutKeys = keys;
        return item;
    }

    private void BuildStatus()
    {
        _status.Items.AddRange(new ToolStripItem[] { _stField, new ToolStripSeparator(), _stMessage, _stProgress, _stDb });
        UpdateDbStatus();
    }

    private TabPage BuildCalcPage()
    {
        var page = new TabPage("Вычисления") { Padding = new Padding(8) };

        // --- поле
        var gbField = new GroupBox { Text = "Поле Галуа GF(q), q = p^k", Dock = DockStyle.Fill, Padding = new Padding(10) };
        var tlField = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        tlField.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tlField.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(tlField, "Характеристика p:", _tbP);
        AddRow(tlField, "Степень расширения k:", _nudK);
        AddRow(tlField, "Порождающий f(x):", _tbModulus);
        var fieldButtons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        fieldButtons.Controls.Add(_btnField);
        tlField.Controls.Add(fieldButtons, 1, tlField.RowCount++);
        tlField.Controls.Add(_lblFieldInfo, 0, tlField.RowCount);
        tlField.SetColumnSpan(_lblFieldInfo, 2);
        gbField.Controls.Add(tlField);

        _tips.SetToolTip(_tbP, "Простое число или выражение: 7, 65537, 2^61-1, 2^127-1, 10^9+7");
        _tips.SetToolTip(_tbModulus, "Неприводимый многочлен степени k над GF(p), например x^8 + x^4 + x^3 + x + 1.\nЕсли поле пустое, многочлен подбирается автоматически.");
        _tbP.KeyPress += (_, e) => FilterKeys(e, "0123456789^+-*() ");
        _tbModulus.KeyPress += (_, e) => FilterKeys(e, "0123456789xX^+-*() ,");
        _nudK.ValueChanged += (_, _) => { _tbModulus.Enabled = _nudK.Value > 1; UpdateFieldInfo(); };
        _tbP.TextChanged += (_, _) => UpdateFieldInfo();
        _btnField.Click += async (_, _) => await CreateFieldAsync();

        // --- алгоритмы и операции
        var gbOps = new GroupBox { Text = "Операции", Dock = DockStyle.Fill, Padding = new Padding(10) };
        var tlOps = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        tlOps.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tlOps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(tlOps, "Умножение:", _cbMul);
        AddRow(tlOps, "Деление:", _cbDiv);
        var opsFlow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        opsFlow.Controls.AddRange(new Control[] { _btnAdd, _btnSub, _btnMul, _btnDiv, _btnEval });
        tlOps.Controls.Add(opsFlow, 0, tlOps.RowCount);
        tlOps.SetColumnSpan(opsFlow, 2);
        tlOps.RowCount++;
        var opsFlow2 = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        opsFlow2.Controls.AddRange(new Control[] { _btnCancel, _chkLog });
        tlOps.Controls.Add(opsFlow2, 0, tlOps.RowCount);
        tlOps.SetColumnSpan(opsFlow2, 2);
        gbOps.Controls.Add(tlOps);
        _chkLog.Enabled = _db is not null;
        if (_db is null) _chkLog.Checked = false;

        _btnAdd.Click += async (_, _) => await RunOperationAsync("add");
        _btnSub.Click += async (_, _) => await RunOperationAsync("sub");
        _btnMul.Click += async (_, _) => await RunOperationAsync("mul");
        _btnDiv.Click += async (_, _) => await RunOperationAsync("divmod");
        _btnEval.Click += async (_, _) => await RunOperationAsync("eval");
        _btnCancel.Click += (_, _) => _cts?.Cancel();
        _cbMul.SelectedIndexChanged += (_, _) => ApplyAlgorithms();
        _cbDiv.SelectedIndexChanged += (_, _) => ApplyAlgorithms();

        // --- исходные многочлены
        var gbData = new GroupBox { Text = "Исходные данные", Dock = DockStyle.Fill, Padding = new Padding(10) };
        var tlData = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        tlData.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tlData.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tlData.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tlData.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        tlData.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        tlData.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlData.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlData.Controls.Add(new Label { Text = "A(x) =", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(3, 8, 3, 3) }, 0, 0);
        tlData.Controls.Add(_tbA, 1, 0);
        tlData.Controls.Add(RandomPanel(_nudRandA, _btnRandA), 2, 0);
        tlData.Controls.Add(new Label { Text = "B(x) =", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(3, 8, 3, 3) }, 0, 1);
        tlData.Controls.Add(_tbB, 1, 1);
        tlData.Controls.Add(RandomPanel(_nudRandB, _btnRandB), 2, 1);
        tlData.Controls.Add(new Label { Text = "Точка c =", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 3) }, 0, 2);
        tlData.Controls.Add(_tbC, 1, 2);
        var hint = new Label
        {
            AutoSize = true, ForeColor = Color.DimGray, MaximumSize = new Size(900, 0),
            Text = "Запись: 3x^4 + 5x^3 − x + 6  или список коэффициентов от старшего: 3 5 0 −1 6. " +
                   "Элементы расширения GF(p^k) — в скобках через a: (a^2 + 1)x^3 + (a)x + 1.",
        };
        tlData.Controls.Add(hint, 0, 3);
        tlData.SetColumnSpan(hint, 3);
        gbData.Controls.Add(tlData);

        _tbA.KeyPress += (_, e) => FilterPolynomialKeys(e);
        _tbB.KeyPress += (_, e) => FilterPolynomialKeys(e);
        _tbC.KeyPress += (_, e) => FilterKeys(e, "0123456789aA^+-*() ");
        _tbA.TextChanged += (_, _) => { if (!_settingText) _sourceA = "keyboard"; OnInputChanged(); };
        _tbB.TextChanged += (_, _) => { if (!_settingText) _sourceB = "keyboard"; OnInputChanged(); };
        _tbC.TextChanged += (_, _) => OnInputChanged();
        _btnRandA.Click += async (_, _) => await RandomAsync(_tbA, (int)_nudRandA.Value, isA: true);
        _btnRandB.Click += async (_, _) => await RandomAsync(_tbB, (int)_nudRandB.Value, isA: false);
        _tips.SetToolTip(_btnRandA, "Заполнить A случайным многочленом заданной степени");
        _tips.SetToolTip(_btnRandB, "Заполнить B случайным многочленом заданной степени");

        // --- результаты
        var gbRes = new GroupBox { Text = "Результаты", Dock = DockStyle.Fill, Padding = new Padding(8) };
        gbRes.Controls.Add(_tbResult);

        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Height = 230 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        top.Controls.Add(gbField, 0, 0);
        top.Controls.Add(gbOps, 1, 0);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 235));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(top, 0, 0);
        layout.Controls.Add(gbData, 0, 1);
        layout.Controls.Add(gbRes, 0, 2);
        page.Controls.Add(layout);
        return page;
    }

    private static Control RandomPanel(NumericUpDown nud, Button btn)
    {
        var p = new Panel { Width = 190, Height = 66, Margin = new Padding(6, 0, 0, 0) };
        p.Controls.Add(new Label { Text = "степень:", AutoSize = true, Left = 0, Top = 6 });
        nud.Left = 70;
        nud.Top = 3;
        nud.Width = 110;
        btn.AutoSize = false;
        btn.Left = 0;
        btn.Top = 32;
        btn.Width = 180;
        btn.Height = 28;
        p.Controls.Add(nud);
        p.Controls.Add(btn);
        return p;
    }

    private static void AddRow(TableLayoutPanel t, string label, Control c)
    {
        t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 6, 3) }, 0, t.RowCount);
        t.Controls.Add(c, 1, t.RowCount);
        t.RowCount++;
    }

    private TabPage BuildStoragePage()
    {
        var page = new TabPage("Хранилище") { Padding = new Padding(8) };
        AddColumn(_gridPoly, "Id", "№", 50);
        AddColumn(_gridPoly, "FieldName", "Поле", 110);
        AddColumn(_gridPoly, "Degree", "Степень", 70);
        AddColumn(_gridPoly, "Source", "Источник", 90);
        AddColumn(_gridPoly, "Title", "Название", 120);
        AddColumn(_gridPoly, "CreatedAt", "Создан", 130);
        AddColumn(_gridPoly, "Preview", "Коэффициенты (начало записи)", 380);

        AddColumn(_gridOps, "Id", "№", 50);
        AddColumn(_gridOps, "PerformedAt", "Время", 130);
        AddColumn(_gridOps, "FieldName", "Поле", 110);
        AddColumn(_gridOps, "Operation", "Операция", 80);
        AddColumn(_gridOps, "DegreeA", "deg A", 60);
        AddColumn(_gridOps, "DegreeB", "deg B", 60);
        AddColumn(_gridOps, "Result", "Результат", 220);
        AddColumn(_gridOps, "Algorithm", "Алгоритм", 150);
        AddColumn(_gridOps, "ElapsedMs", "Время, мс", 80, "0.###");

        var polyButtons = ButtonRow(
            ("Загрузить в A", async () => await LoadFromStorageAsync(_tbA)),
            ("Загрузить в B", async () => await LoadFromStorageAsync(_tbB)),
            ("Удалить многочлен", DeletePolynomial),
            ("Обновить", () => { RefreshStorage(); return Task.CompletedTask; }));
        var opButtons = ButtonRow(
            ("Повторить операцию", RepeatOperationAsync),
            ("Удалить запись", DeleteOperation),
            ("Очистить журнал…", () => { ClearJournal(); return Task.CompletedTask; }));

        var gbPoly = new GroupBox { Text = "Многочлены", Dock = DockStyle.Fill, Padding = new Padding(8) };
        gbPoly.Controls.Add(_gridPoly);
        gbPoly.Controls.Add(polyButtons);
        var gbOps = new GroupBox { Text = "Журнал операций", Dock = DockStyle.Fill, Padding = new Padding(8) };
        gbOps.Controls.Add(_gridOps);
        gbOps.Controls.Add(opButtons);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        split.Panel1.Controls.Add(gbPoly);
        split.Panel2.Controls.Add(gbOps);
        page.Controls.Add(split);
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 26 };
        bottom.Controls.Add(_lblDbStats);
        page.Controls.Add(bottom);
        page.Enabled = _db is not null;
        page.Layout += (_, _) => { if (split.Height > 200) split.SplitterDistance = split.Height / 2; };
        return page;
    }

    private static FlowLayoutPanel ButtonRow(params (string Text, Func<Task> Action)[] buttons)
    {
        var p = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(0, 4, 0, 0) };
        foreach (var (text, action) in buttons)
        {
            var b = new Button { Text = text, AutoSize = true };
            b.Click += async (_, _) => await action();
            p.Controls.Add(b);
        }
        return p;
    }

    private static void AddColumn(DataGridView g, string prop, string header, int weight, string? format = null)
    {
        var col = new DataGridViewTextBoxColumn { DataPropertyName = prop, HeaderText = header, FillWeight = weight };
        if (format is not null) col.DefaultCellStyle.Format = format;
        if (prop.EndsWith("At")) col.DefaultCellStyle.Format = "dd.MM.yyyy HH:mm:ss";
        g.Columns.Add(col);
    }

    private TabPage BuildBenchPage()
    {
        var page = new TabPage("Производительность") { Padding = new Padding(8) };
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 6) };
        top.Controls.Add(new Label { Text = "Операция:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
        _cbBenchOp.Width = 280;
        top.Controls.Add(_cbBenchOp);
        top.Controls.Add(new Label { Text = "Наибольшая степень n:", AutoSize = true, Margin = new Padding(12, 7, 3, 3) });
        top.Controls.Add(_nudBenchMax);
        top.Controls.Add(new Label { Text = "Точек:", AutoSize = true, Margin = new Padding(12, 7, 3, 3) });
        top.Controls.Add(_nudBenchSteps);
        top.Controls.Add(_btnBench);
        _btnBench.Click += async (_, _) => await RunBenchmarkAsync();

        AddColumn(_gridBench, "Operation", "Операция", 80);
        AddColumn(_gridBench, "Algorithm", "Алгоритм", 120);
        AddColumn(_gridBench, "Size", "n", 60);
        AddColumn(_gridBench, "ElapsedMs", "Время, мс", 80, "0.###");

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 760 };
        split.Panel1.Controls.Add(_chart);
        split.Panel2.Controls.Add(_gridBench);
        page.Controls.Add(split);
        page.Controls.Add(top);
        page.Layout += (_, _) => { if (split.Width > 500) split.SplitterDistance = (int)(split.Width * 0.66); };
        return page;
    }

    // ============================================================ состояние формы

    private void UpdateState()
    {
        bool hasField = _field is not null;
        bool hasA = _tbA.TextLength > 0, hasB = _tbB.TextLength > 0, hasC = _tbC.TextLength > 0;
        bool idle = !_busy;
        _btnAdd.Enabled = _btnSub.Enabled = _btnMul.Enabled = _btnDiv.Enabled = idle && hasField && hasA && hasB;
        _btnEval.Enabled = idle && hasField && hasA && hasC;
        _btnRandA.Enabled = _btnRandB.Enabled = idle && hasField;
        _btnField.Enabled = idle && _tbP.TextLength > 0;
        _btnBench.Enabled = idle && hasField;
        _btnCancel.Enabled = _busy;
        _tbP.ReadOnly = _busy;
        _nudK.Enabled = idle;
        _tbModulus.ReadOnly = _busy;
        _cbMul.Enabled = _cbDiv.Enabled = idle;
        _stProgress.Visible = _busy;
        foreach (ToolStripMenuItem m in _menu.Items)
            if (m.Text != "&Справка") m.Enabled = idle && (m.Text != "&Хранилище" || _db is not null);
        _tabs.TabPages[1].Enabled = idle && _db is not null;
        _stField.Text = hasField ? $"Поле: {_field!.Name}" : "Поле не задано";
    }

    private void SetBusy(bool busy, string? message)
    {
        _busy = busy;
        _stMessage.Text = message ?? "";
        Cursor = busy ? Cursors.AppStarting : Cursors.Default;
        UpdateState();
    }

    private void UpdateFieldInfo()
    {
        bool stale = _field is null
                     || _nudK.Value != _field.ExtensionDegree
                     || !SameCharacteristic();
        if (stale)
        {
            _lblFieldInfo.ForeColor = Color.DarkOrange;
            _lblFieldInfo.Text = _field is null
                ? "Задайте характеристику p и степень расширения k и нажмите «Создать поле»."
                : $"Параметры изменены — нажмите «Создать поле». Сейчас используется {_field.Name}.";
        }
        else
        {
            _lblFieldInfo.ForeColor = Color.DarkGreen;
            var f = _field!;
            _lblFieldInfo.Text = $"Поле {f.Name} создано: q = {GaloisField.ShortNumber(f.Order)}" +
                                 (f.ExtensionDegree > 1 ? $", f(x) = {f.Modulus}" : "") +
                                 $"; представление: {f.Implementation}.";
        }
    }

    private bool SameCharacteristic()
    {
        try
        {
            return GFPolyLib.Fields.NumberTheory.ParseIntegerExpression(_tbP.Text) == _field!.Characteristic;
        }
        catch (GfException)
        {
            return false;
        }
    }

    // ================================================================= ввод данных

    private bool _settingText;

    private void SetText(TextBox tb, string text)
    {
        _settingText = true;
        try
        {
            tb.Text = text;
        }
        finally
        {
            _settingText = false;
        }
    }

    private void FilterPolynomialKeys(KeyPressEventArgs e)
    {
        string allowed = "0123456789xX^+-*() ,;\r\n·−";
        if (_field is { ExtensionDegree: > 1 }) allowed += "aA";
        FilterKeys(e, allowed);
    }

    private void FilterKeys(KeyPressEventArgs e, string allowed)
    {
        if (char.IsControl(e.KeyChar) || allowed.Contains(e.KeyChar)) return;
        e.Handled = true;
        System.Media.SystemSounds.Beep.Play();
        _stMessage.Text = $"Символ «{e.KeyChar}» недопустим в этом поле ввода.";
    }

    private void OnInputChanged()
    {
        UpdateState();
        _validateTimer.Stop();
        _validateTimer.Start();
    }

    /// <summary>Проверка записей во время ввода: ошибка отмечается значком рядом с полем.</summary>
    private void LiveValidate()
    {
        if (_field is null) return;
        Check(_tbA, t => _field.Parse(t));
        Check(_tbB, t => _field.Parse(t));
        Check(_tbC, t => _field.NormalizeElement(t));

        void Check(TextBox tb, Action<string> parse)
        {
            if (tb.TextLength == 0 || tb.TextLength > LiveCheckLimit)
            {
                _errors.SetError(tb, "");
                return;
            }
            try
            {
                parse(tb.Text);
                _errors.SetError(tb, "");
            }
            catch (GfException ex)
            {
                _errors.SetError(tb, ex.Message);
            }
        }
    }

    // =================================================================== поле

    private async Task CreateFieldAsync()
    {
        string p = _tbP.Text, modulus = _tbModulus.Text;
        int k = (int)_nudK.Value;
        SetBusy(true, "Проверка простоты p и построение поля…");
        try
        {
            var sw = Stopwatch.StartNew();
            var field = await Task.Run(() => GaloisField.Create(p, k, k > 1 && !string.IsNullOrWhiteSpace(modulus) ? modulus : null));
            _field = field;
            _errors.SetError(_tbP, "");
            _errors.SetError(_tbModulus, "");
            _tbModulus.Text = field.Modulus;
            ApplyAlgorithms();
            _stMessage.Text = $"Поле {field.Name} создано за {sw.Elapsed.TotalMilliseconds:F1} мс.";
        }
        catch (GfException ex)
        {
            _errors.SetError(ex.Code == GfError.InvalidModulus ? _tbModulus : _tbP, ex.Message);
            ShowError(ex.Message);
            _stMessage.Text = "Поле не создано.";
        }
        finally
        {
            SetBusy(false, _stMessage.Text);
            UpdateFieldInfo();
            LiveValidate();
        }
    }

    private void ApplyAlgorithms()
    {
        if (_field is null) return;
        _field.Options.Multiplication = (MulAlgorithm)_cbMul.SelectedIndex;
        _field.Options.Division = (DivAlgorithm)_cbDiv.SelectedIndex;
    }

    private bool EnsureFieldCurrent()
    {
        if (_field is null)
        {
            ShowError("Сначала создайте поле Галуа.");
            return false;
        }
        if (_nudK.Value != _field.ExtensionDegree || !SameCharacteristic())
        {
            if (!Ask($"Параметры поля в форме изменены, но поле не пересоздано.\nВыполнить операцию в прежнем поле {_field.Name}?"))
                return false;
        }
        return true;
    }

    // ============================================================== операции

    private async Task RandomAsync(TextBox target, int degree, bool isA)
    {
        if (_field is null) return;
        var field = _field;
        SetBusy(true, "Генерация многочлена…");
        try
        {
            int seed = Environment.TickCount;
            string text = await Task.Run(() => field.Random(degree, seed).ToCoefficients());
            SetText(target, text);
            if (isA) _sourceA = "random";
            else _sourceB = "random";
            _stMessage.Text = $"Сгенерирован многочлен степени {degree}.";
        }
        catch (GfException ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            SetBusy(false, _stMessage.Text);
        }
    }

    private async Task RunOperationAsync(string op)
    {
        if (!EnsureFieldCurrent()) return;
        var field = _field!;
        string textA = _tbA.Text, textB = _tbB.Text, textC = _tbC.Text;
        string srcA = _sourceA, srcB = _sourceB;
        bool log = _chkLog.Checked && _db is not null;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        SetBusy(true, "Выполняется операция… (можно прервать)");
        try
        {
            var result = await Task.Run(() =>
            {
                Polynomial a = ParseOperand(field, textA, "A");
                Polynomial? b = op == "eval" ? null : ParseOperand(field, textB, "B");
                var sw = Stopwatch.StartNew();
                Polynomial? r = null, rem = null;
                string? value = null, algorithm;
                switch (op)
                {
                    case "add":
                        r = a + b!;
                        algorithm = "покоэффициентное сложение";
                        break;
                    case "sub":
                        r = a - b!;
                        algorithm = "покоэффициентное вычитание";
                        break;
                    case "mul":
                        algorithm = field.Options.ResolveMultiplication(a.Degree + 1, b!.Degree + 1) == MulAlgorithm.School
                            ? "школьный" : $"Карацубы (n0 = {field.Options.KaratsubaThreshold})";
                        r = a.Multiply(b, ct);
                        break;
                    case "divmod":
                        algorithm = field.Options.ResolveDivision(a.Degree + 1, Math.Max(1, b!.Degree + 1)) == DivAlgorithm.Newton
                            ? "Ньютона (обращение ряда)" : "«уголком»";
                        (r, rem) = a.DivRem(b, ct);
                        break;
                    default:
                        algorithm = "схема Горнера";
                        value = a.Evaluate(textC, ct);
                        break;
                }
                sw.Stop();
                double ms = sw.Elapsed.TotalMilliseconds;

                var inputs = new List<(string, string)> { ("A(x)", a.ToAlgebraic()) };
                if (b is not null) inputs.Add(("B(x)", b.ToAlgebraic()));
                if (op == "eval") inputs.Add(("c", field.NormalizeElement(textC)));
                var outputs = new List<(string, string)>();
                switch (op)
                {
                    case "add": outputs.Add(("A + B", r!.ToAlgebraic())); break;
                    case "sub": outputs.Add(("A − B", r!.ToAlgebraic())); break;
                    case "mul": outputs.Add(("A · B", r!.ToAlgebraic())); break;
                    case "divmod":
                        outputs.Add(("Q(x)", r!.ToAlgebraic()));
                        outputs.Add(("R(x)", rem!.ToAlgebraic()));
                        break;
                    default: outputs.Add(($"A({field.NormalizeElement(textC)})", value!)); break;
                }
                if (r is not null) outputs.Add(("степень результата", r.Degree.ToString()));

                if (log)
                    _db!.LogOperation(op, a, srcA, b, srcB, op == "eval" ? field.NormalizeElement(textC) : null,
                        r, rem, value, algorithm, ms);

                return new OperationResult
                {
                    Title = OperationTitle(op), FieldDescription = FieldDescription(field), Inputs = inputs,
                    Outputs = outputs, Algorithm = algorithm, ElapsedMs = ms,
                };
            }, ct);
            _results.Add(result);
            AppendResult(result, _results.Count);
            _stMessage.Text = $"{result.Title}: выполнено за {result.ElapsedMs:F3} мс ({result.Algorithm}).";
            UpdateDbStatus();
        }
        catch (GfException ex) when (ex.Code == GfError.Cancelled)
        {
            _stMessage.Text = "Операция прервана пользователем.";
        }
        catch (OperationCanceledException)
        {
            _stMessage.Text = "Операция прервана пользователем.";
        }
        catch (OperandException ex)
        {
            _errors.SetError(ex.Name == "A" ? _tbA : _tbB, ex.Message);
            ShowError($"Ошибка в записи многочлена {ex.Name}: {ex.Message}");
        }
        catch (GfException ex)
        {
            if (ex.Code is GfError.ParseError or GfError.CoefficientOutOfRange && op == "eval") _errors.SetError(_tbC, ex.Message);
            ShowError(ex.Message);
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex)
        {
            ShowError("Операция выполнена, но запись в журнал хранилища не удалась: " + ex.Message);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetBusy(false, _stMessage.Text);
        }
    }

    private sealed class OperandException(string name, string message) : Exception(message)
    {
        public string Name { get; } = name;
    }

    private static Polynomial ParseOperand(GaloisField f, string text, string name)
    {
        try
        {
            return f.Parse(text);
        }
        catch (GfException ex) when (ex.Code is GfError.ParseError or GfError.CoefficientOutOfRange or GfError.TooLarge)
        {
            throw new OperandException(name, ex.Message);
        }
    }

    private static string OperationTitle(string op) => op switch
    {
        "add" => "Сложение A + B",
        "sub" => "Вычитание A − B",
        "mul" => "Умножение A · B",
        "divmod" => "Деление с остатком A = B·Q + R",
        _ => "Значение многочлена A(c)",
    };

    private static string FieldDescription(GaloisField f) =>
        f.ExtensionDegree == 1 ? $"{f.Name}, p = {GaloisField.ShortNumber(f.Characteristic)}"
            : $"{f.Name}, f(x) = {f.Modulus}";

    private void AppendResult(OperationResult r, int number)
    {
        var sb = new StringBuilder();
        if (_tbResult.TextLength > 0) sb.AppendLine().AppendLine(new string('─', 80));
        sb.AppendLine($"{number}. {r.Title}    [{r.Time:HH:mm:ss}]");
        sb.AppendLine($"   Поле: {r.FieldDescription}");
        foreach (var (n, v) in r.Inputs) sb.AppendLine($"   {n} = {ReportBuilder.Cut(v, DisplayLimit)}");
        foreach (var (n, v) in r.Outputs) sb.AppendLine($"   {n} = {ReportBuilder.Cut(v, DisplayLimit)}");
        sb.Append($"   Алгоритм: {r.Algorithm}; время: {r.ElapsedMs:F3} мс");
        _tbResult.AppendText(sb.ToString().Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"));
    }

    private void ClearResults()
    {
        _results.Clear();
        _tbResult.Clear();
    }

    // ============================================================= файлы и печать

    private void OpenTaskFile()
    {
        using var dlg = new OpenFileDialog { Filter = TaskFile.Filter, Title = "Открыть исходные данные" };
        string samples = Path.Combine(AppContext.BaseDirectory, "samples");
        if (Directory.Exists(samples)) dlg.InitialDirectory = samples;
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _ = LoadTaskFileAsync(dlg.FileName);
    }

    private async Task LoadTaskFileAsync(string path)
    {
        try
        {
            TaskData data = await Task.Run(() => TaskFile.Load(path));
            _tbP.Text = data.P;
            _nudK.Value = Math.Clamp(data.K, 1, GaloisField.MaxExtensionDegree);
            _tbModulus.Text = data.Modulus;
            SetText(_tbA, data.A);
            SetText(_tbB, data.B);
            _tbC.Text = data.C;
            _sourceA = _sourceB = "file";
            _lastFile = path;
            _stMessage.Text = $"Загружен файл {Path.GetFileName(path)}.";
            await CreateFieldAsync();
        }
        catch (TaskFileException ex)
        {
            ShowError($"Файл «{Path.GetFileName(path)}» имеет неверный формат.\n{ex.Message}");
        }
        catch (IOException ex)
        {
            ShowError("Не удалось прочитать файл: " + ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowError("Нет доступа к файлу: " + ex.Message);
        }
    }

    private void SaveTaskFile()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = TaskFile.Filter, Title = "Сохранить исходные данные", DefaultExt = "gfp",
            FileName = _lastFile is null ? "задача.gfp" : Path.GetFileName(_lastFile),
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            TaskFile.Save(dlg.FileName, new TaskData
            {
                P = _tbP.Text.Trim(), K = (int)_nudK.Value, Modulus = _nudK.Value > 1 ? _tbModulus.Text.Trim() : "",
                A = _tbA.Text.ReplaceLineEndings(" ").Trim(), B = _tbB.Text.ReplaceLineEndings(" ").Trim(), C = _tbC.Text.Trim(),
            });
            _lastFile = dlg.FileName;
            _stMessage.Text = $"Исходные данные сохранены в {Path.GetFileName(dlg.FileName)}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError("Не удалось сохранить файл: " + ex.Message);
        }
    }

    private void SaveResults()
    {
        if (_results.Count == 0)
        {
            ShowInfo("Нет результатов для сохранения — выполните хотя бы одну операцию.");
            return;
        }
        using var dlg = new SaveFileDialog
        {
            Filter = "Текстовый файл (*.txt)|*.txt", Title = "Сохранить результаты", DefaultExt = "txt",
            FileName = $"результаты_{DateTime.Now:yyyyMMdd_HHmm}.txt",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dlg.FileName, ReportBuilder.Build(_results), new UTF8Encoding(true));
            _stMessage.Text = $"Результаты сохранены в {Path.GetFileName(dlg.FileName)} (полностью, без сокращений).";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError("Не удалось сохранить файл: " + ex.Message);
        }
    }

    private void Print(bool preview)
    {
        if (_results.Count == 0)
        {
            ShowInfo("Нет результатов для печати — выполните хотя бы одну операцию.");
            return;
        }
        string text = ReportBuilder.Build(_results, maxValueLength: 3000);
        int pages = TextPrinter.EstimatePages(text);
        if (pages > 20 && !Ask($"Отчёт займёт около {pages} страниц. Длинные многочлены сокращены до 3000 символов. Продолжить?"))
            return;
        var printer = new TextPrinter(text, "Отчёт о вычислениях");
        try
        {
            if (preview)
            {
                using var dlg = new PrintPreviewDialog { Document = printer.Document, Width = 900, Height = 700 };
                dlg.ShowDialog(this);
            }
            else
            {
                using var dlg = new PrintDialog { Document = printer.Document, UseEXDialog = true };
                if (dlg.ShowDialog(this) == DialogResult.OK) printer.Document.Print();
            }
        }
        catch (InvalidPrinterException ex)
        {
            ShowError("Принтер недоступен: " + ex.Message);
        }
    }

    // ================================================================= хранилище

    private void UpdateDbStatus()
    {
        if (_db is null)
        {
            _stDb.Text = "Хранилище недоступно";
            return;
        }
        try
        {
            var s = _db.Statistics();
            _stDb.Text = $"Хранилище: {s.Polynomials} многочл., {s.Operations} опер., {s.Bytes / 1024.0:F0} КБ";
            _lblDbStats.Text = $"Файл: {_db.Path}   Полей: {s.Fields}, многочленов: {s.Polynomials}, " +
                               $"операций: {s.Operations}, замеров: {s.Benchmarks}, размер: {s.Bytes / 1024.0:F0} КБ";
        }
        catch (Exception ex)
        {
            _stDb.Text = "Ошибка хранилища: " + ex.Message;
        }
    }

    private void RefreshStorage()
    {
        if (_db is null) return;
        try
        {
            _gridPoly.DataSource = _db.GetPolynomials();
            _gridOps.DataSource = _db.GetOperations();
            UpdateDbStatus();
        }
        catch (Exception ex)
        {
            ShowError("Ошибка чтения хранилища: " + ex.Message);
        }
    }

    private void SaveToStorage(TextBox source, string name)
    {
        if (_db is null || !EnsureFieldCurrent()) return;
        if (source.TextLength == 0)
        {
            ShowInfo($"Многочлен {name} не задан.");
            return;
        }
        string? title = Prompt($"Название многочлена {name} в хранилище:", $"Многочлен {name}");
        if (title is null) return;
        try
        {
            var p = _field!.Parse(source.Text);
            long id = _db.SavePolynomial(p, name == "A" ? _sourceA : _sourceB, title.Length == 0 ? null : title);
            _stMessage.Text = $"Многочлен {name} сохранён в хранилище под номером {id}.";
            UpdateDbStatus();
        }
        catch (GfException ex)
        {
            ShowError($"Многочлен {name} записан с ошибкой: {ex.Message}");
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex)
        {
            ShowError("Ошибка записи в хранилище: " + ex.Message);
        }
    }

    private async Task LoadFromStorageAsync(TextBox target)
    {
        if (_db is null || _gridPoly.CurrentRow?.DataBoundItem is not StoredPolynomial sp)
        {
            ShowInfo("Выберите многочлен в таблице.");
            return;
        }
        if (!await SwitchToStoredFieldAsync(sp.FieldId)) return;
        var (_, coefficients) = _db.LoadPolynomial(sp.Id);
        SetText(target, coefficients);
        if (target == _tbA) _sourceA = "file";
        else _sourceB = "file";
        _tabs.SelectedIndex = 0;
        _stMessage.Text = $"Многочлен №{sp.Id} загружен из хранилища.";
    }

    /// <summary>Если многочлен задан над другим полем, предлагает перейти к этому полю.</summary>
    private async Task<bool> SwitchToStoredFieldAsync(long fieldId)
    {
        var sf = _db!.GetField(fieldId);
        if (sf is null) return false;
        if (_field is not null && _field.Characteristic.ToString() == sf.Characteristic &&
            _field.ExtensionDegree == sf.ExtensionDegree && _field.Modulus == sf.Modulus)
            return true;
        if (!Ask($"Многочлен задан над полем {GfDatabase.FieldName(sf.Characteristic, sf.ExtensionDegree)}. Перейти к этому полю?"))
            return false;
        _tbP.Text = sf.Characteristic;
        _nudK.Value = sf.ExtensionDegree;
        _tbModulus.Text = sf.Modulus;
        await CreateFieldAsync();
        return _field is not null && _field.Characteristic.ToString() == sf.Characteristic;
    }

    private Task DeletePolynomial()
    {
        if (_db is null || _gridPoly.CurrentRow?.DataBoundItem is not StoredPolynomial sp)
        {
            ShowInfo("Выберите многочлен в таблице.");
            return Task.CompletedTask;
        }
        if (!Ask($"Удалить многочлен №{sp.Id}? Записи журнала, в которых он является операндом, тоже будут удалены."))
            return Task.CompletedTask;
        _db.DeletePolynomial(sp.Id);
        RefreshStorage();
        return Task.CompletedTask;
    }

    private async Task RepeatOperationAsync()
    {
        if (_db is null || _gridOps.CurrentRow?.DataBoundItem is not OperationRecord op)
        {
            ShowInfo("Выберите запись журнала.");
            return;
        }
        if (!await SwitchToStoredFieldAsync(op.FieldId)) return;
        SetText(_tbA, _db.LoadPolynomial(op.OperandA).Coefficients);
        SetText(_tbB, op.OperandB is long b ? _db.LoadPolynomial(b).Coefficients : "");
        _tbC.Text = op.Point ?? "";
        _sourceA = _sourceB = "file";
        _tabs.SelectedIndex = 0;
        string code = op.Operation switch
        {
            "A + B" => "add", "A − B" => "sub", "A × B" => "mul", "A ÷ B" => "divmod", _ => "eval",
        };
        await RunOperationAsync(code);
    }

    private Task DeleteOperation()
    {
        if (_db is null || _gridOps.CurrentRow?.DataBoundItem is not OperationRecord op)
        {
            ShowInfo("Выберите запись журнала.");
            return Task.CompletedTask;
        }
        if (!Ask($"Удалить запись журнала №{op.Id}?")) return Task.CompletedTask;
        _db.DeleteOperation(op.Id);
        RefreshStorage();
        return Task.CompletedTask;
    }

    private void ClearJournal()
    {
        if (_db is null) return;
        if (!Ask("Очистить журнал операций и замеров? Будут удалены также многочлены без названия. Действие необратимо."))
            return;
        try
        {
            _db.ClearJournal();
            RefreshStorage();
            _stMessage.Text = "Журнал очищен.";
        }
        catch (Exception ex)
        {
            ShowError("Не удалось очистить журнал: " + ex.Message);
        }
    }

    // ======================================================= замер производительности

    private async Task RunBenchmarkAsync()
    {
        if (!EnsureFieldCurrent()) return;
        var field = _field!;
        bool mul = _cbBenchOp.SelectedIndex == 0;
        int max = (int)_nudBenchMax.Value, steps = (int)_nudBenchSteps.Value;
        if (field.ExtensionDegree > 1 && max > 3000 &&
            !Ask("Для полей-расширений замер при больших степенях может занять несколько минут. Продолжить?"))
            return;
        _chart.Clear();
        _chart.XTitle = mul ? "Степень сомножителей n" : "Степень делителя n (делимое — 2n)";
        var rows = new List<BenchmarkRecord>();
        _gridBench.DataSource = null;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        SetBusy(true, "Выполняется замер производительности…");
        var opts = field.Options.Clone();
        try
        {
            for (int i = 1; i <= steps; i++)
            {
                int n = Math.Max(2, max * i / steps);
                var measured = await Task.Run(() => Measure(field, mul, n, ct), ct);
                foreach (var r in measured)
                {
                    rows.Add(r);
                    _chart.AddPoint(r.Algorithm, r.Size, (float)r.ElapsedMs);
                }
                _gridBench.DataSource = rows.ToList();
                _stMessage.Text = $"Замер: выполнено {i} из {steps}.";
            }
            _db?.SaveBenchmarks(field, rows);
            _stMessage.Text = "Замер завершён" + (_db is null ? "." : " и сохранён в хранилище.");
        }
        catch (Exception ex) when (ex is OperationCanceledException or GfException { Code: GfError.Cancelled })
        {
            _stMessage.Text = "Замер прерван.";
        }
        catch (GfException ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            field.Options.Multiplication = opts.Multiplication;
            field.Options.Division = opts.Division;
            _cts.Dispose();
            _cts = null;
            SetBusy(false, _stMessage.Text);
            UpdateDbStatus();
        }
    }

    private static List<BenchmarkRecord> Measure(GaloisField f, bool mul, int n, CancellationToken ct)
    {
        var list = new List<BenchmarkRecord>();
        var a = f.Random(mul ? n : 2 * n, n);
        var b = f.Random(n, n + 1);
        if (mul)
        {
            foreach (var (alg, name) in new[] { (MulAlgorithm.School, "школьный"), (MulAlgorithm.Karatsuba, "Карацубы") })
            {
                f.Options.Multiplication = alg;
                list.Add(new BenchmarkRecord("mul", name, n, BestOf(() => a.Multiply(b, ct))));
            }
        }
        else
        {
            f.Options.Multiplication = MulAlgorithm.Auto;
            foreach (var (alg, name) in new[] { (DivAlgorithm.Long, "«уголком»"), (DivAlgorithm.Newton, "Ньютона") })
            {
                f.Options.Division = alg;
                list.Add(new BenchmarkRecord("divmod", name, n, BestOf(() => a.DivRem(b, ct))));
            }
        }
        return list;
    }

    /// <summary>Наименьшее время из трёх запусков после прогрева (снижает влияние JIT и сборки мусора).</summary>
    private static double BestOf(Action action)
    {
        action();
        double best = double.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            var sw = Stopwatch.StartNew();
            action();
            best = Math.Min(best, sw.Elapsed.TotalMilliseconds);
        }
        return best;
    }

    // ================================================================== прочее

    private void ShowHelp()
    {
        string text;
        using (var s = typeof(MainForm).Assembly.GetManifestResourceStream("GFPolyApp.UserGuide.txt"))
        using (var r = new StreamReader(s!, Encoding.UTF8))
            text = r.ReadToEnd();
        var form = new Form
        {
            Text = "Руководство пользователя GFPolyLib", Size = new Size(820, 680), StartPosition = FormStartPosition.CenterParent,
            ShowInTaskbar = false, MinimizeBox = false,
        };
        var box = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Text = text.ReplaceLineEndings("\r\n"),
            Font = new Font("Segoe UI", 10f), BackColor = Color.White,
        };
        form.Controls.Add(box);
        form.Shown += (_, _) => box.Select(0, 0);
        form.Show(this);
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_screenshotDir is not null || !_busy) return;
        if (Ask("Выполняется вычисление. Прервать его и закрыть программу?")) _cts?.Cancel();
        else e.Cancel = true;
    }

    private bool Ask(string question) =>
        _screenshotDir is not null ||
        MessageBox.Show(this, question, "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    private void ShowError(string message)
    {
        if (_screenshotDir is null) MessageBox.Show(this, message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        else _stMessage.Text = message;
    }

    private void ShowInfo(string message) => MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);

    private string? Prompt(string question, string initial)
    {
        if (_screenshotDir is not null) return initial;
        using var form = new Form
        {
            Text = "Хранилище", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(420, 120), MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, Font = Font,
        };
        var lbl = new Label { Text = question, Left = 12, Top = 12, AutoSize = true };
        var tb = new TextBox { Left = 12, Top = 38, Width = 396, Text = initial, MaxLength = 200 };
        var ok = new Button { Text = "Сохранить", DialogResult = DialogResult.OK, Left = 222, Top = 78, Width = 90 };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Left = 318, Top = 78, Width = 90 };
        form.Controls.AddRange(new Control[] { lbl, tb, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return form.ShowDialog(this) == DialogResult.OK ? tb.Text.Trim() : null;
    }
}
