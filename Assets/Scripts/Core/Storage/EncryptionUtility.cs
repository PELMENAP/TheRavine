using System;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace TheRavine.Security
{
    public static class EncryptionUtility
    {
        private const string EncryptionKey =
            "00240ny87c2yn2t42ux7v12390uv5c02mx88924";

        private static readonly byte[] CachedKey;
        private static readonly byte[] CachedHmacKey;

        static EncryptionUtility()
        {
            using var sha = SHA512.Create();
            byte[] hash = sha.ComputeHash(
                Encoding.UTF8.GetBytes(EncryptionKey));

            CachedKey = new byte[32];
            CachedHmacKey = new byte[32];

            Buffer.BlockCopy(hash, 0, CachedKey, 0, 32);
            Buffer.BlockCopy(hash, 32, CachedHmacKey, 0, 32);
        }

        public static byte[] EncryptBytes(byte[] plainBytes)
        {
            using Aes aes = Aes.Create();

            aes.Key = CachedKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();

            using var cipherStream = new MemoryStream();

            cipherStream.Write(aes.IV);

            using (var cryptoStream = new CryptoStream(
                    cipherStream,
                    aes.CreateEncryptor(),
                    CryptoStreamMode.Write))
            {
                cryptoStream.Write(plainBytes);
                cryptoStream.FlushFinalBlock();
            }

            byte[] encryptedData = cipherStream.ToArray();

            using var hmac = new HMACSHA256(CachedHmacKey);
            byte[] hash = hmac.ComputeHash(encryptedData);

            byte[] result = new byte[
                encryptedData.Length + hash.Length];

            Buffer.BlockCopy(
                encryptedData,
                0,
                result,
                0,
                encryptedData.Length);

            Buffer.BlockCopy(
                hash,
                0,
                result,
                encryptedData.Length,
                hash.Length);

            return result;
        }

        public static byte[] DecryptBytes(byte[] cipherBytes)
        {
            const int hmacSize = 32;
            const int ivSize = 16;

            if (cipherBytes.Length < ivSize + hmacSize)
                throw new CryptographicException("Invalid data");

            int encryptedLength = cipherBytes.Length - hmacSize;

            using (var hmac = new HMACSHA256(CachedHmacKey))
            {
                byte[] computedHash = hmac.ComputeHash(
                    cipherBytes,
                    0,
                    encryptedLength);

                for (int i = 0; i < hmacSize; i++)
                {
                    if (computedHash[i] != cipherBytes[encryptedLength + i])
                        throw new CryptographicException("Data corrupted");
                }
            }

            using Aes aes = Aes.Create();

            aes.Key = CachedKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            aes.IV = cipherBytes.AsSpan(0, ivSize).ToArray();

            using var input = new MemoryStream(
                cipherBytes,
                ivSize,
                encryptedLength - ivSize);

            using var cryptoStream = new CryptoStream(
                input,
                aes.CreateDecryptor(),
                CryptoStreamMode.Read);

            using var output = new MemoryStream();

            cryptoStream.CopyTo(output);

            return output.ToArray();
        }
    }
}