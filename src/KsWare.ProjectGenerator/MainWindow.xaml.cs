using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace KsWare.ProjectGenerator {

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		private Dictionary<string, string> _guids = new Dictionary<string, string>();
		private Dictionary<string, string> _replacementsDictionary = new Dictionary<string, string>();
		private DirectoryInfo _destinationDirectory;
		private bool _createRepository;
		private string _branchName;
		private bool _commit = true;
		private string _templatesRootPath;

		// SolutionGuid = {00000001-0000-FFFF-BCDE-0123456789A}
		// Project1 {00000002-0000-FFFF-BCDE-0123456789A}
		// Project2 {00000003-0000-FFFF-BCDE-0123456789A}
		// Solutionitems {00000004-0000-FFFF-BCDE-0123456789AB}

		public MainWindow() {
			InitializeComponent();
			_templatesRootPath = @"D:\Develop\Extern\GitHub.KsWare"; //TODO configure templatesRootPath
			TemplateRootTextBox.Text = _templatesRootPath;
			FindTemplates();
		}

		private void FindTemplates() {
			var root=new DirectoryInfo(_templatesRootPath);
			var templates = new List<string>();
			foreach (var directory in root.GetDirectories()) {
				var template=new FileInfo(Path.Combine(directory.FullName,".template"));
				if(!template.Exists) continue;
				templates.Add(directory.Name);
			}

			TemplateComboBox.ItemsSource = templates;
		}

		private void CreateButton_Click(object sender, RoutedEventArgs e) {
			var templatePath = Path.Combine(TemplateRootTextBox.Text, TemplateComboBox.SelectedItem.ToString());
			var destinationRootPath = DestinationTextBox.Text;
			var projectName = NameTextBox.Text;
			_destinationDirectory = new DirectoryInfo(Path.Combine(destinationRootPath, projectName));

			_createRepository = CreateRepositoryCheckBox.IsChecked == true;
			_branchName = BranchTextBox.Text;
			CreateReplacements(projectName);
			if (_destinationDirectory.Exists) {
				MessageBox.Show("Directory already exists!", "Information", MessageBoxButton.OK, MessageBoxImage.Hand);
				return;
			}

			_destinationDirectory.Create();

			if (_createRepository) {
				if (0 != Git("init")) return;
				if (0 != Git($"checkout -b {_branchName}")) return;
			}

			var co = new CopyOptions {
				ReplaceGuids = true,
				Mode=GenerationMode.Create
			};
			Debug.WriteLine($"> Processing template...");
			Copy(new DirectoryInfo(templatePath), _destinationDirectory, co, new CopyStats());

			if (_createRepository && _commit) {
				if (0 != Git("add *")) return;
				if (0 != Git("commit -m \"create from template\"")) return;
			}

			if (0 != Nuget($"restore src\\{_replacementsDictionary[""]}.sln")) { }

			;

			Process.Start(_destinationDirectory.FullName);
			Process.Start(Path.Combine(_destinationDirectory.FullName, "src", $"{SafeSolutionName}.sln"));
		}

		public string SafeProjectName { get; set; }

		public string SafeSolutionName { get; set; }

		public string RepositoryName { get; set; }

		private void CopyButton_Click(object sender, RoutedEventArgs e) {
			var templatePath = Path.Combine(TemplateRootTextBox.Text, TemplateComboBox.SelectedItem.ToString());
			var destinationRootPath = DestinationTextBox.Text;
			var projectName = NameTextBox.Text;
			_destinationDirectory = new DirectoryInfo(Path.Combine(destinationRootPath, projectName));

			CreateReplacements(projectName);
			_createRepository = false;
			_replacementsDictionary = new Dictionary<string, string>();
			_guids=new Dictionary<string, string>();

			if (_destinationDirectory.Exists) {
				MessageBox.Show("Directory already exists!", "Information", MessageBoxButton.OK, MessageBoxImage.Hand);
				return;
			}

			_destinationDirectory.Create();
			var co = new CopyOptions {
				ReplaceGuids = false,
				Mode = GenerationMode.Copy
			};
			Debug.WriteLine($"> Processing template...");
			Copy(new DirectoryInfo(templatePath), _destinationDirectory, co,new CopyStats());

			Process.Start(_destinationDirectory.FullName);
			Process.Start(Path.Combine(_destinationDirectory.FullName, "src", $"{SafeSolutionName}.sln"));
		}

		private void CreateReplacements(string destination) {
			RepositoryName = destination;
			SafeSolutionName = destination;
			SafeProjectName = destination;
			_replacementsDictionary = new Dictionary<string, string>();
			_replacementsDictionary.Add(Variables.SafeProjectName, destination);
			_replacementsDictionary.Add(Variables.SafeSolutionName, destination);
			_replacementsDictionary.Add(Variables.Company, CompanyTextBox.Text); 
			_replacementsDictionary.Add(Variables.Year, DateTime.Today.Year.ToString());

			for (int i = 0; i < 20; i++) { _guids.Add($"{{{i:D8}-0000-FFFF-BCDE-0123456789A}}", Guid.NewGuid().ToString("B").ToUpper()); }

		}

		private int Git(string arguments) => Cmd($"git {arguments}");

		private int Nuget(string arguments) => Cmd($"nuget {arguments}");

		private int Cmd(string cmdline) {
			Debug.WriteLine($"> {cmdline}");
			var cmd = cmdline.Split(new[] {' '}, 2);
			switch (cmd[0]) {
				case "git":
					// "C:\Users\%user%\AppData\Local\GitHubDesktop\app-2.1.3\resources\app\git\mingw64\bin\git.exe"
					// "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe"
					// "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\mingw32\bin\git.exe"
					// "C:\Program Files\Git\cmd\git.exe"
					// "C:\Program Files\Git\bin\git.exe"
					cmd[0] = @"C:\Program Files\Git\bin\git.exe"; //TODO config git.exe
					break;
				case "nuget":
					cmd[0] = @"C:\Tools\NuGet\nuget.exe"; //TODO config nuget.exe
					break;
			}

			var psi = new ProcessStartInfo(cmd[0], cmd[1]) {
				WorkingDirectory = _destinationDirectory.FullName,
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
			if (sourceDirectory.FullName.EndsWith("src\\packages", StringComparison.OrdinalIgnoreCase)) return;
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

		private void RenameButton_Click(object sender, RoutedEventArgs e) {
			throw new NotImplementedException();
		}

		private void ConvertButton_Click(object sender, RoutedEventArgs e) {
			var dlg=new SelectFolderDialog() {
				Title = "Select folder to convert...",
			};

			if(dlg.ShowDialog()!=true) return;

			new TemplateConverter(dlg.FolderName).Start();
		}
	}

}
