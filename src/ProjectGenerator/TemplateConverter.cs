using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace KsWare.ProjectGenerator {

	internal class TemplateConverter {

		private static readonly string[] TextFileExtensions = {".txt", ".cs", ".vb", ".xml", ".xaml", ".resx", ".txt", ".md"};
		private static readonly string[] ExcludeDirectories = {".vs", ".git", "_ReSharper.Caches","bin","obj"};

		private readonly string _rootFolder;
		private SolutionFile _solutionFile;
		private readonly List<ProjectFile> _projects=new List<ProjectFile>();
		private int _guidCounter = 0;
		private readonly List<Tuple<string, string>> _replacements = new List<Tuple<string, string>>();

		public TemplateConverter(string rootFolder) {
			_rootFolder = rootFolder;
		}


		public void Start() {
			Runner.Try(() => {
				FindSolutionFile();

				ProcessSolutionFileContent(_solutionFile);
				ProcessProjects();

				ProcessSolutionFile(_solutionFile);
				_projects.ForEach(ProcessProjectFile);
				ProcessAllTextFiles(_rootFolder);

				using (var w = File.CreateText(Path.Combine(_rootFolder, ".template")))
				{
					w.WriteLine($"# Created: {DateTime.Now:u}");
					w.WriteLine($"# Author: {Environment.UserName}");
				}
			});
		}

		private void ProcessAllTextFiles(string path) {

			_replacements.Sort((a, b) => a.Item1.Length.CompareTo(b.Item1.Length) * -1);

			foreach (var file in Directory.EnumerateFiles(path)) {
				var ext = Path.GetExtension(file)?.ToLowerInvariant();
				if (!TextFileExtensions.Contains(ext)) {
					Debug.WriteLine($"Skip non text file. {file}");
					continue;
				}
				ProcessTextFile(file);
			}
			foreach (var directory in Directory.EnumerateDirectories(path)) {
				if (FileTools.IsPathExcluded(path, ExcludeDirectories)) {
					Debug.WriteLine($"Skip excludes path. {path}");
					continue;
				}
				ProcessAllTextFiles(path);
			}
		}

		private void ProcessTextFile(string path) {
			using var r = File.OpenText(path);
			var content = new StringBuilder(r.ReadToEnd());
			r.Close();

			foreach (var replacement in _replacements) {
				content = content.Replace(replacement.Item1, replacement.Item2);
			}

			using var w = File.CreateText(path);
			w.Write(content);
			w.Close();
		}

		private void ProcessProjectFile(ProjectFile projectFile) {
			if(projectFile.IsSolutionFolder) return;

			var fullName = Path.Combine(_solutionFile.Directory, projectFile.Path);
			var directory = Path.GetDirectoryName(fullName);
			var newDirectory = Path.GetDirectoryName(Path.Combine(_solutionFile.Directory, projectFile.NewPath));

			File.Delete(fullName);
			if(!string.Equals(directory,newDirectory,StringComparison.OrdinalIgnoreCase))
				Directory.Move(directory, newDirectory);

			var newFullName = Path.Combine(_solutionFile.Directory, projectFile.NewPath);
			using var w = new StreamWriter(new FileStream(newFullName, FileMode.CreateNew), new UTF8Encoding(true));
			w.Write(projectFile.Content);
		}

		private void ProcessSolutionFile(SolutionFile solutionFile) {
			File.Delete(_solutionFile.FullName);
			using (var w = new StreamWriter(new FileStream(solutionFile.NewFullName, FileMode.CreateNew), new UTF8Encoding(true))) {
				w.Write(_solutionFile.Content);
			}
		}

		private void ProcessProjects() {
			foreach (var project in _projects) {
				ProcessProjectContent(project);
			}
		}


		private void ProcessProjectContent(ProjectFile projectFile) {
			if(projectFile.IsSolutionFolder) return;

			var d = Path.GetDirectoryName(_solutionFile.FullName) ?? throw new InvalidOperationException("No directory");
			var path = Path.Combine(d, projectFile.Path);

			using var r = File.OpenText(path);
			projectFile.Content = r.ReadToEnd();

			projectFile.Content = projectFile.Content.Replace(projectFile.Guid, projectFile.NewGuid);
			projectFile.Content = projectFile.Content.Replace(projectFile.Name, projectFile.NewName);

				// <RootNamespace>KsWare.ProjectGenerator</RootNamespace>
				// <AssemblyName>KsWare.ProjectGenerator</AssemblyName>
				// <Authors>KsWare</Authors>
				// <Company>KsWare</Company> optional, if missing the <Authors> is used
				// <OutputPath>bin\Debug\</OutputPath>

			var doc = XDocument.Parse(projectFile.Content);
			var ns = doc.Root?.GetDefaultNamespace()?.NamespaceName;
			var company = doc.Descendants(XName.Get("Company", ns)).FirstOrDefault()?.Value;
			var authors = doc.Descendants(XName.Get("Authors", ns)).FirstOrDefault()?.Value;
			if (company != null) {
				_replacements.Add(new Tuple<string, string>(company, Variables.Company));
			}
			else if(authors!=null) {
				_replacements.Add(new Tuple<string, string>(authors.Split(',')[0], Variables.Company));
			}
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
			var pattern = /*lang=regex*/ $@"^ Project\(""(?<projectTypeGuids>{GUID}( , {GUID})*)""\) = {pn},  {pp}, {pg} $";
			pattern = pattern.Replace(" ", /*lang=regex*/ @"\s*");

			var matches = Regex.Matches(solutionFile.Content, pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

			if (matches.Count==0) throw new InvalidDataException("Pattern 'Project' failed.");

			foreach (Match match in matches)
			{
				var project = new ProjectFile
				{
					TypeGuids=match.Groups["projectTypeGuids"].Value,
					Guid = match.Groups["projectGuid"].Value,
					Name = match.Groups["projectName"].Value,
					Path = match.Groups["projectPath"].Value,
					IsSolutionFolder = match.Groups["projectTypeGuids"].Value.Contains("{2150E333-8FDC-42A3-9474-1A3956D46DE8}"),
					IsExternal = match.Groups["projectPath"].Value.StartsWith("..")
				};

				_projects.Add(project);
			}

			var realProjects = _projects.Where(x => x.IsSolutionFolder == false).ToArray();
			if(realProjects.Length<2) return; // nothing to do

			// find "main" project
			// sample => sample.test, sample.interfaces, ...


			// a) by solution name
			var solutionName = Path.GetFileNameWithoutExtension(_solutionFile.FullName);
			var mainProject = realProjects.FirstOrDefault(x => x.Name == solutionName);
			
			
			// b) by shortest name
			if (mainProject == null) {
				// sort array by length of project name
				Array.Sort(realProjects, (a, b) => a.Name.Length.CompareTo(b.Name.Length));
				var shortestName = realProjects.First().Name;
				mainProject = realProjects.First(x => x.Name == shortestName);
				
			}

			if (mainProject == null) {
				throw new InvalidOperationException("Project structure not supported.");
			}

			mainProject.IsMain = true;
			_replacements.Add(new Tuple<string, string>(mainProject.Name, Variables.SafeProjectName));

			//check if all project starts with main project name
			if(!realProjects.Any(x=>x.Name.StartsWith(mainProject.Name))) {
				throw new InvalidOperationException("Project structure not supported.");
			}

			// sort by name length descent, so we can rename longest name first
			_projects.Sort((a, b) => a.Name.Length.CompareTo(b.Name.Length) * -1);
			foreach (var project in _projects) {
				if (project.IsSolutionFolder) {
					project.NewGuid = CreateGuid();
					solutionFile.Content = solutionFile.Content.Replace(project.Guid, project.NewGuid);
				}
				else if(project.IsExternal) continue;
				else {
					project.NewGuid = CreateGuid();
					project.NewPath = project.Path.Replace(mainProject.Name, Variables.SafeProjectName);
					project.NewName = project.Name.Replace(mainProject.Name, Variables.SafeProjectName);

					solutionFile.Content = solutionFile.Content.Replace(project.Guid, project.NewGuid);
					solutionFile.Content = solutionFile.Content.Replace(project.Name, project.NewName);
				}
			}
		}

		private void FindSolutionFile() {

			foreach (var slnFile in Directory.EnumerateFiles(_rootFolder, "*.sln", SearchOption.AllDirectories)) {
				if (_solutionFile != null) {
					throw new NotSupportedException("More as one solution file found! Operation aborted.");
				}

				_solutionFile = new SolutionFile(slnFile) {
					Directory = Path.GetDirectoryName(slnFile)
				};
			}

			if (_solutionFile == null) {
				throw new InvalidOperationException("No solution file found. Operation aborted.");
			}
		}


		// supported special cases:
		// - TODO has more as one project 
		// - has "Solution Items"
		// - has renamed "Solution Items"
		// - has external projects
	}

}