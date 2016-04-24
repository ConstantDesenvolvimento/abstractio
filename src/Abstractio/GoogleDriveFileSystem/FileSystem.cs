using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abstractio.Caching;
using Abstractio.Logging;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using File = Google.Apis.Drive.v3.Data.File;

namespace Abstractio.GoogleDriveFileSystem
{
    /// <summary>
    ///     A File System Abstraction on top of google drive
    /// </summary>
    public class FileSystem : IFileSystem
    {
        private static readonly ILog Logger = LogProvider.For<FileSystem>();
        private readonly ICache _cache;
        private readonly DriveService _service;


        public FileSystem(BaseClientService.Initializer initializer, ICache cache)
        {
            _cache = cache;
            _service = new DriveService(initializer);
            //Let's naivily load all folders and cache it.
            //Problems
            //Disconected: If others create directories we will not see it
            //Memory: If the drive has a huge amount of directories can hurt memory usage
            //Good
            //Speed: We dont need to query google for folder ids
            LoadFolders();
        }

        public IEnumerable<string> ListFiles(string path)
        {
            var folderId = FindFolderIdByName(path);
            if (folderId == null)
            {
                throw new DirectoryNotFoundException();
            }
            string pageToken = null;
            do
            {
               
                    var request = _service.Files.List();
                    request.Q = "'" + folderId + "' in parents and mimeType != 'application/vnd.google-apps.folder'";
                    request.Fields = "files/name,nextPageToken";
                    request.Spaces = "drive";
                    request.PageToken = pageToken;
                    request.Corpus = FilesResource.ListRequest.CorpusEnum.Domain;
                    var files = request.Execute();
                    pageToken = files.NextPageToken;
                    foreach (var file in files.Files)
                    {
                        yield return file.Name;
                    }
                
            } while (!string.IsNullOrEmpty(pageToken));
        }

        public IEnumerable<string> ListFolders(string path)
        {
            var folder = GetFolderByName(path);
            if (folder == null)
            {
                throw new DirectoryNotFoundException();
            }
            if (folder.Childs != null)
            {
                return folder.Childs.Select(f => f.Name).ToList();
            }
            return new string[0];
        }

        public void CreateFolder(string path)
        {
            try
            {
                var folderPath = new Path(path);
                Folder parentFolder = null;
                if (folderPath.Parent != null)
                {
                    EnsureFolderExists(folderPath.Parent);
                    parentFolder = GetFolderByName(folderPath.Parent);
                    if (parentFolder == null)
                    {
                        throw new DirectoryNotFoundException();
                    }
                }
                var fileMetadata = new File
                {
                    Name = folderPath.Name,
                    MimeType = "application/vnd.google-apps.folder"
                };
                if (parentFolder != null)
                {
                    fileMetadata.Parents = new List<string> {parentFolder.Id};
                }
                var request = _service.Files.Create(fileMetadata);
                request.Fields = "id";

                var file = request.Execute();
                var newFolder = new Folder {Name = folderPath.Name, Id = file.Id, Parent = parentFolder};
                newFolder.CreateFullName();
                RegisterFolder(newFolder);
                if (parentFolder != null)
                {
                    if (parentFolder.Childs == null)
                    {
                        parentFolder.Childs = new List<Folder>();
                    }
                    parentFolder.Childs.Add(newFolder);
                }
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error trying to create folder {path} ", ex, path);
                throw;
            }
        }


        public void DeleteFolder(string path)
        {
            var folder = GetFolderByName(path);
            if (folder == null)
            {
                throw new DirectoryNotFoundException();
            }
            _service.Files.Delete(folder.Id).Execute();
            _cache.Remove(folder.FullName);
            _cache.Remove(folder.Id);
        }

        public void SaveFile(string path, Stream content, string mimeType)
        {
            var filePath = new Path(path);
            var fileMetadata = new File
            {
                Name = filePath.Name
            };
            if (filePath.Parent != null)
            {
                EnsureFolderExists(filePath.Parent);
                var folder = GetFolderByName(filePath.Parent);
                fileMetadata.Parents = new List<string> {folder.Id};
            }
            var request = _service.Files.Create(fileMetadata, content, mimeType);
            request.Fields = "id";
            request.Upload();
        }

        private void EnsureFolderExists(string parent)
        {
            var path=new Path(parent);
            if (path.Parent != null)
            {
                EnsureFolderExists(path.Parent);
            }
            var folder = GetFolderByName(parent);
            if (folder == null)
            {
                CreateFolder(parent);
            }
        }

        private string GetFileIdByPath(string path)
        {
            var filePath = new Path(path);
            var request = _service.Files.List();
            request.Q = "name = '" + filePath.Name + "' and mimeType != 'application/vnd.google-apps.folder'";
            if (filePath.Parent != null)
            {
                var folder = GetFolderByName(filePath.Parent);
                request.Q = request.Q + " and '" + folder.Id + "' in parents";
            }
            request.Fields = "files/id";
            request.Spaces = "drive";
            request.Corpus = FilesResource.ListRequest.CorpusEnum.Domain;
            var files = request.Execute();
            if (files.Files.Any())
            {
                return files.Files.First().Id;
            }
            return null;
        }
        public void DeleteFile(string path)
        {
            var fileId = GetFileIdByPath(path);
            if (fileId!=null)
            {
                _service.Files.Delete(fileId).Execute();
            }
            else
            {
                throw new FileNotFoundException();
            }
        }

        public Stream ReadFile(string path)
        {
            var fileId = GetFileIdByPath(path);
            if (fileId != null)
            {
                var tempFileName = System.IO.Path.GetTempFileName();
                using (var stream= System.IO.File.OpenWrite(tempFileName))
                {
                    var request=_service.Files.Get(fileId);
                    request.Download(stream);

                }
                return System.IO.File.OpenRead(tempFileName);
            }
            throw new FileNotFoundException();
        }

        public IEnumerable<string> SearchFiles(string pattern)
        {
            string pageToken = null;
            do
            {
                var request = _service.Files.List();
                request.Q = "name contains '" + pattern + "' and mimeType != 'application/vnd.google-apps.folder'";
                request.Fields = "files(name,parents),nextPageToken";
                request.Spaces = "drive";
                request.Corpus = FilesResource.ListRequest.CorpusEnum.Domain;
                request.PageToken = pageToken;
                var files = request.Execute();
                pageToken = files.NextPageToken;
                foreach (var file in files.Files)
                {
                    yield return file.Name;
                }
            } while (!string.IsNullOrEmpty(pageToken));
        }

        public IEnumerable<string> SearchFolders(string pattern)
        {
            string pageToken = null;
            do
            {
                var request = _service.Files.List();
                request.Q = "name contains '" + pattern + "' and mimeType = 'application/vnd.google-apps.folder'";
                request.Fields = "files(name,parents),nextPageToken";
                request.Spaces = "drive";
                request.Corpus = FilesResource.ListRequest.CorpusEnum.Domain;
                request.PageToken = pageToken;
                var files = request.Execute();
                pageToken = files.NextPageToken;
                foreach (var file in files.Files)
                {
                    yield return file.Name;
                }
            } while (!string.IsNullOrEmpty(pageToken));
        }

        public bool FileExists(string path)
        {
            return GetFileIdByPath(path)!=null;
            
        }

        public bool FolderExists(string path)
        {
            var folder = _cache.Get(path);
            return folder != null;
        }

        private string FindFolderIdByName(string path)
        {
            var folder = GetFolderByName(path);
            if (folder == null)
            {
                return null;
            }
            return folder.Id;
        }

        private Folder GetFolderByName(string path)
        {
            var normalized = path.Replace("\\", "/");
            var folder = _cache.Get(normalized) as Folder;
            if (folder == null)
            {
                //What To Do, try to rescan?? Is it a cache miss or the directory does not exists?
                //For now let's believe that the cache never loses its values and that everything that exists is in the cache
                return null;
            }
            return folder;
        }

        private void LoadFolders()
        {
            try
            {
                var folders = new Dictionary<string, Folder>();
                string pageToken = null;
                do
                {
                    var request = _service.Files.List();

                    request.Fields = "files(id,name,parents),nextPageToken";
                    request.Q = "mimeType = 'application/vnd.google-apps.folder'";
                    request.Spaces = "drive";
                    request.PageToken = pageToken;
                    request.Corpus = FilesResource.ListRequest.CorpusEnum.Domain;

                    var files = request.Execute();
                    pageToken = files.NextPageToken;
                    foreach (var file in files.Files)
                    {
                        if (file.Parents != null)
                        {
                            foreach (var parent in file.Parents)
                            {
                                folders.Add(file.Id, new Folder {Name = file.Name, Id = file.Id, ParentId = parent});
                            }
                        }
                        else
                        {
                            folders.Add(file.Id, new Folder { Name = file.Name, Id = file.Id });
                        }
                    }
                } while (!string.IsNullOrEmpty(pageToken));
                foreach (var folder in folders.Values)
                {
                    if (folder.ParentId != null && folder.Parent == null)
                    {
                        Folder parent = null;
                        folders.TryGetValue(folder.ParentId, out parent);
                        folder.Parent = parent;
                    }
                }
                foreach (var folder in folders.Values)
                {
                    folder.CreateFullName();
                    folder.Childs = folders.Values.Where(f => f.Parent == folder).ToList();
                }
                foreach (var folder in folders.Values)
                {
                    //Put in cache
                    RegisterFolder(folder);
                }
            }
            catch (Exception ex)
            {
                Logger.ErrorException(ex.Message, ex);
                throw;
            }
        }

        private void RegisterFolder(Folder folder)
        {
            _cache.Set(folder.Id, folder);
            _cache.Set(folder.FullName, folder);
        }
    }
}