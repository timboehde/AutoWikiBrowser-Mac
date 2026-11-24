// Saved passwords in the macOS login keychain (Windows AWB keeps them encrypted in the Registry).
// Items are "generic passwords" with service "AutoWikiBrowser" and the wiki username as account,
// so they show up in Keychain Access under that name.
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AutoWikiBrowser.Mac
{
    internal static class Keychain
    {
        private const string Service = "AutoWikiBrowser";
        private const string Security = "/System/Library/Frameworks/Security.framework/Security";
        private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private const int ErrSecSuccess = 0, ErrSecItemNotFound = -25300;

        [DllImport(Security)]
        private static extern int SecKeychainAddGenericPassword(IntPtr keychain, uint serviceNameLength, byte[] serviceName,
            uint accountNameLength, byte[] accountName, uint passwordLength, byte[] passwordData, out IntPtr itemRef);

        [DllImport(Security)]
        private static extern int SecKeychainFindGenericPassword(IntPtr keychainOrArray, uint serviceNameLength, byte[] serviceName,
            uint accountNameLength, byte[] accountName, out uint passwordLength, out IntPtr passwordData, out IntPtr itemRef);

        [DllImport(Security)]
        private static extern int SecKeychainItemModifyAttributesAndData(IntPtr itemRef, IntPtr attrList, uint length, byte[] data);

        [DllImport(Security)]
        private static extern int SecKeychainItemDelete(IntPtr itemRef);

        [DllImport(Security)]
        private static extern int SecKeychainItemFreeContent(IntPtr attrList, IntPtr data);

        [DllImport(CoreFoundation)]
        private static extern void CFRelease(IntPtr cf);

        private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s ?? "");

        /// <summary>The saved password for this username, or null</summary>
        public static string GetPassword(string user)
        {
            if (string.IsNullOrEmpty(user)) return null;
            try
            {
                byte[] service = Utf8(Service), account = Utf8(user);
                int status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)service.Length, service,
                    (uint)account.Length, account, out uint length, out IntPtr data, out IntPtr item);
                if (status != ErrSecSuccess) return null;
                try
                {
                    var bytes = new byte[length];
                    Marshal.Copy(data, bytes, 0, (int)length);
                    return Encoding.UTF8.GetString(bytes);
                }
                finally
                {
                    SecKeychainItemFreeContent(IntPtr.Zero, data);
                    if (item != IntPtr.Zero) CFRelease(item);
                }
            }
            catch { return null; }
        }

        /// <summary>Adds or updates the saved password. False if the keychain refused.</summary>
        public static bool SavePassword(string user, string password)
        {
            if (string.IsNullOrEmpty(user)) return false;
            try
            {
                byte[] service = Utf8(Service), account = Utf8(user), secret = Utf8(password);
                int status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)service.Length, service,
                    (uint)account.Length, account, out uint length, out IntPtr data, out IntPtr item);
                if (status == ErrSecSuccess)
                {
                    SecKeychainItemFreeContent(IntPtr.Zero, data);
                    try { return SecKeychainItemModifyAttributesAndData(item, IntPtr.Zero, (uint)secret.Length, secret) == ErrSecSuccess; }
                    finally { CFRelease(item); }
                }
                if (status != ErrSecItemNotFound) return false;

                status = SecKeychainAddGenericPassword(IntPtr.Zero, (uint)service.Length, service,
                    (uint)account.Length, account, (uint)secret.Length, secret, out IntPtr newItem);
                if (newItem != IntPtr.Zero) CFRelease(newItem);
                return status == ErrSecSuccess;
            }
            catch { return false; }
        }

        public static void DeletePassword(string user)
        {
            if (string.IsNullOrEmpty(user)) return;
            try
            {
                byte[] service = Utf8(Service), account = Utf8(user);
                if (SecKeychainFindGenericPassword(IntPtr.Zero, (uint)service.Length, service,
                        (uint)account.Length, account, out uint length, out IntPtr data, out IntPtr item) != ErrSecSuccess)
                    return;
                SecKeychainItemFreeContent(IntPtr.Zero, data);
                SecKeychainItemDelete(item);
                CFRelease(item);
            }
            catch { }
        }
    }
}
