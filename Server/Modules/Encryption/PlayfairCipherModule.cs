using System.Text;

namespace Server.Modules.Encryption;

/// <summary>
/// Результат шифрования/расшифрования
/// </summary>
public class CipherResult
{
    public string Result { get; set; } = string.Empty;
    public long ExecutionTimeMs { get; set; }
    public DateTime CompletionTime { get; set; }
}

/// <summary>
/// Модуль шифрования Плейфера
/// Шифр Плейфейра — это полиграфический шифр подстановки, который работает с парами символов (биграммами)
/// используя матрицу 5x5, содержащую ключевое слово.
/// </summary>
public class PlayfairCipherModule
{
    private const int MatrixSize = 5;
    private const char ReplacementChar = 'I'; // J заменяется на I

    /// <summary>
    /// Создает матрицу 5x5 из ключевого слова
    /// </summary>
    private char[,] CreateMatrix(string key)
    {
        var matrix = new char[MatrixSize, MatrixSize];
        var usedChars = new HashSet<char>();
        var keyUpper = key.ToUpper().Replace('J', ReplacementChar);
        var alphabet = "ABCDEFGHIKLMNOPQRSTUVWXYZ"; // J исключен, так как заменяется на I

        int row = 0, col = 0;

        // Сначала добавляем буквы ключа
        foreach (var ch in keyUpper)
        {
            if (char.IsLetter(ch) && !usedChars.Contains(ch))
            {
                matrix[row, col] = ch;
                usedChars.Add(ch);
                col++;
                if (col == MatrixSize)
                {
                    col = 0;
                    row++;
                }
            }
        }

        // Затем добавляем остальные буквы алфавита
        foreach (var ch in alphabet)
        {
            if (!usedChars.Contains(ch))
            {
                matrix[row, col] = ch;
                usedChars.Add(ch);
                col++;
                if (col == MatrixSize)
                {
                    col = 0;
                    row++;
                }
            }
        }

        return matrix;
    }

    /// <summary>
    /// Находит позицию символа в матрице
    /// </summary>
    private (int row, int col) FindPosition(char[,] matrix, char ch)
    {
        for (int i = 0; i < MatrixSize; i++)
        {
            for (int j = 0; j < MatrixSize; j++)
            {
                if (matrix[i, j] == ch)
                {
                    return (i, j);
                }
            }
        }
        throw new ArgumentException($"Символ {ch} не найден в матрице");
    }

    /// <summary>
    /// Подготавливает текст для шифрования
    /// </summary>
    private string PrepareText(string text)
    {
        // Удаляем все небуквенные символы
        var cleaned = new string(text.Where(char.IsLetter).ToArray());
        
        // Преобразуем в верхний регистр
        cleaned = cleaned.ToUpper();
        
        // Заменяем J на I
        cleaned = cleaned.Replace('J', ReplacementChar);
        
        // Разбиваем на пары и обрабатываем одинаковые буквы
        var pairs = new List<string>();
        for (int i = 0; i < cleaned.Length; i += 2)
        {
            if (i + 1 < cleaned.Length)
            {
                var ch1 = cleaned[i];
                var ch2 = cleaned[i + 1];
                
                // Если буквы одинаковые, вставляем X между ними
                if (ch1 == ch2)
                {
                    pairs.Add($"{ch1}{ReplacementChar}");
                    i--; // Откатываемся на один символ назад
                }
                else
                {
                    pairs.Add($"{ch1}{ch2}");
                }
            }
            else
            {
                // Если длина нечетная, добавляем X в конец
                pairs.Add($"{cleaned[i]}{ReplacementChar}");
            }
        }
        
        return string.Join("", pairs);
    }

    /// <summary>
    /// Шифрует пару символов
    /// </summary>
    private string EncryptPair(char[,] matrix, char ch1, char ch2)
    {
        var pos1 = FindPosition(matrix, ch1);
        var pos2 = FindPosition(matrix, ch2);

        // Если символы в одной строке: заменяются на символы справа (циклически)
        if (pos1.row == pos2.row)
        {
            return $"{matrix[pos1.row, (pos1.col + 1) % MatrixSize]}" +
                   $"{matrix[pos2.row, (pos2.col + 1) % MatrixSize]}";
        }

        // Если символы в одном столбце: заменяются на символы снизу (циклически)
        if (pos1.col == pos2.col)
        {
            return $"{matrix[(pos1.row + 1) % MatrixSize, pos1.col]}" +
                   $"{matrix[(pos2.row + 1) % MatrixSize, pos2.col]}";
        }

        // Иначе: образуют прямоугольник, заменяются на символы в противоположных углах
        return $"{matrix[pos1.row, pos2.col]}" +
               $"{matrix[pos2.row, pos1.col]}";
    }

    /// <summary>
    /// Расшифровывает пару символов
    /// </summary>
    private string DecryptPair(char[,] matrix, char ch1, char ch2)
    {
        var pos1 = FindPosition(matrix, ch1);
        var pos2 = FindPosition(matrix, ch2);

        // Если символы в одной строке: заменяются на символы слева (циклически)
        if (pos1.row == pos2.row)
        {
            return $"{matrix[pos1.row, (pos1.col - 1 + MatrixSize) % MatrixSize]}" +
                   $"{matrix[pos2.row, (pos2.col - 1 + MatrixSize) % MatrixSize]}";
        }

        // Если символы в одном столбце: заменяются на символы сверху (циклически)
        if (pos1.col == pos2.col)
        {
            return $"{matrix[(pos1.row - 1 + MatrixSize) % MatrixSize, pos1.col]}" +
                   $"{matrix[(pos2.row - 1 + MatrixSize) % MatrixSize, pos2.col]}";
        }

        // Иначе: образуют прямоугольник, заменяются на символы в противоположных углах
        return $"{matrix[pos1.row, pos2.col]}" +
               $"{matrix[pos2.row, pos1.col]}";
    }

    /// <summary>
    /// Шифрует текст с использованием ключа
    /// </summary>
    public string Encrypt(string text, string key)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Ключ не может быть пустым");

        var matrix = CreateMatrix(key);
        var preparedText = PrepareText(text);
        var result = new StringBuilder();

        for (int i = 0; i < preparedText.Length; i += 2)
        {
            var ch1 = preparedText[i];
            var ch2 = preparedText[i + 1];
            result.Append(EncryptPair(matrix, ch1, ch2));
        }

        return result.ToString();
    }

    /// <summary>
    /// Расшифровывает текст с использованием ключа
    /// </summary>
    public string Decrypt(string cipherText, string key)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Ключ не может быть пустым");

        var matrix = CreateMatrix(key);
        var preparedText = cipherText.ToUpper().Replace('J', ReplacementChar);
        
        // Удаляем все небуквенные символы
        preparedText = new string(preparedText.Where(char.IsLetter).ToArray());
        
        if (preparedText.Length % 2 != 0)
            throw new ArgumentException("Зашифрованный текст должен содержать четное количество букв");

        var result = new StringBuilder();

        for (int i = 0; i < preparedText.Length; i += 2)
        {
            var ch1 = preparedText[i];
            var ch2 = preparedText[i + 1];
            result.Append(DecryptPair(matrix, ch1, ch2));
        }

        return result.ToString();
    }

    /// <summary>
    /// Шифрует текст с возвратом метаданных
    /// </summary>
    public CipherResult EncryptWithMetadata(string text, string key)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var startTime = DateTime.UtcNow;

        var result = Encrypt(text, key);
        
        stopwatch.Stop();
        var completionTime = DateTime.UtcNow;

        return new CipherResult
        {
            Result = result,
            ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
            CompletionTime = completionTime
        };
    }

    /// <summary>
    /// Расшифровывает текст с возвратом метаданных
    /// </summary>
    public CipherResult DecryptWithMetadata(string cipherText, string key)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var startTime = DateTime.UtcNow;

        var result = Decrypt(cipherText, key);
        
        stopwatch.Stop();
        var completionTime = DateTime.UtcNow;

        return new CipherResult
        {
            Result = result,
            ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
            CompletionTime = completionTime
        };
    }

    /// <summary>
    /// Генерирует случайный ключ заданной длины
    /// </summary>
    public string GenerateKey(int length = 10)
    {
        if (length <= 0)
            length = 10;

        var random = new Random();
        var alphabet = "ABCDEFGHIKLMNOPQRSTUVWXYZ";
        var key = new StringBuilder();

        for (int i = 0; i < length; i++)
        {
            key.Append(alphabet[random.Next(alphabet.Length)]);
        }

        return key.ToString();
    }
}

