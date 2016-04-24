using System;
using System.Text.RegularExpressions;

namespace Abstractio.GoogleDriveFileSystem
{
    public class Path
    {
        private static readonly Regex _parentPath = new Regex(@".+(?=(/|\\)[^(/|\\)]+$)", RegexOptions.Compiled);
        private static readonly Regex _name = new Regex(@"[^(/|\\)]+$", RegexOptions.Compiled);

        public Path(string path)
        {
            var parentMatch = _parentPath.Match(path);
            if (parentMatch.Success)
            {
                Parent = parentMatch.Value;
            }
            var nameMatch = _name.Match(path);
            if (nameMatch.Success)
            {
                Name = nameMatch.Value;
            }
            else
            {
                throw new ArgumentException("a path must be informed", "path");
            }
        }

        public string Parent { get; set; }
        public string Name { get; set; }
    }
}