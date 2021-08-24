using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Xml.Serialization;
using KsWare.Presentation.Lite;
using Newtonsoft.Json;

namespace KsWare.ProjectGenerator {

	public class ShellViewModel : NotifyPropertyChangedBase {

		private Dictionary<string, string> _guids = new Dictionary<string, string>();
		private Dictionary<string, string> _replacementsDictionary = new Dictionary<string, string>();
		private string _projectName;
		private Settings _data;
		private readonly JsonSerializer _dataSerializer;

		private readonly string _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"KsWare", "ProjectGenerator", "1.0", "settings.dat");

		/// <inheritdoc />
		public ShellViewModel() {
			_dataSerializer = JsonSerializer.CreateDefault();

			LoadSettings();

			TemplatesRootPath = @"D:\Develop\Extern\GitHub.KsWare"; //TODO load from setting
			DestinationRootPath = @"D:\Develop\Extern\GitHub.KsWare"; //TODO load from setting
			Company = "KsWare"; //TODO load from settings
			BranchName = "develop";
			Commit = true;

			FindTemplates();

			CreateCommand = new RelayCommand(_ => CreateProjectAction());
			CopyCommand = new RelayCommand(_ => CopyTemplateAction());
			RenameCommand = new RelayCommand(_ => RenameTemplateAction(), _ => false);
			ConvertCommand = new RelayCommand(_ => ConvertProjectToTemplateAction());

			Application.Current.Exit += (s, e) => SaveSettings();
		}

		public RelayCommand ConvertCommand { get; }

		public ICommand RenameCommand { get; }

		public ICommand CopyCommand { get; }

		public ICommand CreateCommand { get; }

		public string TemplatesRootPath { get => _data.TemplatesRootPath; set => Set(ref _data.TemplatesRootPath, value); }
		public string Company { get => _data.Company; set => Set(ref _data.Company, value); }
		public string SelectedTemplate { get => _data.TemplateName; set => Set(ref _data.TemplateName, value); }
		public string ProjectName { get => _projectName; set => Set(ref _projectName, value); }
		public string DestinationRootPath { get => _data.DestinationRootPath; set => Set(ref _data.DestinationRootPath, value); }
		public bool CreateRepository { get => _data.CreateRepository; set => Set(ref _data.CreateRepository, value); }
		public string BranchName { get => _data.BranchName; set => Set(ref _data.BranchName, value); }
		public bool Commit { get => _data.Commit; set => Set(ref _data.Commit, value); }
		public bool IsOpenFolderRequested { get => _data.OpenFolder; set => Set(ref _data.OpenFolder, value); }
		public bool IsOpenSolutionRequested { get => _data.OpenSolution; set => Set(ref _data.OpenSolution, value); }
		
		public ObservableCollection<string> Templates { get; } = new ObservableCollection<string>();

		private void CreateProjectAction() {
			var templatePath = Path.Combine(TemplatesRootPath, SelectedTemplate);
			
			var destinationDirectory = new DirectoryInfo(Path.Combine(DestinationRootPath, ProjectName));


			CreateReplacements(ProjectName);
			if (destinationDirectory.Exists) {
				MessageBox.Show("Directory already exists!", "Information", MessageBoxButton.OK, MessageBoxImage.Hand);
				return;
			}

			destinationDirectory.Create();

			if (CreateRepository) {
				if (0 != Git("init")) return;
				if (0 != Git($"checkout -b {BranchName}")) return;
			}

			var co = new CopyOptions {
				ReplaceGuids = true,
				Mode=GenerationMode.Create
			};
			Debug.WriteLine($"> Processing template...");
			Copy(new DirectoryInfo(templatePath), destinationDirectory, co, new CopyStats());

			if (CreateRepository && Commit) {
				if (0 != Git("add *")) return;
				if (0 != Git("commit -m \"create from template\"")) return;
			}

			var solutionPath = destinationDirectory.EnumerateFiles(
					$"{_replacementsDictionary[Variables.SafeSolutionName]}.sln", SearchOption.AllDirectories)
				.FirstOrDefault()?.FullName;

			if (0 != Nuget($"restore {solutionPath}")) { }
			
			;

			// Process.Start(_destinationDirectory.FullName)
			// System.ComponentModel.Win32Exception: 'Zugriff verweigert' ??!
			// Process.Start(solutionPath);
			// Win32Exception: 'The specified executable is not a valid application for this OS platform.'
			// in .NET Core we have to use 'UseShellExecute = true' explicitly

			if(IsOpenFolderRequested)
				Process.Start(new ProcessStartInfo(destinationDirectory.FullName) { UseShellExecute = true });

			if(IsOpenSolutionRequested)
				Process.Start(new ProcessStartInfo(solutionPath) { UseShellExecute = true });

		}

		public string SafeProjectName { get; set; }

		public string SafeSolutionName { get; set; }

		public string RepositoryName { get; set; }

		private void CopyTemplateAction() {
			var templatePath = Path.Combine(TemplatesRootPath, SelectedTemplate);
			var destinationDirectory = new DirectoryInfo(Path.Combine(DestinationRootPath, ProjectName));

			if (destinationDirectory.Exists) {
				MessageBox.Show("Directory already exists!", "Information", MessageBoxButton.OK, MessageBoxImage.Hand);
				return;
			}

			CreateReplacements(ProjectName);
			_replacementsDictionary = new Dictionary<string, string>();
			_guids=new Dictionary<string, string>();

			destinationDirectory.Create();
			var co = new CopyOptions {
				ReplaceGuids = false,
				Mode = GenerationMode.Copy
			};
			Debug.WriteLine($"> Processing template...");
			Copy(new DirectoryInfo(templatePath), destinationDirectory, co,new CopyStats());

			Process.Start(destinationDirectory.FullName);
			Process.Start(Path.Combine(destinationDirectory.FullName, "src", $"{SafeSolutionName}.sln"));
		}

		private void CreateReplacements(string destination) {
			RepositoryName = destination;
			SafeSolutionName = destination;
			SafeProjectName = destination;
			_replacementsDictionary = new Dictionary<string, string>();
			_replacementsDictionary.Add(Variables.SafeProjectName, destination);
			_replacementsDictionary.Add(Variables.SafeSolutionName, destination);
			_replacementsDictionary.Add(Variables.Company, Company); 
			_replacementsDictionary.Add(Variables.Year, DateTime.Today.Year.ToString());

			for (int i = 0; i < 20; i++) { _guids.Add($"{{{i:D8}-0000-FFFF-BCDE-0123456789AB}}", Guid.NewGuid().ToString("B").ToUpper()); }
		}

		private int Git(string arguments) => Cmd($"git {arguments}");

		private int Nuget(string arguments) => Cmd($"nuget {arguments}");

		private int Cmd(string cmdline) {
			Debug.WriteLine($"> {cmdline}");
			var cmd = cmdline.Split(new[] {' '}, 2);
			switch (cmd[0]) {
				case "git":
					cmd[0] = FileTools.GetGitPath(true);
					break;
				case "nuget":
					cmd[0] = FileTools.GetNugetPath(true);
					break;
			}

			if (cmd[0] == null) {
				Debug.WriteLine($"Executable not configured! Command: {cmdline}");
				return -1;
			}

			var psi = new ProcessStartInfo(cmd[0], cmd[1]) {
				WorkingDirectory = Path.Combine(DestinationRootPath, ProjectName),
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};

			var process = Process.Start(psi);

			while (!process.StandardOutput.EndOfStream) {
				var line = process.StandardOutput.ReadLine();
				Debug.WriteLine($"{line}");
			}

			while (!process.StandardError.EndOfStream) {
				var line = process.StandardError.ReadLine();
				Debug.WriteLine($"!: {line}");
			}

			return process.ExitCode;
		}

		private void Copy(DirectoryInfo sourceDirectory, DirectoryInfo destinationDirectory, CopyOptions options, CopyStats stat) {
			if (sourceDirectory.Name.StartsWith(".", StringComparison.OrdinalIgnoreCase)) return;
			if (sourceDirectory.FullName.EndsWith("\\bin", StringComparison.OrdinalIgnoreCase)) return;
			if (sourceDirectory.FullName.EndsWith("\\obj", StringComparison.OrdinalIgnoreCase)) return;
			if (sourceDirectory.FullName.EndsWith("\\packages", StringComparison.OrdinalIgnoreCase)) return;
			if (sourceDirectory.FullName.EndsWith("_ReSharper.Caches", StringComparison.OrdinalIgnoreCase)) return;
			destinationDirectory.Create();
			foreach (var file in sourceDirectory.EnumerateFiles()) {
				if(stat.Level==0 && file.Name.Equals(".template",StringComparison.OrdinalIgnoreCase)) continue;
				var fileName = ReplacePlaceholders(file.Name, options);
				Copy(file, new FileInfo(Path.Combine(destinationDirectory.FullName, fileName)), options);
			}

			stat.Level += 1;
			foreach (var directory in sourceDirectory.EnumerateDirectories()) {
				var name = ReplacePlaceholders(directory.Name, options);
				Copy(directory, new DirectoryInfo(Path.Combine(destinationDirectory.FullName, name)), options, stat);
			}
			stat.Level -= 1;
		}

		private string ReplacePlaceholders(string s, CopyOptions options) {
			//TODO optimize;
			if (options.ReplaceGuids) {
				foreach (var guid in _guids) { s = s.Replace(guid.Key, guid.Value); }

			}
			foreach (var replacement in _replacementsDictionary) { s = s.Replace(replacement.Key, replacement.Value); }

			return s;
		}

		private void Copy(FileInfo sourceFile, FileInfo destinationFile, CopyOptions options) {
			sourceFile.CopyTo(destinationFile.FullName, false);
			ReplaceText(destinationFile, options);
		}

		private void ReplaceText(FileInfo file, CopyOptions options) {
			if (file.FullName.EndsWith(".snk", StringComparison.OrdinalIgnoreCase)) return;
			string text = null;
			using (var r = file.OpenText())
				text = r.ReadToEnd();

			text = ReplacePlaceholders(text, options);

			using (var w = new StreamWriter(file.Open(FileMode.Create, FileAccess.Write), Encoding.UTF8))
				w.Write(text);
		}

		private void RenameTemplateAction() {
			// TODO RenameTemplateAction()
			MessageBox.Show("Sorry. Function not implemented.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		private void ConvertProjectToTemplateAction() {
			var dlg=new SelectFolderDialog() {
				Title = "Select folder to convert...",
			};

			if(dlg.ShowDialog()!=true) return;

			new TemplateConverter(dlg.FolderName).Start();
		}

		private void FindTemplates() {
			Templates.Clear();
			var root=new DirectoryInfo(TemplatesRootPath);
			var templates = new List<string>();
			foreach (var directory in root.GetDirectories()) {
				var template=new FileInfo(Path.Combine(directory.FullName,".template"));
				if(!template.Exists) continue;
				Templates.Add(directory.Name);
			}
		}

		private void LoadSettings() {
			if (File.Exists(_dataPath)) {
				using var reader = new JsonTextReader(File.OpenText(_dataPath));
				_data = _dataSerializer.Deserialize<Settings>(reader);
			}
			else {
				_data = new Settings();
			}
		}

		private void SaveSettings() {
			Directory.CreateDirectory(Path.GetDirectoryName(_dataPath));
			if(File.Exists(_dataPath)) File.Delete(_dataPath);
			using var writer = new JsonTextWriter(File.CreateText(_dataPath)) { Formatting = Formatting.Indented };
			_dataSerializer.Serialize(writer, _data);
		}
	}

	public class Settings {
		// [Major.Minor] Major changes are breaking changes. Minor changes are compatible changes (add fields)
		public string FormatVersion = "1.1";
		public string TemplatesRootPath;
		public string DestinationRootPath;
		public string Company;
		public string TemplateName;
		public bool CreateRepository;
		public string BranchName;
		public bool Commit;
		public bool OpenFolder; // 1.1
		public bool OpenSolution; // 1.1
	}

}
