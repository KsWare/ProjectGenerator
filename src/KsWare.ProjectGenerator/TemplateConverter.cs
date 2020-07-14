using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace KsWare.ProjectGenerator {

	internal class TemplateConverter {
		private readonly string _rootFolder;

		private SolutionFile _solutionFile;
		private readonly List<ProjectFile> _projects=new List<ProjectFile>();
		private int _guidCounter = 0;

		public TemplateConverter(string rootFolder) {
			_rootFolder = rootFolder;
		}

		private void FindSolutionFile() {

			foreach (var slnFile in Directory.EnumerateFiles(_rootFolder, "*.sln", SearchOption.AllDirectories)) {
				if (_solutionFile != null) {
					throw new NotSupportedException("More as one solution file found! Operation aborted.");
				}

				_solutionFile = new SolutionFile(slnFile);
				_solutionFile.Directory = Path.GetDirectoryName(slnFile);
			}

			if (_solutionFile == null) {
				throw new InvalidOperationException("No solution file found. Operation aborted.");
			}
		}

		public void Start() {
			Runner.Try(() => {
				FindSolutionFile();

				ProcessSolution(_solutionFile);
				ProcessProjects();


				ProcessSolutionFile(_solutionFile);
				_projects.ForEach(ProcessProjectFile);

				using (var w = File.CreateText(Path.Combine(_rootFolder, ".template")))
				{
					w.WriteLine($"# Created: {DateTime.Now:u}");
					w.WriteLine($"# Author: {Environment.UserName}");
				}
			});
		}

		private void ProcessProjectFile(ProjectFile projectFile) {

			var fullName = Path.Combine(_solutionFile.Directory, projectFile.Path);
			var directory = Path.GetDirectoryName(fullName);
			var newDirectory = Path.GetDirectoryName(Path.Combine(_solutionFile.Directory, projectFile.NewPath));

			File.Delete(fullName);
			if(!string.Equals(directory,newDirectory,StringComparison.OrdinalIgnoreCase))
				Directory.Move(directory, newDirectory);

			var newFullName = Path.Combine(_solutionFile.Directory, projectFile.NewPath);
			using (var w = new StreamWriter(new FileStream(newFullName, FileMode.CreateNew), new UTF8Encoding(true))) {
				w.Write(projectFile.Content);
			}
		}

		private void ProcessSolutionFile(SolutionFile solutionFile) {
			File.Delete(_solutionFile.FullName);
			using (var w = new StreamWriter(new FileStream(solutionFile.NewFullName, FileMode.CreateNew), new UTF8Encoding(true))) {
				w.Write(_solutionFile.Content);
			}
		}

		private void ProcessProjects() {
			foreach (var project in _projects) {
				ProcessProject(project);
			}
		}

		private void ProcessProject(ProjectFile projectFile) {
			ProcessProjectContent(projectFile);
		}

		private void ProcessProjectContent(ProjectFile projectFile) {
			var d = Path.GetDirectoryName(_solutionFile.FullName) ?? throw new InvalidOperationException("No directory");
			var path = Path.Combine(d, projectFile.Path);
			
			using (var r = File.OpenText(path)) { projectFile.Content = r.ReadToEnd(); }
			projectFile.Content = projectFile.Content.Replace(projectFile.Guid, projectFile.NewGuid);
			projectFile.Content = projectFile.Content.Replace(projectFile.Name, projectFile.NewName);
		}

		private void ProcessSolution(SolutionFile solutionFile) {
			ProcessSolutionFileContent(solutionFile);
		}


		private string CreateGuid() {
			return $"{{{++_guidCounter:X8}-0000-FFFF-CDEF-0123456789AB}}";
		}

		private void ProcessSolutionFileContent(SolutionFile solutionFile) {
			// Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Template.WpfCore", "Template.WpfCore\Template.WpfCore.csproj", "{C4D31281-3515-4140-A82A-A038315B4CDA}"

			using (var r = File.OpenText(solutionFile.FullName)) { solutionFile.Content = r.ReadToEnd(); }

			ProcessSolutionFileSolutionGuid(solutionFile);
			ProcessSolutionFileProjects(solutionFile);

			var d = Path.GetDirectoryName(_solutionFile.FullName) ?? throw new InvalidOperationException("No directory.");
			var n = Path.GetFileNameWithoutExtension(_solutionFile.FullName);
			var e = Path.GetExtension(_solutionFile.FullName);

			solutionFile.NewFullName = Path.Combine(d, Variables.SafeSolutionName + e);
		}

		private void ProcessSolutionFileSolutionGuid(SolutionFile solutionFile) {
			// SolutionGuid = {D329D17A-DC39-4C57-9D72-CDF89C29D441}
			var GUID = /*lang=regex*/@"\{[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}\}";
			var pattern = /*lang=regex*/$@"^ SolutionGuid = (?<solutionGuid>{GUID}) $";
			pattern = pattern.Replace(" ", /*lang=regex*/ @"\s*");
			var match = Regex.Match(solutionFile.Content, pattern,RegexOptions.Compiled|RegexOptions.IgnoreCase|RegexOptions.Multiline);

			if(!match.Success) throw new InvalidDataException("Pattern 'SolutionGuid' failed.");

			var guid = CreateGuid();
			solutionFile.Content = solutionFile.Content.Replace(match.Groups["solutionGuid"].Value, guid);
		}

		private void ProcessSolutionFileProjects(SolutionFile solutionFile) {
			var GUID = /*lang=regex*/@"\{[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}\}";
			var pn = /*lang=regex*/@"""(?<projectName>[^""]*)""";
			var pp = /*lang=regex*/@"""(?<projectPath>[^""]*)""";
			var pg = /*lang=regex*/$@"""(?<projectGuid>{GUID})""";
			var pattern = /*lang=regex*/ $@"^ Project\(""{GUID}""\) = {pn},  {pp}, {pg} $";
			pattern = pattern.Replace(" ", /*lang=regex*/ @"\s*");

			var matches = Regex.Matches(solutionFile.Content, pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

			if (matches.Count==0) throw new InvalidDataException("Pattern 'Project' failed.");

			foreach (Match match in matches)
			{
				var project = new ProjectFile
				{
					Guid = match.Groups["projectGuid"].Value,
					Name = match.Groups["projectName"].Value,
					Path = match.Groups["projectPath"].Value
				};
				_projects.Add(project);
			}

			foreach (var project in _projects)
			{
				project.NewGuid = CreateGuid();
				project.NewPath = project.Path.Replace(project.Name, Variables.SafeProjectName);
				project.NewName = Variables.SafeProjectName;
				solutionFile.Content = solutionFile.Content.Replace(project.Guid, project.NewGuid);
				solutionFile.Content = solutionFile.Content.Replace(project.Name, project.NewName);
			}
		}
	}

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

	internal class ProjectFile {
		public string Guid;
		public string Name;
		public string Path;
		public string NewGuid;
		public string NewPath { get; set; }
		public string NewName;
		public string Content;
	}

}