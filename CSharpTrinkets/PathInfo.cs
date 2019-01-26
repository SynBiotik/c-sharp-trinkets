using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Bazaar.Trinkets
{

    public class PathInfo
    {

        #region public properties

        public string Path
        {
            get
            {
                var s = new StringBuilder();
                if (IsAbsolute)
                {
                    if (Volume != null)
                    {
                        s.Append(Volume);
                        s.Append(PathSystemConfig.VolumeSeparator);
                    }
                    s.Append(DirectorySeparator);
                }
                else if (RelativeAnchor != null) s.Append(RelativeAnchor);
                if (Directories != null)
                {
                    foreach (string directory in Directories)
                    {
                        s.Append(directory);
                        s.Append(DirectorySeparator);
                    }
                }
                if (Filename != null) s.Append(Filename);
                string path = s.ToString();
                if (string.IsNullOrWhiteSpace(path)) path = null;
                return path;
            }
        }

        public bool IsAbsolute { get; private set; } = false;

        public bool IsValid { get; private set; } = false;

        public string DirectorySeparator { get; private set; } = null;

        public string Volume { get; private set; } = null;

        public string Root
        {
            get
            {
                string root = null;
                if (IsAbsolute)
                {
                    root = Volume != null
                        ? Volume + PathSystemConfig.VolumeSeparator + DirectorySeparator
                        : DirectorySeparator;
                }
                return root;
            }
        }

        public string RelativeAnchor { get; private set; } = null;

        public string[] Directories { get; private set; } = null;

        public string Filename { get; private set; } = null;

        public bool HasVolume { get { return Volume != null; } }

        public bool HasFilename { get { return Filename != null; } }

        #endregion public properties

        #region constructors and initializers

        protected PathInfo(bool isValid, string directorySeparator, bool isAbsolute, string volume, string relativeAnchor, string[] directories, string filename)
        {
            DirectorySeparator = directorySeparator;
            IsAbsolute = isAbsolute;
            Volume = volume;
            RelativeAnchor = relativeAnchor;
            Directories = directories;
            Filename = filename;
            IsValid = isValid && (IsAbsolute || RelativeAnchor != null) && !(isPathTooLong() ?? false);
        }

        protected PathInfo(string path, bool? isFile)
        {
            IsValid = true;

            // treat empty or whitespace path as null
            if (string.IsNullOrWhiteSpace(path)) path = null;
            IsValid = path != null;

            // ensure path:
            // 1. does not contain invalid characters
            // 2. does not contain repeated directory separators
            // 3. does not contain both types of directory separators
            if (path != null)
            {
                if (PathSystemConfig.InvalidPathCharacters.Any(c => path.Contains(c)) ||
                    path.Contains(PathSystemConfig.StandardDirectorySeparator + PathSystemConfig.StandardDirectorySeparator) ||
                    path.Contains(PathSystemConfig.AltDirectorySeparator + PathSystemConfig.AltDirectorySeparator) ||
                    (path.Contains(PathSystemConfig.StandardDirectorySeparator) && path.Contains(PathSystemConfig.AltDirectorySeparator)))
                {
                    IsValid = false;
                }
            }

            // detect the directory separator
            DirectorySeparator = path != null && path.Contains(PathSystemConfig.AltDirectorySeparator) && !path.Contains(PathSystemConfig.StandardDirectorySeparator)
                ? PathSystemConfig.AltDirectorySeparator
                : PathSystemConfig.StandardDirectorySeparator;


            // determine if path is absolute
            IsAbsolute = IsValid && System.IO.Path.IsPathRooted(path);

            // extract volume from path
            Volume = null;
            if (IsValid && IsAbsolute)
            {
                if (PathSystemConfig.VolumeSeparator != null && path.Contains(PathSystemConfig.VolumeSeparator))
                {
                    int volumeSeparatorIndex = path.IndexOf(PathSystemConfig.VolumeSeparator);
                    Volume = path.Substring(0, volumeSeparatorIndex).ToUpper().Trim();
                    path = path.Substring(volumeSeparatorIndex + 1).Trim();
                    if (string.IsNullOrWhiteSpace(path)) path = DirectorySeparator;
                    if (!PathSystemConfig.ValidDriveLetters.Any(vdl => vdl.Equals(Volume)))
                    {
                        IsValid = false;
                    }
                }
            }

            // extract relative anchor
            // possible relative anchor types
            // ./
            // ../
            // ..../
            // ../../../
            // ./../../
            RelativeAnchor = null;
            if (IsValid && path != null && !IsAbsolute)
            {
                bool pathChanged = true;
                while (pathChanged)
                {
                    string lastPath = path;
                    path = extractRelativeAnchorPartFromPath(path);
                    pathChanged = path != lastPath;
                }
                string currentFolderRelativeAnchor = PathSystemConfig.RelativeAnchorCharacter + DirectorySeparator;
                if (RelativeAnchor.Length > currentFolderRelativeAnchor.Length && RelativeAnchor.StartsWith(currentFolderRelativeAnchor))
                {
                    RelativeAnchor = RelativeAnchor.Substring(currentFolderRelativeAnchor.Length);
                }
            }

            // extract directories and filename from path
            Directories = new string[] { };
            if (IsValid && path != null)
            {
                bool isDirectory = DirectorySeparator != null && path.EndsWith(DirectorySeparator);
                string[] pathParts = path.Split(new string[] { DirectorySeparator }, StringSplitOptions.RemoveEmptyEntries);
                if (!pathParts.Any(p => string.IsNullOrWhiteSpace(p) || (PathSystemConfig.VolumeSeparator != null && p.Contains(PathSystemConfig.VolumeSeparator))))
                {
                    string lastPart = pathParts.Last();
                    if (isDirectory || isFilenameWithoutPathValid(lastPart) || (isFile.HasValue && !isFile.Value))
                    {
                        Directories = pathParts;
                        Filename = null;
                        if (isFile.HasValue && isFile.Value) IsValid = false;
                    }
                    else
                    {
                        Directories = pathParts.Take(pathParts.Length - 1).ToArray();
                        Filename = lastPart;
                        if (isFile.HasValue && !isFile.Value) IsValid = false;
                    }
                }
                else IsValid = false;
            }

            //verify the length of the path if the path is absolute
            IsValid = IsValid && !(isPathTooLong() ?? false);

        }

        protected PathInfo(PathInfo pathInfo)
        {
            IsValid = pathInfo.IsValid;
            DirectorySeparator = pathInfo.DirectorySeparator;
            IsAbsolute = pathInfo.IsAbsolute;
            Volume = pathInfo.Volume;
            RelativeAnchor = pathInfo.RelativeAnchor;
            pathInfo.Directories.CopyTo(Directories, 0);
            Filename = pathInfo.Filename;
        }

        public static PathInfo Directory(string path) { return new PathInfo(path, false); }

        public static PathInfo File(string path) { return new PathInfo(path, true); }

        public static PathInfo FileOrDirectory(string path) { return new PathInfo(path, null); }

        public static PathInfo ExecutingDirectory()
        {
            PathInfo executingDirectory = null;
            try
            {
                var location = new Uri(Assembly.GetEntryAssembly().GetName().CodeBase);
                System.IO.DirectoryInfo directory = new System.IO.FileInfo(location.AbsolutePath).Directory;
                executingDirectory = new PathInfo(directory.FullName, false);
            }
            catch { }
            return executingDirectory;
        }

        public static PathInfo CurrentDirectory()
        {
            PathInfo currentDirectory = null;
            try
            {
                currentDirectory = new PathInfo(System.IO.Directory.GetCurrentDirectory(), false);
            }
            catch { }
            return currentDirectory;
        }

        public static PathInfo RelativeCurrentDirectory()
        {
            string path = PathSystemConfig.RelativeAnchorCharacter + PathSystemConfig.StandardDirectorySeparator;
            return new PathInfo(path, false);
        }

        public PathInfo Clone() { return new PathInfo(this); }

        #endregion constructors and initializers

        #region public methods

        public PathInfo GetPathWithoutFilename()
        {
            return new PathInfo(IsValid, DirectorySeparator, IsAbsolute, Volume, RelativeAnchor, Directories, null);
        }

        public PathInfo GetAbsolutePath(PathInfo rootPath)
        {
            if (!IsValid) throw new InvalidOperationException("The absolute path can't be determined on an invalid path.");
            if (!rootPath.IsValid) throw new ArgumentException("The provided path is not valid.", "rootPath");
            if (!rootPath.IsAbsolute) throw new ArgumentException("The provided path must be absolute.", "rootPath");
            if (rootPath.HasFilename) throw new ArgumentException("The provided path must be a directory.", "rootPath");
            bool isValid = rootPath.IsValid && IsValid;
            string[] directories = null;
            if (!IsAbsolute)
            {
                int traverseUpCount = RelativeAnchor.CountOccurrences(".." + DirectorySeparator);
                int rootDirectoriesToKeepCount = rootPath.Directories.Length - traverseUpCount;
                directories = rootDirectoriesToKeepCount > 0
                    ? rootPath.Directories.Take(rootDirectoriesToKeepCount).ToArray()
                    : new string[0];
            }
            else directories = Directories;
            return new PathInfo(isValid, rootPath.DirectorySeparator, true, rootPath.Volume, null, directories, Filename);
        }

        public PathInfo GetAbsolutePath(string rootPath)
        {
            return GetAbsolutePath(new PathInfo(rootPath, false));
        }

        public bool Exists()
        {
            bool exists = false;
            if (IsValid && IsAbsolute)
            {
                try
                {
                    exists = Filename != null
                        ? System.IO.File.Exists(Path)
                        : System.IO.Directory.Exists(Path);
                }
                catch { }
            }
            return exists;
        }

        public void SetFilename(string filename = null)
        {
            if (filename == null || isFilenameWithoutPathValid(filename)) Filename = filename;
            else throw new ArgumentException("The specified value is not a valid filename.", "value");
        }

        #endregion public methods

        #region private methods

        private string extractRelativeAnchorPartFromPath(string path)
        {
            if (path != null)
            {
                var part = new StringBuilder();
                int firstDirectorySeparatorIndex = path.IndexOf(DirectorySeparator);
                if (firstDirectorySeparatorIndex > 0)
                {
                    for (int i = 0; i < firstDirectorySeparatorIndex; i++)
                    {
                        if (path.Substring(i, 1).Equals(PathSystemConfig.RelativeAnchorCharacter))
                        {
                            part.Append(PathSystemConfig.RelativeAnchorCharacter);
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
                    if (partLength == 1) part.Append(DirectorySeparator);
                    else
                    {
                        part.Clear();
                        for (int i = 0; i < partLength - 1; i++)
                        {
                            part.Append(PathSystemConfig.RelativeAnchorCharacter);
                            part.Append(PathSystemConfig.RelativeAnchorCharacter);
                            part.Append(DirectorySeparator);
                        }
                    }
                    if (RelativeAnchor == null) RelativeAnchor = string.Empty;
                    RelativeAnchor += part.ToString();
                    path = path.Substring(partLength + 1).Trim();
                    if (path == string.Empty) path = null;
                }
            }
            return path;
        }

        private bool isFilenameWithoutPathValid(string filename)
        {
            return !filename.Any(c => c.Equals(PathSystemConfig.StandardDirectorySeparator) ||
                                 c.Equals(PathSystemConfig.AltDirectorySeparator) ||
                                 c.Equals(PathSystemConfig.VolumeSeparator) ||
                                 PathSystemConfig.InvalidFilenameCharacters.Any(i => i.Equals(c)));
        }

        //private PathReadableResult getPathReadability()
        //{
        //    // TODO: change this code to not depend on exceptions.
        //    PathReadableResult pathReadableResult = PathReadableResult.Readable;
        //    if (IsValid)
        //    {
        //        if (IsAbsolute || BasePath != null)
        //        {
        //            try
        //            {
        //                PathInfo absolutePathWithoutFilename = GetPathWithoutFilename().GetAbsolutePath();
        //                var files = System.IO.Directory.EnumerateFiles(absolutePathWithoutFilename.Path);
        //            }
        //            catch (UnauthorizedAccessException)
        //            {
        //                pathReadableResult = PathReadableResult.UserNotAuthorized;
        //            }
        //            catch (System.IO.DirectoryNotFoundException)
        //            {
        //                pathReadableResult = PathReadableResult.Inexistent;
        //            }
        //            catch (System.IO.PathTooLongException)
        //            {
        //                pathReadableResult = PathReadableResult.Invalid;
        //            }
        //        }
        //        else pathReadableResult = PathReadableResult.Incomplete;
        //    }
        //    else pathReadableResult = PathReadableResult.Invalid;
        //    return pathReadableResult;
        //}

        private bool? isPathTooLong()
        {
            bool? pathTooLong = null;
            if (IsValid && IsAbsolute)
            {
                pathTooLong = HasFilename ? PathSystemConfig.IsFilePathTooLong(Path) : PathSystemConfig.IsDirectoryPathTooLong(Path);
            }
            return pathTooLong;
        }

        #endregion private methods

    }

}
