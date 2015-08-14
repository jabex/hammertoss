using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Security.Cryptography;

namespace Uploader
{
    class Program
    {
        #region Constant
        private const string DEFAULT_HASH_ALGORITHM = "SHA1";
        private const int DEFAULT_MIN_SALT_LEN = 4;
        private const int DEFAULT_MAX_SALT_LEN = 8;
        private const int PASSWORD_ITERATIONS = 1000;
        private const int DEFAULT_KEY_SIZE = 256;
        #endregion

        #region Variables
        private static ArrayList MARKER = new ArrayList()
        {
            "FFD8", // SOI
            "FFD9", // EOI
        };
        #endregion

        #region Methods
        /**
          * Convert image bytes to 16-bit integer in big endian 
         **/
        private static int i16(byte[] c)
        {
            return (c[0] << 8) | c[1];
        }

        /**
          * Create a salt with variable size (4 to 8) by a default charset
         **/
        private static string getSaltStr()
        {
            string chars = "abcdefghijklmnopqrstuvwxyz";
            Random random = new Random();
            int saltSize = random.Next(DEFAULT_MIN_SALT_LEN, DEFAULT_MAX_SALT_LEN);
            string saltStr = new string(Enumerable.Repeat(chars, saltSize)
                          .Select(s => s[random.Next(s.Length)])
                          .ToArray());
            return saltStr;
        }

        /**
         * AES 256 bits encrypt with salt
        **/
        private static byte[] Encrypt(byte[] plainTextBytes, byte[] passwordBytes, byte[] saltBytes)
        {
            byte[] cipherTextBytes = null;
            ICryptoTransform encryptor = null;

            PasswordDeriveBytes key = new PasswordDeriveBytes(passwordBytes, saltBytes, DEFAULT_HASH_ALGORITHM, PASSWORD_ITERATIONS);

            RijndaelManaged AES = new RijndaelManaged();
            AES.KeySize = DEFAULT_KEY_SIZE;
            AES.Mode = CipherMode.CBC;

            // Get Key And IV From Password And Salt
            AES.Key = key.GetBytes(AES.KeySize / 8);
            AES.IV = key.GetBytes(AES.BlockSize / 8);
            encryptor = AES.CreateEncryptor();

            MemoryStream memoryStream = new MemoryStream();
            CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);

            cryptoStream.FlushFinalBlock();
            cipherTextBytes = memoryStream.ToArray();

            memoryStream.Close();
            cryptoStream.Close();

            return cipherTextBytes;
        }

        #endregion

        static void Main(string[] args)
        {
            //Change value with path of your test jpeg image
            string file_in = @"";

            #region Validate JPEG file markers
            /**
              * Open JPEG file and read for SOI and EOI Marker
            **/
            Console.WriteLine("CHECK JPEG FORMAT: START");
            int chunkSize = 2;
            byte[] chunk = new byte[chunkSize];
            int i16value = 0;
            string hex = "";
            Console.WriteLine("\nFile JPEG:\n{0}", file_in);
            try
            {
                using (FileStream fileReader = new FileStream(file_in, FileMode.Open, FileAccess.Read))
                {
                    BinaryReader binaryReader = new BinaryReader(fileReader);
                    int length = (int)fileReader.Length;

                    // Check SOI marker
                    binaryReader.Read(chunk, 0, chunkSize);
                    i16value = i16(chunk);
                    hex = i16value.ToString("X4");
                    if (!hex.Equals(MARKER[0])) { throw new Exception("(SOI) JFIF format not valid!"); }
                    Console.WriteLine("SOI MARKER HIT: {0}", hex);

                    // Check EOI marker
                    int offsetcheck = length - 2;
                    binaryReader.BaseStream.Seek(offsetcheck, SeekOrigin.Begin);

                    binaryReader.Read(chunk, 0, chunkSize);
                    i16value = i16(chunk);
                    hex = i16value.ToString("X4");
                    if (!hex.Equals(MARKER[1])) { throw new Exception("(EOI) JFIF format not valid!"); }
                    Console.WriteLine("EOI MARKER HIT: {0}\n", hex);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
                Environment.Exit(0);
            }
            Console.WriteLine("CHECK JPEG FORMAT: STOP");
            Console.ReadLine();
            #endregion

            #region Steganography
            Console.WriteLine("CREATE CRYPTOTEXT AND APPEND TO FILE: START\n");
            string plaintext = "tasklist"; //windows Commands
            string password = "Pas5pr@se";
            string salt = getSaltStr();

            // Get the bytes of the string
            byte[] bytesToBeEncrypted = Encoding.ASCII.GetBytes(plaintext);
            byte[] passwordBytes = Encoding.ASCII.GetBytes(password);
            byte[] saltBytes = Encoding.ASCII.GetBytes(salt);

            // Hash the password with SHA256
            passwordBytes = SHA256.Create().ComputeHash(passwordBytes);
            byte[] chipertext = Encrypt(bytesToBeEncrypted, passwordBytes, saltBytes);

            Console.WriteLine("Plaintext : {0}", plaintext);
            Console.WriteLine("Chipertext : {0}", Convert.ToBase64String(chipertext));

            int offset = 0;
            try
            {
                using (FileStream fileWriter = new FileStream(file_in, FileMode.Append, FileAccess.Write))
                {
                    offset = (int)fileWriter.Length;
                    fileWriter.Write(chipertext, 0, chipertext.Length);
                }
                Console.WriteLine("Hash Tag : #{0}{1}", offset, salt);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Environment.Exit(0);
            }
            Console.WriteLine("\nCREATE CHiIPERTEXT AND APPEND TO FILE: STOP");
            Console.ReadLine();
            #endregion

        }
    }
}