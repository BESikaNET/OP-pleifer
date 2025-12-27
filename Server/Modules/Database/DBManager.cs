using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Server.Modules.Database;

/// <summary>
/// Менеджер базы данных
/// </summary>
public class DBManager
{
    private SqliteConnection? _connection = null;

    private string HashPassword(string password)
    {
        using var algorithm = SHA256.Create();
        var bytesHash = algorithm.ComputeHash(Encoding.Unicode.GetBytes(password));
        return Convert.ToBase64String(bytesHash);
    }

    public bool ConnectToDB(string path)
    {
        Console.WriteLine("Connecting to database...");

        try
        {
            // Создаём директорию для БД, если её нет
            var dbDirectory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
            }

            _connection = new SqliteConnection("Data Source=" + path);
            _connection.Open();

            if (_connection.State != System.Data.ConnectionState.Open)
            {
                Console.WriteLine("Failed to open database!");
                return false;
            }

            // Создаём таблицу пользователей, если её нет
            InitializeDatabase();
        }
        catch (Exception exp)
        {
            Console.WriteLine($"Database connection error: {exp.Message}");
            return false;
        }

        Console.WriteLine("Database connected successfully!");
        return true;
    }

    private void InitializeDatabase()
    {
        if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
            return;

        var createUsersTable = @"
            CREATE TABLE IF NOT EXISTS users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Login TEXT UNIQUE NOT NULL,
                Password TEXT NOT NULL,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            )";

        var createLogsTable = @"
            CREATE TABLE IF NOT EXISTS logs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp DATETIME NOT NULL,
                Level TEXT NOT NULL,
                Message TEXT NOT NULL,
                UserId TEXT,
                InputText TEXT,
                OutputText TEXT,
                Key TEXT,
                OperationType TEXT,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            )";
        
        // Миграция: добавляем новые колонки, если они отсутствуют
        var migrationQueries = new[]
        {
            "ALTER TABLE logs ADD COLUMN InputText TEXT",
            "ALTER TABLE logs ADD COLUMN OutputText TEXT",
            "ALTER TABLE logs ADD COLUMN Key TEXT",
            "ALTER TABLE logs ADD COLUMN OperationType TEXT"
        };

        using var command1 = new SqliteCommand(createUsersTable, _connection);
        command1.ExecuteNonQuery();

        using var command2 = new SqliteCommand(createLogsTable, _connection);
        command2.ExecuteNonQuery();
        
        // Выполняем миграции (игнорируем ошибки, если колонки уже существуют)
        foreach (var migrationQuery in migrationQueries)
        {
            try
            {
                using var migrationCommand = new SqliteCommand(migrationQuery, _connection);
                migrationCommand.ExecuteNonQuery();
            }
            catch
            {
                // Колонка уже существует, игнорируем ошибку
            }
        }
    }

    public SqliteConnection? GetConnection()
    {
        return _connection?.State == System.Data.ConnectionState.Open ? _connection : null;
    }

    public void Disconnect()
    {
        if (_connection == null)
            return;

        if (_connection.State != System.Data.ConnectionState.Open)
            return;

        _connection.Close();
        _connection.Dispose();
        _connection = null;

        Console.WriteLine("Disconnected from database");
    }

    public bool AddUser(string login, string password)
    {
        if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
            return false;

        var query = "INSERT INTO users (Login, Password) VALUES (@login, @password)";
        
        try
        {
            using var command = new SqliteCommand(query, _connection);
            command.Parameters.AddWithValue("@login", login);
            command.Parameters.AddWithValue("@password", HashPassword(password));

            var result = command.ExecuteNonQuery();
            return result == 1;
        }
        catch (Exception exp)
        {
            Console.WriteLine($"Error adding user: {exp.Message}");
            return false;
        }
    }

    public bool CheckUser(string login, string password)
    {
        if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
            return false;

        var query = "SELECT COUNT(*) FROM users WHERE Login = @login AND Password = @password";
        
        try
        {
            using var command = new SqliteCommand(query, _connection);
            command.Parameters.AddWithValue("@login", login);
            command.Parameters.AddWithValue("@password", HashPassword(password));

            var result = command.ExecuteScalar();
            return result != null && Convert.ToInt32(result) > 0;
        }
        catch (Exception exp)
        {
            Console.WriteLine($"Error checking user: {exp.Message}");
            return false;
        }
    }
}

