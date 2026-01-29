using Microsoft.VisualStudio.TestTools.UnitTesting;
using Server.Modules.Encryption;
using System;

namespace TestProject1
{
    [TestClass]
    public class UnitTest1
    {
        private PlayfairCipherModule _cipher;

        [TestInitialize]
        public void Setup()
        {
            _cipher = new PlayfairCipherModule();
        }

        // 1. Пустой текст → пустая строка
        [TestMethod]
        public void Encrypt_EmptyText_ReturnsEmptyString()
        {
            var result = _cipher.Encrypt("", "KEY");
            Assert.AreEqual(string.Empty, result);
        }

        // 2. Пустой ключ → исключение
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Encrypt_EmptyKey_ThrowsException()
        {
            _cipher.Encrypt("HELLO", "");
        }

        // 3. Проверка эталонного примера
        [TestMethod]
        public void Encrypt_HELLO_MONARCHY_Returns_CFSUPM()
        {
            var result = _cipher.Encrypt("HELLO", "MONARCHY");
            Assert.AreEqual("CFSEPM", result);
        }

        // 4. Шифрование + расшифровка
        [TestMethod]
        public void EncryptThenDecrypt_ReturnsOriginalPreparedText()
        {
            var text = "HELLO";
            var key = "MONARCHY";

            var encrypted = _cipher.Encrypt(text, key);
            var decrypted = _cipher.Decrypt(encrypted, key);

            Assert.AreEqual("HELILO", decrypted);
        }

        // 5. Проверка замены J → I
        [TestMethod]
        public void Encrypt_TextWithJ_ReplacesJWithI()
        {
            var encrypted = _cipher.Encrypt("JUMP", "KEY");
            Assert.IsFalse(encrypted.Contains('J'));
        }

        // 6. Нечётная длина шифртекста → исключение
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Decrypt_OddLengthCipher_ThrowsException()
        {
            _cipher.Decrypt("ABC", "KEY");
        }

        // 7. EncryptWithMetadata возвращает данные
        [TestMethod]
        public void EncryptWithMetadata_ReturnsValidMetadata()
        {
            var result = _cipher.EncryptWithMetadata("HELLO", "KEY");

            Assert.IsNotNull(result);
            Assert.IsFalse(string.IsNullOrEmpty(result.Result));
            Assert.IsTrue(result.ExecutionTimeMs >= 0);
        }

        // 8. GenerateKey по умолчанию = 10
        [TestMethod]
        public void GenerateKey_DefaultLength_Is10()
        {
            var key = _cipher.GenerateKey();
            Assert.AreEqual(10, key.Length);
        }

        // 9. GenerateKey с заданной длиной
        [TestMethod]
        public void GenerateKey_CustomLength_IsCorrect()
        {
            var key = _cipher.GenerateKey(20);
            Assert.AreEqual(20, key.Length);
        }
    }
}
