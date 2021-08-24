using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace KsWare.ProjectGenerator {

	public static class FileTools {

		private static readonly List<string> PossibleNugetLocations = new List<string> {
			@"%ProgramData%\chocolatey\lib\NuGet.CommandLine\tools\nuget.exe",
			@"C:\Tools\NuGet\nuget.exe",
		};

		private static readonly List<string> PossibleGitLocations = new List<string> {
			@"%ProgramFiles%\Git\bin\git.exe",
		};

		public static string GetNugetPath(bool allowUserFeedback = true) {
			foreach (var location in PossibleNugetLocations) {
				var path = Environment.ExpandEnvironmentVariables(location);
				if (File.Exists(path)) return path;
			}

			if (!allowUserFeedback) return null;
			var dlg = new OpenFileDialog {
				Title = "Locate nuget.exe",
				Filter = "nuget.exe|nuget.exe",
				CheckFileExists = true,
				CheckPathExists = true,
				FileName = "nuget.exe"
			};
			if (dlg.ShowDialog() != true) return null;

			PossibleNugetLocations.Insert(0, dlg.FileName);
			return dlg.FileName;
		}

		public static string GetGitPath(bool allowUserFeedback = true) {
			foreach (var location in PossibleGitLocations) {
				var path = Environment.ExpandEnvironmentVariables(location);
				if (File.Exists(path)) return path;
			}

			if (!allowUserFeedback) return null;
			var dlg = new OpenFileDialog {
				Title = "Locate git.exe",
				Filter = "git.exe|git.exe",
				CheckFileExists = true,
				CheckPathExists = true,
				FileName = "git.exe"
			};
			if (dlg.ShowDialog() != true) return null;

			PossibleGitLocations.Insert(0, dlg.FileName);
			return dlg.FileName;
		}

		public static bool IsPathExcluded(string path, string[] patterns) {
			// excl		=> /excl/
			// *excl	=> /*excl/
			// *excl*	=> /*excl*/
			patterns = CreateExcludePattern(patterns); 
			foreach (var pattern in patterns) {
				if (Regex.IsMatch(path, pattern, RegexOptions.IgnoreCase)) return true;
			}

			return false;
		}

		public static string[] CreateExcludePattern(string[] excludes) {
			var output=new string[excludes.Length];
			for (int i = 0; i < excludes.Length; i++) {
				var s = excludes[i];
				s = s.Replace("*", "~ANY~").Replace("?", "~CHAR~");
				s = Regex.Escape(s);
				s = s.Replace("~ANY~",@"[^\\]*").Replace("~CHAR~", @"[^\\]");
				s = @"(^|\\)" + s + @"(\\|$)";
				output[i] = s;
			}

			return output;
		}
	}

}
