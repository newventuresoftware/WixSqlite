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
            byte[] ba = null;
            Assembly asm = null;
            Assembly curAsm = Assembly.GetExecutingAssembly();

            using (Stream stm = curAsm.GetManifestResourceStream(embeddedResource))
            {
                // Either the file is not existed or it is not mark as embedded resource
                if (stm == null)
                    throw new Exception(embeddedResource + " is not found in Embedded Resources.");

                // Get byte[] from the file from embedded resource
                ba = new byte[(int)stm.Length];
                stm.Read(ba, 0, (int)stm.Length);
                try
                {
                    asm = Assembly.Load(ba);
                    return;
                }
                catch
                {
                    // Purposely do nothing
                    // Unmanaged dll or assembly cannot be loaded directly from byte[]
                    // Let the process fall through for next part
                }
            }

            bool fileOk = false;
            string tempFile = "";

            using (SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider())
            {
                // Get the hash value from embedded DLL/assembly
                string fileHash = BitConverter.ToString(sha1.ComputeHash(ba)).Replace("-", string.Empty);

                // Define the temporary storage location of the DLL/assembly
                tempFile = Path.GetTempPath() + fileName;

                // Determines whether the DLL/assembly is existed or not
                if (File.Exists(tempFile))
                {
                    // Get the hash value of the existed file
                    byte[] bb = File.ReadAllBytes(tempFile);
                    string fileHash2 = BitConverter.ToString(sha1.ComputeHash(bb)).Replace("-", string.Empty);

                    // Compare the existed DLL/assembly with the Embedded DLL/assembly
                    fileOk = fileHash == fileHash2;
                }
                else
                {
                    // The DLL/assembly is not existed yet
                    fileOk = false;
                }
            }

            // Create the file on disk
            if (!fileOk)
            {
                System.IO.File.WriteAllBytes(tempFile, ba);
            }

            // Load it into memory
            IntPtr result = LoadLibrary(tempFile);
            if (result == IntPtr.Zero)
            {
                Exception e = new Win32Exception();
                throw new DllNotFoundException("Unable to load library: " + fileName, e);
            }
        }

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr LoadLibrary(string lpFileName);
    }
}