using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace BuildIndexAction
{
    /// <summary>
    /// A class for loading Embedded Assembly
    /// </summary>
    public class EmbeddedAssembly
    {
/// <summary>
/// Load Assembly, DLL from Embedded Resources into memory.
/// </summary>
/// <param name="embeddedResource">Embedded Resource string. Example: WindowsFormsApplication1.SomeTools.dll</param>
/// <param name="fileName">File Name. Example: SomeTools.dll</param>
public static void Load(string embeddedResource, string fileName)
{
    byte[] embeddedResourceBytes = null;
    Assembly curAsm = Assembly.GetExecutingAssembly();

    using (Stream stm = curAsm.GetManifestResourceStream(embeddedResource))
    {
        // Either the file is not existed or it is not mark as embedded resource
        if (stm == null)
            throw new Exception(embeddedResource + " is not found in Embedded Resources.");
                
        // Get byte[] from the file from embedded resource
        embeddedResourceBytes = new byte[(int)stm.Length];
        stm.Read(embeddedResourceBytes, 0, (int)stm.Length);
    }

    string tempFile = SaveOnDisk(embeddedResourceBytes, fileName);

    // Load it into memory
    IntPtr result = LoadLibrary(tempFile);
    if (result == IntPtr.Zero)
    {
        Exception e = new Win32Exception();
        throw new DllNotFoundException("Unable to load library: " + fileName, e);
    }
}

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        private static string SaveOnDisk(byte[] data, string fileName)
        {
            bool fileOk = false;
            string tempFile = "";

            using (SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider())
            {
                // Define the temporary storage location of the DLL/assembly
                tempFile = Path.Combine(Path.GetTempPath(), fileName);

                // Determines whether the DLL/assembly exists or not
                if (File.Exists(tempFile))
                {
                    // Get the hash value of the existed file
                    byte[] localFileBytes = File.ReadAllBytes(tempFile);
                    string fileHash = ComputeHash(data, sha1);
                    string fileHash2 = ComputeHash(localFileBytes, sha1);

                    // Compare the existed DLL/assembly with the Embedded DLL/assembly
                    fileOk = fileHash == fileHash2;
                }
            }

            // Create the file on disk
            if (!fileOk)
            {
                File.WriteAllBytes(tempFile, data);
            }

            return tempFile;
        }

        private static string ComputeHash(byte[] data, HashAlgorithm algorithm)
        {
            return BitConverter.ToString(algorithm.ComputeHash(data));
        }
    }
}