using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Client.Modules;

namespace Client;

class Program
{
    private static readonly string ServerUrl = Environment.GetEnvironmentVariable("SERVER_URL") ?? "http://localhost:5247";
    private static readonly string LogsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlayfairCipherClient", "logs");
    private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlayfairCipherClient", "settings.json");
    private static readonly string ErrorLogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlayfairCipherClient", "error.log");
    
    private static HttpClientModule? _httpClientModule;
    private static string? _authCookie;

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        
        _httpClientModule = new HttpClientModule(ServerUrl, maxRetries: 3, retryDelayMs: 1000);
        
        ShowWelcome();
        
        bool running = true;
        while (running)
        {
            try
            {
                running = await MainMenu();
            }
            catch (Exception ex)
            {
                LogError($"Критическая ошибка: {ex.Message}", ex);
                Console.WriteLine($"\nОшибка: {ex.Message}");
                Console.WriteLine("Нажмите любую клавишу для продолжения...");
                Console.ReadKey();
            }
        }
        
        Console.WriteLine("\nЗавершение работы.");
    }

    static void ShowWelcome()
    {
        Console.Clear();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("              СИСТЕМА ШИФРОВАНИЯ ПЛЕЙФЕРА");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();
    }

    static async Task<bool> MainMenu()
    {
        Console.WriteLine("\nГЛАВНОЕ МЕНЮ:");
        Console.WriteLine("───────────────────────────────────────────────────────────");
        Console.WriteLine("1. Вход в систему");
        Console.WriteLine("2. Регистрация");
        Console.WriteLine("3. Посмотреть теоретическую справку");
        Console.WriteLine("4. Зашифровать текст");
        Console.WriteLine("5. Расшифровать текст");
        Console.WriteLine("6. Просмотр истории операций");
        Console.WriteLine("0. Выход");
        Console.WriteLine("───────────────────────────────────────────────────────────");
        Console.Write("Выберите действие: ");

        var choice = Console.ReadLine()?.Trim();
        Console.WriteLine();

        switch (choice)
        {
            case "1":
                await Login();
                return true;
            case "2":
                await Signup();
                return true;
            case "3":
                await ShowInfo();
                return true;
            case "4":
                if (await CheckAuth())
                {
                    await PerformEncryption();
                }
                return true;
            case "5":
                if (await CheckAuth())
                {
                    await PerformDecryption();
                }
                return true;
            case "6":
                if (await CheckAuth())
                {
                    await ViewHistory();
                }
                return true;
            case "0":
                return false;
            default:
                Console.WriteLine("Неверный выбор. Попробуйте снова.");
                return true;
        }
    }

    static async Task Login()
    {
        Console.WriteLine("ВХОД В СИСТЕМУ");
        Console.WriteLine("───────────────────────────────────────────────────────────");
        Console.Write("Логин: ");
        var login = Console.ReadLine()?.Trim();
        Console.Write("Пароль: ");
        var password = ReadPassword();

        if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("Логин и пароль не могут быть пустыми.");
            return;
        }

        try
        {
            var payload = new { login, password };
            var response = await _httpClientModule!.ExecuteWithRetryAsync(() =>
                CreateJsonRequest(HttpMethod.Post, "/login", payload));
            
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _authCookie = HttpClientModule.ExtractCookie(response);
                if (!string.IsNullOrEmpty(_authCookie))
                {
                    _httpClientModule.SetAuthCookie(_authCookie);
                }

                string message = "Login successful";
                string username = login;

                if (TryParseJsonElement(responseContent, out var json))
                {
                    if (json.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.String)
                    {
                        message = msgProp.GetString() ?? message;
                    }

                    if (json.TryGetProperty("username", out var userProp) && userProp.ValueKind == JsonValueKind.String)
                    {
                        username = userProp.GetString() ?? username;
                    }
                }

                Console.WriteLine($"{message}");
                Console.WriteLine($"👤 Пользователь: {username}");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Console.WriteLine("Неверный логин или пароль.");
            }
            else
            {
                Console.WriteLine($"Ошибка входа (HTTP {(int)response.StatusCode}): {DescribeResponseText(responseContent)}");
            }
        }
        catch (Exception ex)
        {
            LogError($"Ошибка при входе: {ex.Message}", ex);
            Console.WriteLine($"Ошибка подключения к серверу: {ex.Message}");
            Console.WriteLine("Проверьте, что сервер запущен и доступен.");
        }
    }

    static async Task Signup()
    {
        Console.WriteLine("РЕГИСТРАЦИЯ");
        Console.WriteLine("───────────────────────────────────────────────────────────");
        Console.Write("Логин: ");
        var login = Console.ReadLine()?.Trim();
        Console.Write("Пароль: ");
        var password = ReadPassword();

        if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("Логин и пароль не могут быть пустыми.");
            return;
        }

        try
        {
            var payload = new { login, password };
            var response = await _httpClientModule!.ExecuteWithRetryAsync(() =>
                CreateJsonRequest(HttpMethod.Post, "/signup", payload));
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                string message = "Регистрация успешно";

                if (TryParseJsonElement(responseContent, out var json) &&
                    json.TryGetProperty("message", out var msgProp) &&
                    msgProp.ValueKind == JsonValueKind.String)
                {
                    message = msgProp.GetString() ?? message;
                }

                Console.WriteLine($"{message}");
            }
            else
            {
                if (TryParseJsonElement(responseContent, out var json) &&
                    json.TryGetProperty("error", out var errorProp) &&
                    errorProp.ValueKind == JsonValueKind.String)
                {
                    Console.WriteLine($"{errorProp.GetString()}");
                }
                else
                {
                    Console.WriteLine($"Ошибка регистрации (HTTP {(int)response.StatusCode}): {DescribeResponseText(responseContent)}");
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Ошибка при регистрации: {ex.Message}", ex);
            Console.WriteLine($"Ошибка подключения к серверу: {ex.Message}");
        }
    }

    static async Task ShowInfo()
    {
        try
        {
            var response = await _httpClientModule!.ExecuteWithRetryAsync(() =>
                new HttpRequestMessage(HttpMethod.Get, "/info"));
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                if (TryParseJsonElement(responseContent, out var json))
                {
                    Console.WriteLine("═══════════════════════════════════════════════════════════");
                    Console.WriteLine("           ТЕОРЕТИЧЕСКАЯ СПРАВКА ПО ШИФРУ ПЛЕЙФЕРА");
                    Console.WriteLine("═══════════════════════════════════════════════════════════");
                    Console.WriteLine();

                    if (json.TryGetProperty("description", out var descProp))
                    {
                        Console.WriteLine($"Описание: {descProp.GetString()}");
                        Console.WriteLine();
                    }

                    if (json.TryGetProperty("algorithm", out var algProp))
                    {
                        Console.WriteLine("Алгоритм работы:");
                        Console.WriteLine("───────────────────────────────────────────────────────────");
                        
                        if (algProp.TryGetProperty("step1", out var step1))
                            Console.WriteLine($"1. {step1.GetString()}");
                        if (algProp.TryGetProperty("step2", out var step2))
                            Console.WriteLine($"2. {step2.GetString()}");
                        if (algProp.TryGetProperty("step3", out var step3))
                            Console.WriteLine($"3. {step3.GetString()}");
                        if (algProp.TryGetProperty("step4", out var step4))
                            Console.WriteLine($"4. {step4.GetString()}");
                        Console.WriteLine();
                    }

                    if (json.TryGetProperty("example", out var exProp))
                    {
                        Console.WriteLine("Пример использования:");
                        Console.WriteLine("───────────────────────────────────────────────────────────");
                        if (exProp.TryGetProperty("key", out var keyProp))
                            Console.WriteLine($"Ключ: {keyProp.GetString()}");
                        if (exProp.TryGetProperty("plainText", out var textProp))
                            Console.WriteLine($"Исходный текст: {textProp.GetString()}");
                        if (exProp.TryGetProperty("cipherText", out var cipherProp))
                            Console.WriteLine($"Зашифрованный текст: {cipherProp.GetString()}");
                    }
                    
                    Console.WriteLine("═══════════════════════════════════════════════════════════");
                }
            }
            else
            {
                Console.WriteLine($"Ошибка получения справки (HTTP {(int)response.StatusCode}): {DescribeResponseText(responseContent)}");
            }
        }
        catch (Exception ex)
        {
            LogError($"Ошибка при получении справки: {ex.Message}", ex);
            Console.WriteLine($"Ошибка подключения к серверу: {ex.Message}");
        }
        
        Console.WriteLine("\nНажмите любую клавишу для продолжения...");
        Console.ReadKey();
        ShowWelcome();
    }

    static Task<bool> CheckAuth()
    {
        if (string.IsNullOrEmpty(_authCookie))
        {
            Console.WriteLine("Вы не авторизованы. Пожалуйста, войдите в систему.");
            Console.WriteLine("Нажмите любую клавишу для продолжения...");
            Console.ReadKey();
            return Task.FromResult(false);
        }
        return Task.FromResult(true);
    }

    static async Task PerformEncryption()
    {
        Console.WriteLine("ЗАШИФРОВАНИЕ ТЕКСТА");
        Console.WriteLine("───────────────────────────────────────────────────────────");
        
        Console.Write("Введите текст для шифрования: ");
        var text = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(text))
        {
            Console.WriteLine("Текст не может быть пустым.");
            return;
        }

        var CheckedText = CheckModule.CheckeText(text);
        if (CheckedText == null)
        {
            Console.WriteLine("Текст должен содержать только латинские буквы.");
            return;
        }

        Console.WriteLine("\nВыберите способ ввода ключа:");
        Console.WriteLine("1. Ввести ключ вручную");
        Console.WriteLine("2. Сгенерировать ключ автоматически");
        Console.Write("Выбор: ");
        
        var keyChoice = Console.ReadLine()?.Trim();
        string? key = null;

        if (keyChoice == "1")
        {
            Console.Write("Введите ключ: ");
            key = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(key))
            {
                Console.WriteLine("Ключ не может быть пустым.");
                return;
            }

            key = CheckModule.CheckeKey(key);
            if (key == null)
            {
                Console.WriteLine("Ключ должен содержать только латинские буквы.");
                return;
            }
        }
        else if (keyChoice == "2")
        {
            Console.Write("Введите длину ключа (по умолчанию 10): ");
            var lengthInput = Console.ReadLine()?.Trim();
            int length = 10;
            if (!string.IsNullOrEmpty(lengthInput) && int.TryParse(lengthInput, out var parsedLength) && parsedLength > 0)
            {
                length = parsedLength;
            }

            try
            {
                var payload = new { length };
                var response = await _httpClientModule!.ExecuteWithRetryAsync(() =>
                    CreateJsonRequest(HttpMethod.Post, "/generate-key", payload));
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    if (TryParseJsonElement(responseContent, out var json) &&
                        json.TryGetProperty("key", out var keyProp))
                    {
                        key = keyProp.GetString();
                        Console.WriteLine($"Сгенерированный ключ: {key}");
                    }
                }
                else
                {
                    Console.WriteLine($"Ошибка генерации ключа (HTTP {(int)response.StatusCode})");
                    return;
                }
            }
            catch (Exception ex)
            {
                LogError($"Ошибка при генерации ключа: {ex.Message}", ex);
                Console.WriteLine($"Ошибка подключения к серверу: {ex.Message}");
                return;
            }
        }
        else
        {
            Console.WriteLine("Неверный выбор.");
            return;
        }

        if (string.IsNullOrEmpty(key))
        {
            Console.WriteLine("Ключ не может быть пустым.");
            return;
        }

        try
        {
            var payload = new { text = CheckedText, key };
            var response = await _httpClientModule!.ExecuteWithRetryAsync(() =>
                CreateJsonRequest(HttpMethod.Post, "/encrypt", payload));
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = DeserializeOrDefault<CipherResponse>(responseContent, caseInsensitive: true);

                if (result != null)
                {
                    DisplayCipherResult(result, "ЗАШИФРОВАНИЕ");
                    
                    Console.Write("\nСохранить результат в файл логов? (y/n) [y]: ");
                    var saveChoice = Console.ReadLine()?.Trim().ToLower();
                    if (saveChoice != "n")
                    {
                        await SaveToLogFile(result, "Encryption");
                    }
                }
                else
                {
                    Console.WriteLine("Ошибка шифрования: пустой ответ от сервера.");
                }
            }
            else
            {
                if (TryParseJsonElement(responseContent, out var errorJson) &&
                    errorJson.TryGetProperty("error", out var errorProp) &&
                    errorProp.ValueKind == JsonValueKind.String)
                {
                    Console.WriteLine($"Ошибка шифрования: {errorProp.GetString()}");
                }
                else
                {
                    Console.WriteLine($"Ошибка шифрования (HTTP {(int)response.StatusCode}): {DescribeResponseText(responseContent)}");
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Ошибка при шифровании: {ex.Message}", ex);
            Console.WriteLine($"Ошибка подключения к серверу: {ex.Message}");
        }
    }

    static async Task PerformDecryption()
    {
        Console.WriteLine("РАСШИФРОВАНИЕ ТЕКСТА");
        Console.WriteLine("───────────────────────────────────────────────────────────");
        
        Console.Write("Введите зашифрованный текст: ");
        var cipherText = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(cipherText))
        {
            Console.WriteLine("Текст не может быть пустым.");
            return;
        }

        var CheckedCipherText = CheckModule.CheckeText(cipherText);
        if (CheckedCipherText == null)
        {
            Console.WriteLine("Текст должен содержать только латинские буквы.");
            return;
        }

        Console.Write("Введите ключ: ");
        var key = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(key))
        {
            Console.WriteLine("Ключ не может быть пустым.");
            return;
        }

        key = CheckModule.CheckeKey(key);
        if (key == null)
        {
            Console.WriteLine("Ключ должен содержать только латинские буквы.");
            return;
        }

        try
        {
            var payload = new { cipherText = CheckedCipherText, key };
            var response = await _httpClientModule!.ExecuteWithRetryAsync(() =>
                CreateJsonRequest(HttpMethod.Post, "/decrypt", payload));
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = DeserializeOrDefault<CipherResponse>(responseContent, caseInsensitive: true);

                if (result != null)
                {
                    DisplayCipherResult(result, "РАСШИФРОВАНИЕ");
                    
                    Console.Write("\nСохранить результат в файл логов? (y/n) [y]: ");
                    var saveChoice = Console.ReadLine()?.Trim().ToLower();
                    if (saveChoice != "n")
                    {
                        await SaveToLogFile(result, "Decryption");
                    }
                }
                else
                {
                    Console.WriteLine("Ошибка расшифрования: пустой ответ от сервера.");
                }
            }
            else
            {
                if (TryParseJsonElement(responseContent, out var errorJson) &&
                    errorJson.TryGetProperty("error", out var errorProp) &&
                    errorProp.ValueKind == JsonValueKind.String)
                {
                    Console.WriteLine($"Ошибка расшифрования: {errorProp.GetString()}");
                }
                else
                {
                    Console.WriteLine($"Ошибка расшифрования (HTTP {(int)response.StatusCode}): {DescribeResponseText(responseContent)}");
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Ошибка при расшифровании: {ex.Message}", ex);
            Console.WriteLine($"Ошибка подключения к серверу: {ex.Message}");
        }
    }

    static void DisplayCipherResult(CipherResponse result, string operationType)
    {
        Console.WriteLine("\n═══════════════════════════════════════════════════════════");
        Console.WriteLine($"                    РЕЗУЛЬТАТ {operationType}");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine($"Исходный текст: {result.OriginalText}");
        Console.WriteLine($"Результат: {result.Result}");
        Console.WriteLine($"Ключ: {result.Key}");
        Console.WriteLine($"Время выполнения: {result.ExecutionTimeMs} мс");
        Console.WriteLine($"Дата и время завершения: {result.CompletionTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
    }

    static async Task SaveToLogFile(CipherResponse result, string operationType)
    {
        try
        {
            var logEntry = new LogEntry
            {
                Timestamp = result.CompletionTime,
                OriginalText = result.OriginalText,
                Result = result.Result,
                Key = result.Key,
                ExecutionTimeMs = result.ExecutionTimeMs,
                OperationType = operationType
            };

            var logPath = Path.Combine(LogsDirectory, $"{operationType.ToLower()}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            var json = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = true });
            
            var encryptedJson = EncryptionModule.Encrypt(json);
            await File.WriteAllTextAsync(logPath, encryptedJson);
            
            Console.WriteLine($"Результат сохранен в файл: {logPath}");
        }
        catch (Exception ex)
        {
            LogError($"Ошибка сохранения лога: {ex.Message}", ex);
            Console.WriteLine($"Ошибка сохранения лога: {ex.Message}");
        }
    }

    static async Task ViewHistory()
    {
        Console.WriteLine("ПРОСМОТР ИСТОРИИ ОПЕРАЦИЙ");
        Console.WriteLine("───────────────────────────────────────────────────────────");
        Console.WriteLine("Выберите источник истории:");
        Console.WriteLine("1. Локальные логи (сохраненные в файлы)");
        Console.Write("Выбор: ");
        
        var sourceChoice = Console.ReadLine()?.Trim();
        Console.WriteLine();

        if (sourceChoice == "1")
        {
            await ViewLocalLogs();
        }
            //await ViewServerLogs();
        else
        {
            Console.WriteLine("Неверный выбор.");
        }
    }

    static async Task ViewLocalLogs()
    {
        var logFiles = Directory.GetFiles(LogsDirectory, "*.json").OrderByDescending(f => f).ToList();
        
        if (logFiles.Count == 0)
        {
            Console.WriteLine("📭 Локальные логи не найдены.");
            return;
        }

        Console.WriteLine($"Найдено локальных логов: {logFiles.Count}");
        Console.WriteLine("\nСписок логов:");
        for (int i = 0; i < logFiles.Count; i++)
        {
            var fileName = Path.GetFileName(logFiles[i]);
            Console.WriteLine($"{i + 1}. {fileName}");
        }

        Console.Write("\nВыберите номер лога для просмотра (0 - все, Enter - выход): ");
        var choice = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(choice))
        {
            return;
        }

        if (choice == "0")
        {
            foreach (var logFile in logFiles)
            {
                await DisplayLogFile(logFile);
            }
        }
        else if (int.TryParse(choice, out var index) && index > 0 && index <= logFiles.Count)
        {
            await DisplayLogFile(logFiles[index - 1]);
        }
        else
        {
            Console.WriteLine("Неверный выбор.");
        }
    }

    static async Task DisplayLogFile(string filePath)
    {
        try
        {
            var encryptedContent = await File.ReadAllTextAsync(filePath);
            var decryptedContent = EncryptionModule.Decrypt(encryptedContent);
            var logEntry = JsonSerializer.Deserialize<LogEntry>(decryptedContent);

            if (logEntry != null)
            {
                Console.WriteLine("\n───────────────────────────────────────────────────────────");
                Console.WriteLine($"Файл: {Path.GetFileName(filePath)}");
                Console.WriteLine($"Дата и время: {logEntry.Timestamp:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"Тип операции: {logEntry.OperationType}");
                Console.WriteLine($"Исходный текст: {logEntry.OriginalText}");
                Console.WriteLine($"Результат: {logEntry.Result}");
                Console.WriteLine($"Ключ: {logEntry.Key}");
                Console.WriteLine($"Время выполнения: {logEntry.ExecutionTimeMs} мс");
                Console.WriteLine("───────────────────────────────────────────────────────────");
            }
        }
        catch (Exception ex)
        {
            LogError($"Ошибка чтения лога: {ex.Message}", ex);
            Console.WriteLine($"Ошибка чтения лога: {ex.Message}");
        }
    }

    static string ReadPassword()
    {
        var password = new StringBuilder();
        ConsoleKeyInfo key;
        
        do
        {
            key = Console.ReadKey(true);
            
            if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
            {
                password.Append(key.KeyChar);
                Console.Write("*");
            }
            else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Remove(password.Length - 1, 1);
                Console.Write("\b \b");
            }
        }
        while (key.Key != ConsoleKey.Enter);
        
        Console.WriteLine();
        return password.ToString();
    }

    static HttpRequestMessage CreateJsonRequest(HttpMethod method, string url, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return new HttpRequestMessage(method, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    static T? DeserializeOrDefault<T>(string? content, bool caseInsensitive = false) where T : class
    {
        if (string.IsNullOrWhiteSpace(content))
            return default;

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = caseInsensitive
            };
            return JsonSerializer.Deserialize<T>(content, options);
        }
        catch (Exception ex)
        {
            LogError($"Ошибка парсинга JSON: {ex.Message}. Контент: {DescribeResponseText(content)}");
            return default;
        }
    }

    static bool TryParseJsonElement(string? content, out JsonElement element)
    {
        element = default;

        if (string.IsNullOrWhiteSpace(content))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(content);
            element = doc.RootElement.Clone();
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Ошибка парсинга JSON (JsonElement): {ex.Message}. Контент: {DescribeResponseText(content)}");
            return false;
        }
    }

    static string DescribeResponseText(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "<пустой ответ>";
        var trimmed = content.Trim();
        return trimmed.Length > 500 ? trimmed.Substring(0, 500) + "..." : trimmed;
    }

    static void LogError(string message, Exception? ex = null)
    {
        try
        {
            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            if (ex != null)
            {
                logMessage += $"\n{ex}";
            }
            logMessage += "\n" + new string('-', 80) + "\n";
            
            File.AppendAllText(ErrorLogPath, logMessage);
        }
        catch
        {
        }
    }
}

public class CipherResponse
{
    public string OriginalText { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public long ExecutionTimeMs { get; set; }
    public DateTime CompletionTime { get; set; }
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string OriginalText { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public long ExecutionTimeMs { get; set; }
    public string OperationType { get; set; } = string.Empty;
}
