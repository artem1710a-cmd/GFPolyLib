using GFPolyApp.Storage;
using GFPolyApp.UI;

namespace GFPolyApp;

internal static class Program
{
    /// <summary>
    /// Точка входа. Необязательные аргументы:
    /// --db &lt;файл&gt; — использовать указанный файл хранилища;
    /// --screenshots &lt;папка&gt; — сформировать снимки окон для документации и завершить работу.
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowFatal(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ShowFatal(e.ExceptionObject as Exception);

        string? dbPath = null, shots = null;
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "--db") dbPath = args[++i];
            else if (args[i] == "--screenshots") shots = args[++i];
        }
        if (shots is not null) dbPath ??= Path.Combine(shots, "screens.db");

        GfDatabase? db = null;
        string? dbError = null;
        try
        {
            db = new GfDatabase(dbPath ?? GfDatabase.DefaultPath);
        }
        catch (Exception ex)
        {
            dbError = ex.Message;
        }

        Application.Run(new MainForm(db, dbError, shots));
    }

    private static void ShowFatal(Exception? ex)
    {
        MessageBox.Show(
            "Произошла непредвиденная ошибка. Введённые данные не потеряны, работу можно продолжить.\n\n" +
            (ex?.Message ?? "нет сведений"),
            "GFPolyLib", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
