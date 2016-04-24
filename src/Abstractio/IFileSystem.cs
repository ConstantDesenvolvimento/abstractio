using System.Collections.Generic;
using System.IO;

namespace Abstractio
{
    public interface IFileSystem
    {
        IEnumerable<string> ListFiles(string path);
        IEnumerable<string> ListFolders(string path);
        void CreateFolder(string path);
        void DeleteFolder(string path);
        void SaveFile(string path, Stream content);
        void DeleteFile(string path);
        Stream ReadFile(string path);
        IEnumerable<string> SearchFiles(string pattern);
        IEnumerable<string> SearchFolders(string pattern);
        bool FileExists(string path);
        bool FolderExists(string path);
    }
}