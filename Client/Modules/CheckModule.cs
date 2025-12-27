namespace Client.Modules;

public static class CheckModule
{
    /// <summary>
    /// проверяет текст для шифрования (должен содержать только латинские буквы)
    /// </summary>
    public static string? CheckeText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        // Удаляем все небуквенные символы и проверяем, что остались только латинские буквы
        var cleaned = new string(input.Where(char.IsLetter).ToArray());
        
        if (string.IsNullOrEmpty(cleaned))
        {
            return null;
        }

        // Проверяем, что все буквы латинские
        if (cleaned.Any(ch => !((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z'))))
        {
            Console.WriteLine("⚠️  Предупреждение: текст должен содержать только латинские буквы.");
            return null;
        }

        return cleaned;
    }

    /// <summary>
    /// Валидирует ключ для шифрования (должен содержать только латинские буквы)
    /// </summary>
    public static string? CheckeKey(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        // Удаляем все небуквенные символы
        var cleaned = new string(input.Where(char.IsLetter).ToArray());
        
        if (string.IsNullOrEmpty(cleaned))
        {
            return null;
        }

        // Проверяем, что все буквы латинские
        if (cleaned.Any(ch => !((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z'))))
        {
            Console.WriteLine("⚠️  Предупреждение: ключ должен содержать только латинские буквы.");
            return null;
        }

        return cleaned;
    }

    public static bool CheckeFilePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        try
        {
            if (!Path.IsPathRooted(filePath))
            {
                return false;
            }

            return File.Exists(filePath);
        }
        catch
        {
            return false;
        }
    }

    public static bool CheckeFileFormat(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var text = CheckeText(content);
            return !string.IsNullOrEmpty(text);
        }
        catch
        {
            return false;
        }
    }
}

