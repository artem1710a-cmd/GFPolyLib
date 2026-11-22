using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GFPolyLib;
using Microsoft.Data.Sqlite;

namespace GFPolyApp.Storage;

/// <summary>Запись журнала операций для отображения в таблице.</summary>
public sealed record OperationRecord(
    long Id, DateTime PerformedAt, string FieldName, string Operation,
    int DegreeA, int? DegreeB, string Result, string Algorithm, double ElapsedMs,
    long FieldId, long OperandA, long? OperandB, string? Point);

/// <summary>Сохранённый многочлен (для таблицы хранилища).</summary>
public sealed record StoredPolynomial(long Id, long FieldId, string FieldName, int Degree, string Source, string? Title,
    DateTime CreatedAt, string Preview);

/// <summary>Параметры поля, сохранённые в хранилище.</summary>
public sealed record StoredField(long Id, string Characteristic, int ExtensionDegree, string Modulus);

/// <summary>Результат замера производительности.</summary>
public sealed record BenchmarkRecord(string Operation, string Algorithm, int Size, double ElapsedMs);

/// <summary>
/// Реляционное хранилище приложения на основе встраиваемой СУБД SQLite.
/// Схема (третья нормальная форма): поля → многочлены → операции; отдельно — замеры производительности.
/// Одинаковые многочлены над одним полем хранятся в одном экземпляре (уникальный индекс по хешу коэффициентов).
/// </summary>
public sealed class GfDatabase
{
    public const int SchemaVersion = 2;
    private readonly string _connectionString;

    public string Path { get; }

    public GfDatabase(string path)
    {
        Path = path;
        string? dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
        if (dir is not null) Directory.CreateDirectory(dir);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = true,
        }.ToString();
        EnsureSchema();
    }

    public static string DefaultPath =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GFPolyLib", "gfpoly.db");

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_connectionString);
        c.Open();
        return c;
    }

    private void EnsureSchema()
    {
        using var c = Open();
        using (var v = c.CreateCommand())
        {
            v.CommandText = "PRAGMA user_version;";
            long ver = (long)v.ExecuteScalar()!;
            if (ver != 0 && ver != SchemaVersion)
            {
                // Схема прежней версии: таблицы пересоздаются (данные журнала не являются критичными).
                v.CommandText = "DROP TABLE IF EXISTS operations; DROP TABLE IF EXISTS benchmarks; " +
                                "DROP TABLE IF EXISTS polynomials; DROP TABLE IF EXISTS fields;";
                v.ExecuteNonQuery();
            }
        }
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"""
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS fields (
                id             INTEGER PRIMARY KEY AUTOINCREMENT,
                characteristic TEXT    NOT NULL,
                ext_degree     INTEGER NOT NULL CHECK (ext_degree BETWEEN 1 AND 256),
                modulus        TEXT    NOT NULL DEFAULT '',
                created_at     TEXT    NOT NULL DEFAULT (datetime('now', 'localtime')),
                UNIQUE (characteristic, ext_degree, modulus)
            );
            CREATE TABLE IF NOT EXISTS polynomials (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                field_id     INTEGER NOT NULL REFERENCES fields(id) ON DELETE CASCADE,
                degree       INTEGER NOT NULL CHECK (degree >= -1),
                coefficients TEXT    NOT NULL,
                coeff_hash   TEXT    NOT NULL,
                source       TEXT    NOT NULL CHECK (source IN ('keyboard', 'file', 'random', 'result')),
                title        TEXT,
                created_at   TEXT    NOT NULL DEFAULT (datetime('now', 'localtime')),
                UNIQUE (field_id, coeff_hash)
            );
            CREATE TABLE IF NOT EXISTS operations (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                op_code      TEXT    NOT NULL CHECK (op_code IN ('add', 'sub', 'mul', 'divmod', 'eval')),
                operand_a    INTEGER NOT NULL REFERENCES polynomials(id) ON DELETE CASCADE,
                operand_b    INTEGER REFERENCES polynomials(id) ON DELETE CASCADE,
                point        TEXT,
                result_id    INTEGER REFERENCES polynomials(id) ON DELETE SET NULL,
                remainder_id INTEGER REFERENCES polynomials(id) ON DELETE SET NULL,
                value        TEXT,
                algorithm    TEXT    NOT NULL,
                elapsed_ms   REAL    NOT NULL CHECK (elapsed_ms >= 0),
                performed_at TEXT    NOT NULL DEFAULT (datetime('now', 'localtime'))
            );
            CREATE TABLE IF NOT EXISTS benchmarks (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                field_id    INTEGER NOT NULL REFERENCES fields(id) ON DELETE CASCADE,
                op_code     TEXT    NOT NULL CHECK (op_code IN ('mul', 'divmod')),
                algorithm   TEXT    NOT NULL,
                size        INTEGER NOT NULL CHECK (size > 0),
                elapsed_ms  REAL    NOT NULL CHECK (elapsed_ms >= 0),
                measured_at TEXT    NOT NULL DEFAULT (datetime('now', 'localtime'))
            );
            CREATE INDEX IF NOT EXISTS ix_polynomials_field ON polynomials(field_id);
            CREATE INDEX IF NOT EXISTS ix_operations_time   ON operations(performed_at);
            CREATE INDEX IF NOT EXISTS ix_operations_a      ON operations(operand_a);
            CREATE INDEX IF NOT EXISTS ix_benchmarks_field  ON benchmarks(field_id, op_code);
            PRAGMA user_version = {SchemaVersion};
            """;
        cmd.ExecuteNonQuery();
    }

    // ------------------------------------------------------------------ поля

    private static long SaveField(SqliteConnection c, SqliteTransaction tx, GaloisField f)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO fields (characteristic, ext_degree, modulus) VALUES ($p, $k, $m)
            ON CONFLICT (characteristic, ext_degree, modulus) DO UPDATE SET modulus = excluded.modulus
            RETURNING id;
            """;
        cmd.Parameters.AddWithValue("$p", f.Characteristic.ToString(CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$k", f.ExtensionDegree);
        cmd.Parameters.AddWithValue("$m", f.Modulus);
        return (long)cmd.ExecuteScalar()!;
    }

    public StoredField? GetField(long id)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id, characteristic, ext_degree, modulus FROM fields WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? new StoredField(r.GetInt64(0), r.GetString(1), r.GetInt32(2), r.GetString(3)) : null;
    }

    // ------------------------------------------------------------ многочлены

    private static string Hash(string coefficients) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(coefficients)));

    private static long SavePolynomial(SqliteConnection c, SqliteTransaction tx, long fieldId, Polynomial p,
        string source, string? title)
    {
        string coefficients = p.ToCoefficients();
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        // Повторно встретившийся многочлен не дублируется: возвращается существующая запись.
        cmd.CommandText = """
            INSERT INTO polynomials (field_id, degree, coefficients, coeff_hash, source, title)
            VALUES ($f, $d, $c, $h, $s, $t)
            ON CONFLICT (field_id, coeff_hash) DO UPDATE SET title = COALESCE(excluded.title, polynomials.title)
            RETURNING id;
            """;
        cmd.Parameters.AddWithValue("$f", fieldId);
        cmd.Parameters.AddWithValue("$d", p.Degree);
        cmd.Parameters.AddWithValue("$c", coefficients);
        cmd.Parameters.AddWithValue("$h", Hash(coefficients));
        cmd.Parameters.AddWithValue("$s", source);
        cmd.Parameters.AddWithValue("$t", (object?)title ?? DBNull.Value);
        return (long)cmd.ExecuteScalar()!;
    }

    /// <summary>Сохраняет многочлен под заданным названием.</summary>
    public long SavePolynomial(Polynomial p, string source, string? title)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        long fid = SaveField(c, tx, p.Field);
        long id = SavePolynomial(c, tx, fid, p, source, title);
        tx.Commit();
        return id;
    }

    /// <summary>Коэффициенты многочлена и идентификатор его поля.</summary>
    public (long FieldId, string Coefficients) LoadPolynomial(long polynomialId)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT field_id, coefficients FROM polynomials WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", polynomialId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) throw new InvalidOperationException($"Многочлен №{polynomialId} не найден в хранилище.");
        return (r.GetInt64(0), r.GetString(1));
    }

    public IReadOnlyList<StoredPolynomial> GetPolynomials(int limit = 1000)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT p.id, p.field_id, f.characteristic, f.ext_degree, p.degree, p.source, p.title, p.created_at,
                   substr(p.coefficients, 1, 160)
            FROM polynomials p JOIN fields f ON f.id = p.field_id
            ORDER BY p.id DESC LIMIT $lim
            """;
        cmd.Parameters.AddWithValue("$lim", limit);
        var list = new List<StoredPolynomial>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new StoredPolynomial(r.GetInt64(0), r.GetInt64(1), FieldName(r.GetString(2), r.GetInt32(3)),
                r.GetInt32(4), SourceName(r.GetString(5)), r.IsDBNull(6) ? null : r.GetString(6),
                ParseDate(r.GetString(7)), r.GetString(8)));
        return list;
    }

    public void DeletePolynomial(long id)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM polynomials WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // -------------------------------------------------------------- операции

    /// <summary>Записывает операцию в журнал вместе с операндами и результатами (одна транзакция).</summary>
    public long LogOperation(string opCode, Polynomial a, string sourceA, Polynomial? b, string? sourceB,
        string? point, Polynomial? result, Polynomial? remainder, string? value, string algorithm, double elapsedMs)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        long fid = SaveField(c, tx, a.Field);
        long aId = SavePolynomial(c, tx, fid, a, sourceA, null);
        long? bId = b is null ? null : SavePolynomial(c, tx, fid, b, sourceB ?? "keyboard", null);
        long? rId = result is null ? null : SavePolynomial(c, tx, fid, result, "result", null);
        long? remId = remainder is null ? null : SavePolynomial(c, tx, fid, remainder, "result", null);

        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO operations (op_code, operand_a, operand_b, point, result_id, remainder_id, value, algorithm, elapsed_ms)
            VALUES ($op, $a, $b, $pt, $r, $rem, $v, $alg, $ms) RETURNING id;
            """;
        cmd.Parameters.AddWithValue("$op", opCode);
        cmd.Parameters.AddWithValue("$a", aId);
        cmd.Parameters.AddWithValue("$b", (object?)bId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$pt", (object?)point ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$r", (object?)rId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$rem", (object?)remId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$v", (object?)value ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$alg", algorithm);
        cmd.Parameters.AddWithValue("$ms", elapsedMs);
        long id = (long)cmd.ExecuteScalar()!;
        tx.Commit();
        return id;
    }

    public IReadOnlyList<OperationRecord> GetOperations(int limit = 1000)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT o.id, o.performed_at, f.characteristic, f.ext_degree, o.op_code,
                   pa.degree, pb.degree, pr.degree, prem.degree, o.value, o.point, o.algorithm, o.elapsed_ms,
                   pa.field_id, o.operand_a, o.operand_b
            FROM operations o
            JOIN polynomials pa ON pa.id = o.operand_a
            JOIN fields f       ON f.id = pa.field_id
            LEFT JOIN polynomials pb   ON pb.id = o.operand_b
            LEFT JOIN polynomials pr   ON pr.id = o.result_id
            LEFT JOIN polynomials prem ON prem.id = o.remainder_id
            ORDER BY o.id DESC LIMIT $lim
            """;
        cmd.Parameters.AddWithValue("$lim", limit);
        var list = new List<OperationRecord>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            string op = r.GetString(4);
            string? point = r.IsDBNull(10) ? null : r.GetString(10);
            string result = op switch
            {
                "eval" => $"A({point}) = {(r.IsDBNull(9) ? "" : r.GetString(9))}",
                "divmod" => $"deg Q = {(r.IsDBNull(7) ? "—" : r.GetInt32(7))}, deg R = {(r.IsDBNull(8) ? "—" : r.GetInt32(8))}",
                _ => r.IsDBNull(7) ? "—" : $"степень {r.GetInt32(7)}",
            };
            list.Add(new OperationRecord(
                r.GetInt64(0), ParseDate(r.GetString(1)), FieldName(r.GetString(2), r.GetInt32(3)), OperationName(op),
                r.GetInt32(5), r.IsDBNull(6) ? null : r.GetInt32(6), result, r.GetString(11), r.GetDouble(12),
                r.GetInt64(13), r.GetInt64(14), r.IsDBNull(15) ? null : r.GetInt64(15), point));
        }
        return list;
    }

    public void DeleteOperation(long id)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM operations WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Очищает журнал, замеры и безымянные многочлены; сжимает файл базы.</summary>
    public void ClearJournal()
    {
        using var c = Open();
        using (var tx = c.BeginTransaction())
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                DELETE FROM operations;
                DELETE FROM benchmarks;
                DELETE FROM polynomials WHERE title IS NULL;
                DELETE FROM fields WHERE id NOT IN (SELECT field_id FROM polynomials);
                """;
            cmd.ExecuteNonQuery();
            tx.Commit();
        }
        using var vac = c.CreateCommand();
        vac.CommandText = "VACUUM;";
        vac.ExecuteNonQuery();
    }

    // ---------------------------------------------------------------- замеры

    public void SaveBenchmarks(GaloisField field, IEnumerable<BenchmarkRecord> rows)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        long fid = SaveField(c, tx, field);
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO benchmarks (field_id, op_code, algorithm, size, elapsed_ms) VALUES ($f, $op, $alg, $n, $ms)";
        var pf = cmd.Parameters.Add("$f", SqliteType.Integer);
        var pop = cmd.Parameters.Add("$op", SqliteType.Text);
        var palg = cmd.Parameters.Add("$alg", SqliteType.Text);
        var pn = cmd.Parameters.Add("$n", SqliteType.Integer);
        var pms = cmd.Parameters.Add("$ms", SqliteType.Real);
        foreach (var r in rows)
        {
            pf.Value = fid;
            pop.Value = r.Operation;
            palg.Value = r.Algorithm;
            pn.Value = r.Size;
            pms.Value = r.ElapsedMs;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public (long Fields, long Polynomials, long Operations, long Benchmarks, long Bytes) Statistics()
    {
        using var c = Open();
        long Count(string table)
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {table}";
            return (long)cmd.ExecuteScalar()!;
        }
        long bytes = 0;
        foreach (string file in new[] { Path, Path + "-wal" })
            if (File.Exists(file)) bytes += new FileInfo(file).Length;
        return (Count("fields"), Count("polynomials"), Count("operations"), Count("benchmarks"), bytes);
    }

    // ------------------------------------------------------------ утилиты

    public static string FieldName(string p, int k)
    {
        string sp = p.Length <= 24 ? p : $"{p[..8]}…{p[^8..]}";
        return k == 1 ? $"GF({sp})" : $"GF({sp}^{k})";
    }

    public static string OperationName(string code) => code switch
    {
        "add" => "A + B",
        "sub" => "A − B",
        "mul" => "A × B",
        "divmod" => "A ÷ B",
        "eval" => "A(c)",
        _ => code,
    };

    private static string SourceName(string code) => code switch
    {
        "keyboard" => "клавиатура",
        "file" => "файл",
        "random" => "генератор",
        "result" => "результат",
        _ => code,
    };

    private static DateTime ParseDate(string s) =>
        DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateTime.MinValue;
}
