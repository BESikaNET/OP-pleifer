using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Modules.Database;
using ServerLogLevel = Server.Modules.Logging.LogLevel;
using Server.Modules.Logging;
using Server.Modules.Encryption;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:8080",
                "http://127.0.0.1:8080"
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Playfair Cipher Service API",
        Version = "v1",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Playfair Cipher Service",
        }
    });
});

// Настройка модулей
var dbPath = builder.Configuration["Database:Path"] ?? "./data/users.db";

var dbManager = new DBManager();
var logManager = new LogManager(dbManager);
var playfairCipherModule = new PlayfairCipherModule();

builder.Services.AddSingleton(logManager);
builder.Services.AddSingleton(dbManager);
builder.Services.AddSingleton(playfairCipherModule);

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Playfair Cipher Service API");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "Playfair Cipher Service API Documentation";
    c.DefaultModelsExpandDepth(-1);
});

if (!dbManager.ConnectToDB(dbPath))
{
    logManager.Log(ServerLogLevel.ERROR, $"Failed to connect to database at {dbPath}");
    Console.WriteLine($"Failed to connect to database at {dbPath}");
    Console.WriteLine("Shutdown!");
    return;
}

logManager.Log(ServerLogLevel.INFO, "Server started successfully");

app.MapGet("/", () => 
{
    var swaggerUrl = "/swagger";
    return $"Playfair Cipher Service API\n\n" +
           $"API Documentation: {swaggerUrl}\n";
});

app.MapGet("/api/info", () =>
{
    var info = new
    {
        Title = "Шифр Плейфера",
        Description = "Полиграфический шифр подстановки, который работает с парами символов (биграммами) используя матрицу 5x5",
        Algorithm = new
        {
            Step1 = "Подготовка матрицы: создается матрица 5x5 с буквами алфавита (J объединяется с I). Ключевое слово записывается в матрицу, затем добавляются остальные буквы алфавита",
            Step2 = "Подготовка текста: удаляются все небуквенные символы, текст преобразуется в верхний регистр, J заменяются на I, текст разбивается на пары символов. Если пара содержит одинаковые буквы, между ними вставляется X. Если длина текста нечетная, добавляется X в конец",
            Step3 = "Шифрование: для каждой пары символов - если символы в одной строке: заменяются на символы справа (циклически); если символы в одном столбце: заменяются на символы снизу (циклически); иначе: образуют прямоугольник, заменяются на символы в противоположных углах",
            Step4 = "Расшифрование: обратный процесс с использованием тех же правил, но сдвигами в противоположных направлениях"
        },
        Example = new
        {
            Key = "MONARCHY",
            PlainText = "HELLO",
            CipherText = "CFSUPM"
        }
    };
    
    return Results.Ok(info);
});

app.MapPost("/api/encrypt", [Authorize] ([FromBody] EncryptRequest request, [FromServices] PlayfairCipherModule cipherModule, [FromServices] LogManager logger, HttpContext context) =>
{
    var username = context.User.Identity?.Name ?? "unknown";
    
    try
    {
        if (string.IsNullOrEmpty(request.Text))
        {
            logger.Log(ServerLogLevel.WARNING, "Empty text provided for encryption", username);
            return Results.BadRequest(new { error = "Text cannot be empty" });
        }

        if (string.IsNullOrEmpty(request.Key))
        {
            logger.Log(ServerLogLevel.WARNING, "Empty key provided for encryption", username);
            return Results.BadRequest(new { error = "Key cannot be empty" });
        }

        var cipherResult = cipherModule.EncryptWithMetadata(request.Text, request.Key);
        
        // Сохраняем операцию в лог
        logger.LogCipherOperation("Encryption", request.Text, cipherResult.Result, request.Key, username);
        
        return Results.Ok(new CipherResponse
        {
            OriginalText = request.Text,
            Result = cipherResult.Result,
            Key = request.Key,
            ExecutionTimeMs = cipherResult.ExecutionTimeMs,
            CompletionTime = cipherResult.CompletionTime
        });
    }
    catch (Exception ex)
    {
        logger.Log(ServerLogLevel.ERROR, $"Error during encryption: {ex.Message}", username);
        return Results.Problem($"Error during encryption: {ex.Message}");
    }
});

app.MapPost("/api/decrypt", [Authorize] ([FromBody] DecryptRequest request, [FromServices] PlayfairCipherModule cipherModule, [FromServices] LogManager logger, HttpContext context) =>
{
    var username = context.User.Identity?.Name ?? "unknown";
    
    try
    {
        if (string.IsNullOrEmpty(request.CipherText))
        {
            logger.Log(ServerLogLevel.WARNING, "Empty cipher text provided for decryption", username);
            return Results.BadRequest(new { error = "Cipher text cannot be empty" });
        }

        if (string.IsNullOrEmpty(request.Key))
        {
            logger.Log(ServerLogLevel.WARNING, "Empty key provided for decryption", username);
            return Results.BadRequest(new { error = "Key cannot be empty" });
        }

        var cipherResult = cipherModule.DecryptWithMetadata(request.CipherText, request.Key);
        
        // Сохраняем операцию в лог
        logger.LogCipherOperation("Decryption", request.CipherText, cipherResult.Result, request.Key, username);
        
        return Results.Ok(new CipherResponse
        {
            OriginalText = request.CipherText,
            Result = cipherResult.Result,
            Key = request.Key,
            ExecutionTimeMs = cipherResult.ExecutionTimeMs,
            CompletionTime = cipherResult.CompletionTime
        });
    }
    catch (Exception ex)
    {
        logger.Log(ServerLogLevel.ERROR, $"Error during decryption: {ex.Message}", username);
        return Results.Problem($"Error during decryption: {ex.Message}");
    }
});

app.MapPost("/api/generate-key", [Authorize] ([FromBody] GenerateKeyRequest? request, [FromServices] PlayfairCipherModule cipherModule, HttpContext context) =>
{
    var length = request?.Length ?? 10;
    if (length <= 0 || length > 100)
        length = 10;
    
    var key = cipherModule.GenerateKey(length);
    return Results.Ok(new { key, length });
});

app.MapGet("/api/logs", [Authorize] (
    [FromQuery] DateTime? from,
    [FromQuery] DateTime? to,
    [FromQuery] string? level,
    [FromServices] LogManager logger,
    HttpContext context) =>
{
    var username = context.User.Identity?.Name ?? "unknown";
    
    try
    {
        Server.Modules.Logging.LogLevel? logLevel = null;
        if (!string.IsNullOrEmpty(level) && Enum.TryParse<Server.Modules.Logging.LogLevel>(level, true, out var parsedLevel))
        {
            logLevel = parsedLevel;
        }

        var logs = logger.GetLogs(from, to, logLevel, username);
        
        logger.Log(ServerLogLevel.INFO, $"Retrieved {logs.Count} log entries", username);
        
        return Results.Ok(new
        {
            Count = logs.Count,
            Logs = logs.Select(l => new
            {
                l.Timestamp,
                Level = l.Level.ToString(),
                l.Message,
                l.UserId,
                InputText = l.InputText,
                OutputText = l.OutputText,
                Key = l.Key,
                OperationType = l.OperationType
            })
        });
    }
    catch (Exception ex)
    {
        logger.Log(ServerLogLevel.ERROR, $"Error retrieving logs: {ex.Message}", username);
        return Results.Problem($"Error retrieving logs: {ex.Message}");
    }
});

app.MapPost("/api/login", async ([FromBody] LoginRequest request, [FromServices] DBManager db, [FromServices] LogManager logger, HttpContext context) =>
{
    if (string.IsNullOrEmpty(request.Login) || string.IsNullOrEmpty(request.Password))
    {
        return Results.BadRequest(new { error = "Login and password are required" });
    }

    if (!db.CheckUser(request.Login, request.Password))
    {
        logger.Log(ServerLogLevel.WARNING, $"Failed login attempt for user: {request.Login}");
        return Results.Unauthorized();
    }

    var claims = new List<Claim> { new Claim(ClaimTypes.Name, request.Login) };
    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

    logger.Log(ServerLogLevel.INFO, $"User logged in: {request.Login}", request.Login);
    return Results.Ok(new { message = "Login successful", username = request.Login });
});

app.MapPost("/api/signup", ([FromBody] SignupRequest request, [FromServices] DBManager db, [FromServices] LogManager logger) =>
{
    if (string.IsNullOrEmpty(request.Login) || string.IsNullOrEmpty(request.Password))
    {
        return Results.BadRequest(new { error = "Login and password are required" });
    }

    // Проверяем, существует ли пользователь
    if (db.CheckUser(request.Login, request.Password))
    {
        logger.Log(ServerLogLevel.WARNING, $"Attempt to register already existing user: {request.Login}", request.Login);
        return Results.BadRequest(new { error = $"User {request.Login} already exists" });
    }

    if (db.AddUser(request.Login, request.Password))
    {
        logger.Log(ServerLogLevel.INFO, $"User registered: {request.Login}", request.Login);
        return Results.Ok(new { message = $"User {request.Login} registered successfully!" });
    }

    logger.Log(ServerLogLevel.ERROR, $"Failed to register user due to internal error: {request.Login}", request.Login);
    return Results.Problem("Internal error while registering user");
});

app.MapGet("/api/check_user", [Authorize] (HttpContext context, [FromServices] LogManager logger) =>
{
    if (context.User.Identity == null)
        return Results.BadRequest(new { error = "User is unknown" });

    var username = context.User.Identity.Name ?? "unknown";
    logger.Log(ServerLogLevel.INFO, "User check performed", username);
    return Results.Ok(new { username });
});

app.Lifetime.ApplicationStopping.Register(() =>
{
    logManager.Log(ServerLogLevel.INFO, "Server is shutting down");
    dbManager.Disconnect();
});

var port = Environment.GetEnvironmentVariable("PORT") ?? builder.Configuration["Server:Port"] ?? "5247";
app.Run($"http://0.0.0.0:{port}");

/// <summary>
/// Запрос на шифрование текста
/// </summary>
public class EncryptRequest
{
    /// <summary>
    /// Текст для шифрования
    /// </summary>
    /// <example>HELLO</example>
    public string Text { get; set; } = string.Empty;
    
    /// <summary>
    /// Ключевое слово для шифрования
    /// </summary>
    /// <example>MONARCHY</example>
    public string Key { get; set; } = string.Empty;
}

/// <summary>
/// Запрос на расшифрование текста
/// </summary>
public class DecryptRequest
{
    /// <summary>
    /// Зашифрованный текст
    /// </summary>
    /// <example>CFSUPM</example>
    public string CipherText { get; set; } = string.Empty;
    
    /// <summary>
    /// Ключевое слово для расшифрования
    /// </summary>
    /// <example>MONARCHY</example>
    public string Key { get; set; } = string.Empty;
}

/// <summary>
/// Запрос на генерацию ключа
/// </summary>
public class GenerateKeyRequest
{
    /// <summary>
    /// Длина ключа (по умолчанию 10)
    /// </summary>
    /// <example>10</example>
    public int Length { get; set; } = 10;
}

/// <summary>
/// Результат шифрования/расшифрования
/// </summary>
public class CipherResponse
{
    /// <summary>
    /// Исходный текст
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;
    
    /// <summary>
    /// Результат (зашифрованный или расшифрованный текст)
    /// </summary>
    public string Result { get; set; } = string.Empty;
    
    /// <summary>
    /// Использованный ключ
    /// </summary>
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// Время выполнения операции в миллисекундах
    /// </summary>
    public long ExecutionTimeMs { get; set; }
    
    /// <summary>
    /// Дата и время завершения операции
    /// </summary>
    public DateTime CompletionTime { get; set; }
}

/// <summary>
/// Запрос на вход в систему
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// Имя пользователя
    /// </summary>
    /// <example>user123</example>
    public string Login { get; set; } = string.Empty;
    
    /// <summary>
    /// Пароль пользователя
    /// </summary>
    /// <example>password123</example>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Запрос на регистрацию нового пользователя
/// </summary>
public class SignupRequest
{
    /// <summary>
    /// Имя пользователя
    /// </summary>
    /// <example>newuser</example>
    public string Login { get; set; } = string.Empty;
    
    /// <summary>
    /// Пароль пользователя
    /// </summary>
    /// <example>securepassword</example>
    public string Password { get; set; } = string.Empty;
}

