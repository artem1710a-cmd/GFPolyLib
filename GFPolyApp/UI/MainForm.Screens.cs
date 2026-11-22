namespace GFPolyApp.UI;

/// <summary>
/// Режим автоматического формирования иллюстраций для пояснительной записки (аргумент --screenshots).
/// Выполняет типовой сценарий работы через те же обработчики, что и пользователь, и сохраняет снимки окна.
/// </summary>
public sealed partial class MainForm
{
    private async Task RunScreenshotScenarioAsync(string dir)
    {
        Directory.CreateDirectory(dir);
        try
        {
            // 1. Контрольный пример в GF(7)
            _tbP.Text = "7";
            _nudK.Value = 1;
            await CreateFieldAsync();
            SetText(_tbA, "3x^4 + 5x^3 + x + 6");
            SetText(_tbB, "2x^2 + 4x + 1");
            _tbC.Text = "2";
            foreach (var op in new[] { "add", "sub", "mul", "divmod", "eval" }) await RunOperationAsync(op);
            await Snap(dir, "01_calc_gf7");

            // 2. Защита от ошибочного ввода: составная характеристика и коэффициент вне поля
            _tbP.Text = "91";
            await CreateFieldAsync();
            _tbP.Text = "7";
            await CreateFieldAsync();
            SetText(_tbA, "3x^4 + 9x^3 + x + 6");
            LiveValidate();
            _errors.SetError(_tbP, "Число 91 составное: характеристика поля Галуа должна быть простым числом.");
            await Snap(dir, "02_errors");
            _errors.Clear();

            // 3. Поле-расширение GF(2^8) с автоматическим подбором модуля
            ClearResults();
            _tbP.Text = "2";
            _nudK.Value = 8;
            _tbModulus.Text = "";
            await CreateFieldAsync();
            SetText(_tbA, "(a^6 + a^4 + a + 1)x^3 + (a)x^2 + 1");
            SetText(_tbB, "(a^7 + a^6 + a^3 + a)x + (a^2)");
            _tbC.Text = "a^7 + a^6 + a^3 + a";
            foreach (var op in new[] { "mul", "divmod", "eval" }) await RunOperationAsync(op);
            await Snap(dir, "03_calc_gf2_8");

            // 4. Большая характеристика p = 2^127 − 1 и многочлены высокой степени
            ClearResults();
            _tbP.Text = "2^127-1";
            _nudK.Value = 1;
            await CreateFieldAsync();
            _nudRandA.Value = 3000;
            _nudRandB.Value = 1200;
            await RandomAsync(_tbA, 3000, isA: true);
            await RandomAsync(_tbB, 1200, isA: false);
            _tbC.Text = "123456789012345678901234567890";
            foreach (var op in new[] { "mul", "divmod", "eval" }) await RunOperationAsync(op);
            await Snap(dir, "04_calc_big");

            // 5. Хранилище
            _tabs.SelectedIndex = 1;
            RefreshStorage();
            await Snap(dir, "05_storage");

            // 6. Замеры производительности
            _tabs.SelectedIndex = 0;
            _tbP.Text = "998244353";
            _nudK.Value = 1;
            await CreateFieldAsync();
            _tabs.SelectedIndex = 2;
            _cbBenchOp.SelectedIndex = 0;
            _nudBenchMax.Value = 6000;
            _nudBenchSteps.Value = 8;
            await RunBenchmarkAsync();
            await Snap(dir, "06_bench_mul");
            _cbBenchOp.SelectedIndex = 1;
            _nudBenchMax.Value = 6000;
            await RunBenchmarkAsync();
            await Snap(dir, "07_bench_div");

            // 7. Справка
            ShowHelp();
            await Task.Delay(400);
            var help = OwnedForms.LastOrDefault();
            if (help is not null)
            {
                SnapForm(help, dir, "08_help");
                help.Close();
            }
        }
        finally
        {
            Close();
        }
    }

    private async Task Snap(string dir, string name)
    {
        await Task.Delay(300);
        SnapForm(this, dir, name);
    }

    private static void SnapForm(Form f, string dir, string name)
    {
        f.Refresh();
        using var bmp = new Bitmap(f.Width, f.Height);
        f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
        bmp.Save(Path.Combine(dir, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
    }
}
