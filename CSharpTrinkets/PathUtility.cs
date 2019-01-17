using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Bazar.Trinkets
{

    public class PathUtility
    {

        #region private static fields

        private static bool _staticDataInitialized = false;
        private static string _standardDirectorySeparator = null;
        private static string _altDirectorySeparator = null;
        private static string _volumeSeparator = null;
        private static string[] _invalidPathCharacters = null;
        private static string[] _invalidFilenameCharacters = null;
        private static string _relativeAnchorCharacter = null;
        private static string[] _validDriveLetters = null;

        #endregion private static fields

        #region private fields

        private string _volume = null;
        private string _relativeAnchor = null;
        private string[] _directories = null;
        private string _filename = null;
        private string _directorySeparator = null;
        private bool _isAbsolute = false;
        private PathUtility _basePath = null;
        private bool _isValid = false;

        #endregion private fields

        #region public properties

        public string Path { get { return getPath(); } }

        public string BasePath { get { return _basePath?.Path; } }

        public bool IsAbsolute { get { return _isAbsolute; } }

        public bool IsValid { get { return _isValid && isBasePathValid(); } }

        public string DirectorySeparator { get { return _directorySeparator; } }

        public string Volume { get { return _volume; } }

        public string RelativeAnchor { get { return _relativeAnchor; } }

        public string[] Directories { get { return _directories; } }

        public string Filename { get { return _filename; } }

        public bool HasVolume { get { return Volume != null; } }

        public bool HasFilename { get { return Filename != null; } }

        public string AbsolutePath { get { return getAbsolutePath(); } }

        public bool Exists
        {
            get
            {
                bool? exists = directoryOrFileExists();
                return exists.HasValue && exists.Value;
            }
        }

        #endregion public properties

        #region constructors and initializers

        protected PathUtility(string basePath, string path, bool? isFile = null)
        {
            initializeStaticData();
            setPath(path, isFile);
            setBasePath(basePath);
        }

        protected PathUtility(string path, bool? isFile = null) : this(null, path, isFile) { }

        public static PathUtility Directory(string basePath, string path) { return new PathUtility(basePath, path, false); }

        public static PathUtility Directory(string path) { return new PathUtility(getCurrentDirectory(), path, false); }

        public static PathUtility File(string basePath, string path) { return new PathUtility(basePath, path, true); }

        public static PathUtility File(string path) { return new PathUtility(getCurrentDirectory(), path, true); }

        public static PathUtility FileOrDirectory(string basePath, string path) { return new PathUtility(basePath, path); }

        public static PathUtility FileOrDirectory(string path) { return new PathUtility(getCurrentDirectory(), path); }

        public static PathUtility GetExecutingDirectory() { return new PathUtility(getExecutingDirectory(), false); }

        public static PathUtility GetCurrentDirectory()
        {
            string currentDirectory = getCurrentDirectory();
            return currentDirectory != null ? new PathUtility(getCurrentDirectory(), false) : null;
        }

        #endregion constructors and initializers

        #region private methods

        private static void initializeStaticData()
        {
            if (!_staticDataInitialized)
            {
                _standardDirectorySeparator = System.IO.Path.DirectorySeparatorChar.ToString();
                _altDirectorySeparator = System.IO.Path.AltDirectorySeparatorChar.ToString();
                _volumeSeparator = System.IO.Path.VolumeSeparatorChar.ToString();
                _invalidPathCharacters = System.IO.Path.GetInvalidPathChars().Select(c => c.ToString()).ToArray();
                _invalidFilenameCharacters = System.IO.Path.GetInvalidFileNameChars().Select(c => c.ToString()).ToArray();
                _relativeAnchorCharacter = ".";
                _validDriveLetters = new string[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
                                                "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };
                _staticDataInitialized = true;
            }
        }

        private void setPath(string path, bool? isFile = null)
        {
            _isValid = true;
            path = getEmptyOrWhiteSpaceAsNull(path, true);
            determineDirectorySeparator(path);
            ensurePathHasNoInvalidCharactersOrInvalidPatterns(path);
            determineIfPathIsAbsolute(path);
            path = extractVolumeFromPath(path);
            path = extractRelativeAnchorFromPath(path);
            extractDirectoriesAndFilenameFromPath(path, isFile);
        }

        private string getPath()
        {
            var s = new StringBuilder();
            string directorySeparator = _directorySeparator ?? _standardDirectorySeparator;
            if (_isAbsolute)
            {
                if (_volume != null)
                {
                    s.Append(_volume);
                    s.Append(_volumeSeparator);
                }
                s.Append(directorySeparator);
            }
            else if (_relativeAnchor != null) s.Append(_relativeAnchor);
            if (_directories != null)
            {
                foreach (string directory in _directories)
                {
                    s.Append(directory);
                    s.Append(directorySeparator);
                }
            }
            if (_filename != null)
            {
                s.Append(_filename);
            }
            return getEmptyOrWhiteSpaceAsNull(s.ToString(), true);
        }

        private string getAbsolutePath()
        {
            string absolutePath = null;
            if (!_isAbsolute)
            {
                if (_isValid && _basePath != null && isBasePathValid())
                {
                    string directorySeparator = _directorySeparator ?? _standardDirectorySeparator;
                    string path = getPath() ?? string.Empty;
                    if (!directorySeparator.Equals(_basePath.DirectorySeparator))
                    {
                        path = path.Replace(directorySeparator, _basePath.DirectorySeparator);
                    }
                    absolutePath = System.IO.Path.GetFullPath(_basePath.Path + path);
                }
            }
            else if (_isAbsolute && _volume == null && _basePath != null && isBasePathValid() && _basePath.Volume != null)
            {
                string directorySeparator = _directorySeparator ?? _standardDirectorySeparator;
                string path = getPath() ?? string.Empty;
                if (!directorySeparator.Equals(_basePath.DirectorySeparator))
                {
                    path = path.Replace(directorySeparator, _basePath.DirectorySeparator);
                }
                absolutePath = _basePath.Volume + _volumeSeparator + path;
            }
            else absolutePath = getPath();
            return getEmptyOrWhiteSpaceAsNull(absolutePath, true);
        }

        private bool? directoryOrFileExists()
        {
            bool? exists = null;
            if (_isValid)
            {
                string absolutePath = AbsolutePath;
                if (absolutePath != null)
                {
                    try
                    {
                        exists = _filename != null
                            ? System.IO.File.Exists(absolutePath)
                            : System.IO.Directory.Exists(absolutePath);
                    }
                    catch { exists = null; }
                }
            }
            return exists;
        }

        private void setBasePath(string basePath)
        {
            _basePath = basePath != null ? new PathUtility(basePath, false) : null;
        }

        private string getEmptyOrWhiteSpaceAsNull(string value, bool trim = false)
        {
            if (string.IsNullOrWhiteSpace(value)) value = null;
            else if (trim) value = value.Trim();
            return value;
        }

        private void determineDirectorySeparator(string path)
        {
            _directorySeparator = null;
            if (path != null)
            {
                if (path.Contains(_standardDirectorySeparator)) _directorySeparator = _standardDirectorySeparator;
                else if (path.Contains(_altDirectorySeparator)) _directorySeparator = _altDirectorySeparator;
            }
        }

        private void ensurePathHasNoInvalidCharactersOrInvalidPatterns(string path)
        {
            // ensure path:
            // 1. does not contain invalid characters
            // 2. does not contain repeated directory separators
            // 3. does not contain both types of directory separators
            if (path != null)
            {
                if (_invalidPathCharacters.Any(c => path.Contains(c)) ||
                    path.Contains(_standardDirectorySeparator + _standardDirectorySeparator) ||
                    path.Contains(_altDirectorySeparator + _altDirectorySeparator) ||
                    (path.Contains(_standardDirectorySeparator) && path.Contains(_altDirectorySeparator)))
                {
                    _isValid = false;
                }
            }
        }

        private void determineIfPathIsAbsolute(string path)
        {
            if (_isValid && path != null) _isAbsolute = System.IO.Path.IsPathRooted(path);
        }

        private string extractVolumeFromPath(string path)
        {
            if (_isValid && _isAbsolute)
            {
                if (_volumeSeparator != null && path.Contains(_volumeSeparator))
                {
                    int volumeSeparatorIndex = path.IndexOf(_volumeSeparator);
                    _volume = path.Substring(0, volumeSeparatorIndex).ToUpper().Trim();
                    path = getEmptyOrWhiteSpaceAsNull(path.Substring(volumeSeparatorIndex + 1), true);
                    _isValid = _validDriveLetters.Any(vdl => vdl.Equals(_volume));
                    if (_isValid && path == null && _volume != null)
                    {
                        if (_directorySeparator == null) _directorySeparator = _standardDirectorySeparator;
                        path = _directorySeparator;
                    }
                }
            }
            return path;
        }

        private string extractRelativeAnchorPartFromPath(string path)
        {
            if (_isValid && !_isAbsolute && _directorySeparator != null && path != null)
            {
                var part = new StringBuilder();
                int firstDirectorySeparatorIndex = path.IndexOf(_directorySeparator);
                if (firstDirectorySeparatorIndex > 0)
                {
                    for (int i = 0; i < firstDirectorySeparatorIndex; i++)
                    {
                        if (path.Substring(i, 1).Equals(_relativeAnchorCharacter))
                        {
                            part.Append(_relativeAnchorCharacter);
                        }
                        else
                        {
                            part.Clear();
                            break;
                        }
                    }
                }
                if (part.Length > 0)
                {
                    int partLength = part.Length;
                    if (partLength == 1)
                    {
                        part.Append(_directorySeparator);
                    }
                    else
                    {
                        part.Clear();
                        for (int i = 0; i < partLength - 1; i++)
                        {
                            part.Append(_relativeAnchorCharacter);
                            part.Append(_relativeAnchorCharacter);
                            part.Append(_directorySeparator);
                        }
                    }
                    if (_relativeAnchor == null) _relativeAnchor = string.Empty;
                    _relativeAnchor += part.ToString();
                    path = getEmptyOrWhiteSpaceAsNull(path.Substring(partLength + 1));
                }
            }
            return path;

        }

        private string extractRelativeAnchorFromPath(string path)
        {

            // possible relative anchor types
            // ./
            // ../
            // ..../
            // ../../../
            // ./../../

            _relativeAnchor = null;
            if (_isValid && !_isAbsolute && _directorySeparator != null && path != null)
            {
                bool pathChanged = true;
                while (pathChanged)
                {
                    string lastPath = path;
                    path = extractRelativeAnchorPartFromPath(path);
                    pathChanged = path != lastPath;
                }
                string currentFolderRelativeAnchor = _relativeAnchorCharacter + _directorySeparator;
                if (_relativeAnchor.Length > currentFolderRelativeAnchor.Length && _relativeAnchor.StartsWith(currentFolderRelativeAnchor))
                {
                    _relativeAnchor = _relativeAnchor.Substring(currentFolderRelativeAnchor.Length);
                }
            }
            return path;
        }

        private void extractDirectoriesAndFilenameFromPath(string path, bool? isFile = null)
        {
            _directories = new string[] { };
            if (_isValid && path != null)
            {
                bool isDirectory = _directorySeparator != null && path.EndsWith(_directorySeparator);
                string[] pathParts = path.Split(new string[] { _directorySeparator }, StringSplitOptions.RemoveEmptyEntries);
                if (!pathParts.Any(p => string.IsNullOrWhiteSpace(p) || (_volumeSeparator != null && p.Contains(_volumeSeparator))))
                {
                    if (isDirectory || (isFile.HasValue && !isFile.Value))
                    {
                        _directories = pathParts;
                        _filename = null;
                        if (isFile.HasValue && isFile.Value) _isValid = false;
                    }
                    else
                    {
                        _directories = pathParts.Take(pathParts.Length - 1).ToArray();
                        _filename = pathParts.Last();
                        if (isFile.HasValue && !isFile.Value) _isValid = false;
                    }
                }
                else _isValid = false;
            }
        }

        private bool isBasePathValid()
        {
            return _basePath == null || (_basePath.IsValid && _basePath.IsAbsolute);
        }

        private static string getExecutingDirectory()
        {
            string executingDirectory = null;
            try
            {
                var location = new Uri(Assembly.GetEntryAssembly().GetName().CodeBase);
                System.IO.DirectoryInfo directory = new System.IO.FileInfo(location.AbsolutePath).Directory;
                executingDirectory = directory.FullName;
            }
            catch { }
            return executingDirectory;
        }

        private static string getCurrentDirectory()
        {
            string currentDirectory = null;
            try
            {
                currentDirectory = System.IO.Directory.GetCurrentDirectory();
            }
            catch { }
            return currentDirectory;
        }

        #endregion private methods

    }

}
