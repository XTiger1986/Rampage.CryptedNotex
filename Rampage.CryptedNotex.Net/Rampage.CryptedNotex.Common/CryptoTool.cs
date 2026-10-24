using System;
using System.Security.Cryptography;
using System.Text;

namespace Rampage.CryptedNotex.Common
{
    /// <summary>
    /// 加密原语工具：内容密钥派生(PBKDF2-SHA256)与正文/附件加密(AES-256-GCM)
    /// </summary>
    /// <remarks>
    /// 密文 blob 格式：版本1字节(0x01) + nonce12字节 + 密文N字节 + tag16字节。
    /// 登录验证哈希请用框架 SecurityTool.PBKDF2Hash/PBKDF2Verify，与这里的密钥派生使用互相独立的盐，不能互推。
    /// </remarks>
    public static class CryptoTool
    {
        /// <summary>
        /// 内容密钥派生默认迭代次数；提高默认值后老用户在下一次改密时自动升级
        /// </summary>
        public const int KDF_ITERATIONS_DEFAULT = 600000;

        /// <summary>
        /// 派生盐字节数
        /// </summary>
        public const int SALT_SIZE = 16;

        /// <summary>
        /// AES-GCM nonce 字节数
        /// </summary>
        public const int NONCE_SIZE = 12;

        /// <summary>
        /// AES-GCM 认证标签字节数
        /// </summary>
        public const int TAG_SIZE = 16;

        /// <summary>
        /// 密文 blob 格式版本1(当前唯一版本；更换算法时递增并由解密方按版本分派)
        /// </summary>
        public const byte BLOB_VERSION_1 = 0x01;

        /// <summary>
        /// 笔记正文的 AAD 域分隔常量(防笔记密文与附件密文互换粘贴)
        /// </summary>
        public const string AAD_NOTE = "Rampage.CryptedNotex/Note/V1";

        /// <summary>
        /// 附件的 AAD 域分隔常量
        /// </summary>
        public const string AAD_ATTACHMENT = "Rampage.CryptedNotex/Attachment/V1";

        /// <summary>
        /// 内容密钥字节长度(AES-256)
        /// </summary>
        private const int KEY_SIZE = 32;

        /// <summary>
        /// 生成新的内容密钥派生盐(base64,16字节,随机)
        /// </summary>
        /// <returns>base64 编码的盐</returns>
        public static string GenerateSaltBase64()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(SALT_SIZE));
        }

        /// <summary>
        /// 由密码与派生盐派生 256 位内容密钥
        /// </summary>
        /// <param name="password">明文密码</param>
        /// <param name="kdfSaltBase64">base64 派生盐</param>
        /// <param name="iterations">迭代次数</param>
        /// <returns>32 字节内容密钥；调用方负责用后 ClearKey 清零</returns>
        public static byte[] DeriveContentKey(string password, string kdfSaltBase64, int iterations)
        {
            return Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), Convert.FromBase64String(kdfSaltBase64), iterations, HashAlgorithmName.SHA256, KEY_SIZE);
        }

        /// <summary>
        /// 加密明文为密文 blob(版本+nonce+密文+tag)；每次加密生成全新随机 nonce
        /// </summary>
        /// <param name="plain">明文字节</param>
        /// <param name="key">32 字节内容密钥</param>
        /// <param name="aadDomain">AAD 域分隔常量(AAD_NOTE 或 AAD_ATTACHMENT)</param>
        /// <returns>密文 blob</returns>
        public static byte[] Encrypt(byte[] plain, byte[] key, string aadDomain)
        {
            byte[] nonce = RandomNumberGenerator.GetBytes(NONCE_SIZE);
            byte[] cipher = new byte[plain.Length];
            byte[] tag = new byte[TAG_SIZE];
            using (AesGcm aes = new AesGcm(key, TAG_SIZE))
            {
                aes.Encrypt(nonce, plain, cipher, tag, Encoding.ASCII.GetBytes(aadDomain));
            }

            byte[] blob = new byte[1 + NONCE_SIZE + cipher.Length + TAG_SIZE];
            blob[0] = BLOB_VERSION_1;
            Buffer.BlockCopy(nonce, 0, blob, 1, NONCE_SIZE);
            Buffer.BlockCopy(cipher, 0, blob, 1 + NONCE_SIZE, cipher.Length);
            Buffer.BlockCopy(tag, 0, blob, 1 + NONCE_SIZE + cipher.Length, TAG_SIZE);
            return blob;
        }

        /// <summary>
        /// 解密密文 blob 为明文；版本不符或 tag 校验失败(密钥错误/数据被篡改)抛 CryptographicException，由调用方兜底
        /// </summary>
        /// <param name="blob">密文 blob</param>
        /// <param name="key">32 字节内容密钥</param>
        /// <param name="aadDomain">AAD 域分隔常量(须与加密时一致)</param>
        /// <returns>明文字节</returns>
        public static byte[] Decrypt(byte[] blob, byte[] key, string aadDomain)
        {
            if (blob == null || blob.Length < 1 + NONCE_SIZE + TAG_SIZE)
            {
                throw new CryptographicException("密文 blob 长度不合法");
            }
            if (blob[0] != BLOB_VERSION_1)
            {
                throw new CryptographicException("不支持的密文 blob 版本: " + blob[0]);
            }

            int cipherLength = blob.Length - 1 - NONCE_SIZE - TAG_SIZE;
            byte[] nonce = new byte[NONCE_SIZE];
            byte[] cipher = new byte[cipherLength];
            byte[] tag = new byte[TAG_SIZE];
            Buffer.BlockCopy(blob, 1, nonce, 0, NONCE_SIZE);
            Buffer.BlockCopy(blob, 1 + NONCE_SIZE, cipher, 0, cipherLength);
            Buffer.BlockCopy(blob, 1 + NONCE_SIZE + cipherLength, tag, 0, TAG_SIZE);

            byte[] plain = new byte[cipherLength];
            using (AesGcm aes = new AesGcm(key, TAG_SIZE))
            {
                aes.Decrypt(nonce, cipher, tag, plain, Encoding.ASCII.GetBytes(aadDomain));
            }
            return plain;
        }

        /// <summary>
        /// 尽最大努力清零内存中的密钥材料(string 密码无法清零,属已声明的威胁模型边界)
        /// </summary>
        /// <param name="key">待清零的密钥字节数组</param>
        public static void ClearKey(byte[]? key)
        {
            if (key != null)
            {
                Array.Clear(key, 0, key.Length);
            }
        }
    }
}
