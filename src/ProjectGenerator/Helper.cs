using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace KsWare.ProjectGenerator {

	public static class Helper {

		public static string[] GetProjectGuids(string solutionFile) {
			// *.sln
			// Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "KsWare.ProjectGenerator", "KsWare.ProjectGenerator\KsWare.ProjectGenerator.csproj", "{EE5C7F94-4BA5-4EEB-9A83-9146C04198C3}"
			// Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Solution Items", "Solution Items", "{95CE2092-0848-424F-9F49-B491677E8574}"
			string guid = @"\{[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}\}";
			string pattern = $@"^\s*Project(.*?""(?<guid>{guid})""$";

			if (solutionFile.EndsWith(".sln")) {
				using (var r = File.OpenText(solutionFile))
					solutionFile = r.ReadToEnd();
			}

			return Regex.Matches(solutionFile, pattern).Cast<Match>().Select(m => m.Groups["guid"].Value).ToArray();
			//TODO Error handling
		}


		public static string GetSolutionGuid(string solutionFile) {
			// *.sln
			// SolutionGuid = {843ACFD2-DC23-4A2C-A0C5-9EB11AF63263}
			string guid = @"\{[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}\}";
			string pattern = $@"^\s*SolutionGuid\s*=\s*(?<guid>{guid})$";

			if (solutionFile.EndsWith(".sln")) {
				using (var r = File.OpenText(solutionFile))
					solutionFile = r.ReadToEnd();
			}

			return Regex.Match(solutionFile, pattern).Groups["guid"].Value;
			//TODO Error handling
		}

	}

}
