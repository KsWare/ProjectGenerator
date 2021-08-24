using System.IO;

namespace KsWare.ProjectGenerator {

	internal class SolutionFile {

		public string FullName;
		public string Guid;
		public string Content;
		public string NewFullName;

		public SolutionFile(string path) {
			FullName = Path.GetFullPath(path);
		}

		public string Directory;
	}

}