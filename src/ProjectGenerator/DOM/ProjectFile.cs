namespace KsWare.ProjectGenerator {

	internal class ProjectFile {

		public string TypeGuids;
		public string Guid;
		public string Name;
		public string Path;
		public string NewGuid;
		public string NewPath { get; set; }
		public string NewName;
		public string Content;

		public bool IsMain;
		public bool IsSolutionFolder;
		public bool IsExternal;
	}

}