using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace tDiscoverer
{
    class IECache
    {

        #region Get Files URL From Cache
        //Declare the WIN32 API calls to get the entries from IE's history cache  
        [DllImport("wininet.dll", SetLastError = true)]
        public static extern IntPtr FindFirstUrlCacheEntry(string lpszUrlSearchPattern, IntPtr lpFirstCacheEntryInfo, out UInt32 lpdwFirstCacheEntryInfoBufferSize);

        [DllImport("wininet.dll", SetLastError = true)]
        public static extern long FindNextUrlCacheEntry(IntPtr hEnumHandle, IntPtr lpNextCacheEntryInfo, out UInt32 lpdwNextCacheEntryInfoBufferSize);

        [DllImport("wininet.dll", SetLastError = true)]
        public static extern long FindCloseUrlCache(IntPtr hEnumHandle);

        [DllImport("Wininet.dll", SetLastError = true, CharSet = CharSet.Auto)]

        public static extern Boolean GetUrlCacheEntryInfo(String lpxaUrlName, IntPtr lpCacheEntryInfo, ref int lpdwCacheEntryInfoBufferSize);
        [StructLayout(LayoutKind.Sequential)]

        public struct INTERNET_CACHE_ENTRY_INFO
        {
            public UInt32 dwStructSize;
            public string lpszSourceUrlName;
            public string lpszLocalFileName;
            public UInt32 CacheEntryType;
            public UInt32 dwUseCount;
            public UInt32 dwHitRate;
            public UInt32 dwSizeLow;
            public UInt32 dwSizeHigh;
            public FILETIME LastModifiedTime;
            public FILETIME ExpireTime;
            public FILETIME LastAccessTime;
            public FILETIME LastSyncTime;
            public IntPtr lpHeaderInfo;
            public UInt32 dwHeaderInfoSize;
            public string lpszFileExtension;
            public UInt32 dwExemptDelta;
        };

        public static class Hresults
        {
            public const int ERROR_SUCCESS = 0;
            public const int ERROR_FILE_NOT_FOUND = 2;
            public const int ERROR_ACCESS_DENIED = 5;
            public const int ERROR_INSUFFICIENT_BUFFER = 122;
            public const int ERROR_NO_MORE_ITEMS = 259;
        };
        //private static void getUrlEntriesInHistory(TextWriter writer)  
        public static List<string> getUrlEntriesInHistory(string sourceUrlFilter, string fileExtensionFilter)
        {
            List<string> filesList = new List<string>();
            IntPtr buffer = IntPtr.Zero;
            UInt32 structSize;
            const string urlPattern = "Visited:";

            //This call will fail but returns the size required in structSize  
            //to allocate necessary buffer  
            IntPtr hEnum = FindFirstUrlCacheEntry(null, buffer, out structSize);
            try
            {
                if (hEnum == IntPtr.Zero)
                {
                    int lastError = Marshal.GetLastWin32Error();
                    if (lastError == Hresults.ERROR_INSUFFICIENT_BUFFER)
                    {
                        //Allocate buffer  
                        buffer = Marshal.AllocHGlobal((int)structSize);
                        //Call again, this time it should succeed  
                        //hEnum = FindFirstUrlCacheEntry(urlPattern, buffer, out structSize);  
                        hEnum = FindFirstUrlCacheEntry(null, buffer, out structSize);
                    }
                    else if (lastError == Hresults.ERROR_NO_MORE_ITEMS)
                    {
                        Console.Error.WriteLine("No entries in IE's history cache");
                        //return;  
                        return filesList;
                    }
                    else if (lastError != Hresults.ERROR_SUCCESS)
                    {
                        Console.Error.WriteLine("Unable to fetch entries from IE's history cache");
                        //return;  
                        return filesList;
                    }
                }


                INTERNET_CACHE_ENTRY_INFO result = (INTERNET_CACHE_ENTRY_INFO)Marshal.PtrToStructure(buffer, typeof(INTERNET_CACHE_ENTRY_INFO));
                //writer.WriteLine(result.lpszSourceUrlName);  
                string fileUrl = result.lpszSourceUrlName.Substring(result.lpszSourceUrlName.LastIndexOf('@') + 1);
                if (fileUrl.Contains(sourceUrlFilter) && fileUrl.EndsWith(fileExtensionFilter))
                {
                    //Console.WriteLine(fileUrl);
                    filesList.Add(fileUrl);
                }


                // Free the buffer  
                if (buffer != IntPtr.Zero)
                {
                    try { Marshal.FreeHGlobal(buffer); }
                    catch { }
                    buffer = IntPtr.Zero;
                    structSize = 0;
                }

                //Loop through all entries, attempt to find matches  
                while (true)
                {
                    long nextResult = FindNextUrlCacheEntry(hEnum, buffer, out structSize);
                    if (nextResult != 1) //TRUE  
                    {
                        int lastError = Marshal.GetLastWin32Error();
                        if (lastError == Hresults.ERROR_INSUFFICIENT_BUFFER)
                        {
                            buffer = Marshal.AllocHGlobal((int)structSize);
                            nextResult = FindNextUrlCacheEntry(hEnum, buffer, out structSize);
                        }
                        else if (lastError == Hresults.ERROR_NO_MORE_ITEMS)
                        {
                            break;
                        }
                    }

                    result = (INTERNET_CACHE_ENTRY_INFO)Marshal.PtrToStructure(buffer, typeof(INTERNET_CACHE_ENTRY_INFO));
                    //writer.WriteLine(result.lpszSourceUrlName);  
                    fileUrl = result.lpszSourceUrlName.Substring(result.lpszSourceUrlName.LastIndexOf('@') + 1);
                    if (fileUrl.Contains(sourceUrlFilter) && fileUrl.EndsWith(fileExtensionFilter))
                    {
                        //Console.WriteLine(fileUrl);
                        filesList.Add(fileUrl);
                    }


                    if (buffer != IntPtr.Zero)
                    {
                        try { Marshal.FreeHGlobal(buffer); }
                        catch { }
                        buffer = IntPtr.Zero;
                        structSize = 0;
                    }
                }
            }
            finally
            {
                if (hEnum != IntPtr.Zero)
                {
                    FindCloseUrlCache(hEnum);
                }
                if (buffer != IntPtr.Zero)
                {
                    try { Marshal.FreeHGlobal(buffer); }
                    catch { }
                }
            }
            return filesList;
        }

        #endregion

        const int ERROR_FILE_NOT_FOUND = 2;
        #region Convert Files URL to Actual Directory URL
        struct LPINTERNET_CACHE_ENTRY_INFO
        {
            public int dwStructSize;
            IntPtr lpszSourceUrlName;
            public IntPtr lpszLocalFileName;
            int CacheEntryType;
            int dwUseCount;
            int dwHitRate;
            int dwSizeLow;
            int dwSizeHigh;
            FILETIME LastModifiedTime;
            FILETIME Expiretime;
            FILETIME LastAccessTime;
            FILETIME LastSyncTime;
            IntPtr lpHeaderInfo;
            int dwheaderInfoSize;
            IntPtr lpszFileExtension;
            int dwEemptDelta;
        }
        public static string GetPathForCachedFile(string fileUrl)
        {
            int cacheEntryInfoBufferSize = 0;
            IntPtr cacheEntryInfoBuffer = IntPtr.Zero;
            int lastError; Boolean result;
            try
            {
                // call to see how big the buffer needs to be
                result = GetUrlCacheEntryInfo(fileUrl, IntPtr.Zero, ref cacheEntryInfoBufferSize);
                lastError = Marshal.GetLastWin32Error();
                if (result == false)
                {
                    if (lastError == ERROR_FILE_NOT_FOUND) return null;
                }
                // allocate the necessary amount of memory
                cacheEntryInfoBuffer = Marshal.AllocHGlobal(cacheEntryInfoBufferSize);

                // make call again with properly sized buffer
                result = GetUrlCacheEntryInfo(fileUrl, cacheEntryInfoBuffer, ref cacheEntryInfoBufferSize);
                lastError = Marshal.GetLastWin32Error();
                if (result == true)
                {
                    Object strObj = Marshal.PtrToStructure(cacheEntryInfoBuffer, typeof(LPINTERNET_CACHE_ENTRY_INFO));
                    LPINTERNET_CACHE_ENTRY_INFO internetCacheEntry = (LPINTERNET_CACHE_ENTRY_INFO)strObj;
                    //INTERNET_CACHE_ENTRY_INFO internetCacheEntry = (INTERNET_CACHE_ENTRY_INFO)strObj;
                    String localFileName = Marshal.PtrToStringAuto(internetCacheEntry.lpszLocalFileName); return localFileName;
                }
                else return null;// file not found
            }
            finally
            {
                if (!cacheEntryInfoBuffer.Equals(IntPtr.Zero)) Marshal.FreeHGlobal(cacheEntryInfoBuffer);
            }
        }
        #endregion

    }
}
