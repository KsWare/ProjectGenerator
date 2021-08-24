using NUnit.Framework;

namespace KsWare.ProjectGenerator.Tests {
	
	[TestFixture]
	public class FileToolsTests {

		[TestCase(@"C:\Develop\Solution\Project\bin\output.exe","bin", true)]
		[TestCase(@"C:\Develop\Solution\Project\taubin\output.exe","bin", false)]
		[TestCase(@"C:\Develop\Solution\Project\binto\output.exe","bin", false)]

		[TestCase(@"C:\Develop\Solution\Project\binto\output.exe","output.exe", true)]

		[TestCase(@"C:\Develop\Solution\Project\binto\output.exe","*.exe", true)]
		public void IsPathExcluded(string path, string pattern, bool result) {
			Assert.That(FileTools.IsPathExcluded(path,new []{pattern}), Is.EqualTo(result));
		}
	}

}