using System.Collections.Generic;
using System.Text;

namespace Abstractio.GoogleDriveFileSystem
{
    public class Folder
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string ParentId { get; set; }
        public Folder Parent { get; set; }
        public IList<Folder> Childs { get; set; }
        public string FullName { get; set; }

        public void CreateFullName()
        {
            var sb = new StringBuilder();
            sb.Append(Name);
            var folder = Parent;
            while (folder != null)
            {
                sb.Insert(0, folder.Name + "/");
                folder = folder.Parent;
            }
            FullName = sb.ToString();
        }
    }
}