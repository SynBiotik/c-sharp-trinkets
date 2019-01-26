using System;
using System.IO;
using System.Linq;

namespace Bazaar.Trinkets
{

    internal static class PathSystemConfig
    {

        private static bool _isInitialized = false;
        private static string _standardDirectorySeparator = null;
        private static string _altDirectorySeparator = null;
        private static string _volumeSeparator = null;
        private static string[] _invalidPathCharacters = null;
        private static string[] _invalidFilenameCharacters = null;
        private static string _relativeAnchorCharacter = null;
        private static string[] _validDriveLetters = null;

        internal static string StandardDirectorySeparator
        {
            get
            {
                ensureIsInitialized();
                return _standardDirectorySeparator;
            }
        }

        internal static string AltDirectorySeparator
        {
            get
            {
                ensureIsInitialized();
                return _altDirectorySeparator;
            }
        }

        internal static string VolumeSeparator
        {
            get
            {
                ensureIsInitialized();
                return _volumeSeparator;
            }
        }

        internal static string[] InvalidPathCharacters
        {
            get
            {
                ensureIsInitialized();
                return _invalidPathCharacters;
            }
        }

        internal static string[] InvalidFilenameCharacters
        {
            get
            {
                ensureIsInitialized();
                return _invalidFilenameCharacters;
            }
        }

        internal static string RelativeAnchorCharacter
        {
            get
            {
                ensureIsInitialized();
                return _relativeAnchorCharacter;
            }
        }

        internal static string[] ValidDriveLetters
        {
            get
            {
                ensureIsInitialized();
                return _validDriveLetters;
            }
        }

        internal static bool? IsDirectoryPathTooLong(string path)
        {
            return isPathTooLong(path, false);
        }

        internal static bool? IsFilePathTooLong(string path)
        {
            return isPathTooLong(path, true);
        }

        private static bool? isPathTooLong(string path, bool isFile)
        {
            // TODO: would be nice if this method didn't depend on exceptions
            bool? pathTooLong = false;
            try
            {
                DirectoryInfo dirInfo = null;
                FileInfo fileInfo = null;
                if (isFile) fileInfo = new FileInfo(path);
                else dirInfo = new DirectoryInfo(path);
            }
            catch (PathTooLongException)
            {
                pathTooLong = true;
            }
            catch (Exception)
            {
                pathTooLong = null;
            }
            return pathTooLong;
        }

        private static void ensureIsInitialized()
        {
            if (!_isInitialized)
            {
                _standardDirectorySeparator = Path.DirectorySeparatorChar.ToString();
                _altDirectorySeparator = Path.AltDirectorySeparatorChar.ToString();
                _volumeSeparator = Path.VolumeSeparatorChar.ToString();
                _invalidPathCharacters = Path.GetInvalidPathChars().Select(c => c.ToString()).ToArray();
                _invalidFilenameCharacters = Path.GetInvalidFileNameChars().Select(c => c.ToString()).ToArray();
                _relativeAnchorCharacter = ".";
                _validDriveLetters = Environment.OSVersion.Platform == PlatformID.Win32NT
                    ? new string[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" }
                    : null;
                _isInitialized = true;
            }
        }

    }

}
