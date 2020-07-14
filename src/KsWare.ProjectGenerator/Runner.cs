using System;
using System.Diagnostics;
using System.Text;
using System.Windows;

namespace KsWare.ProjectGenerator {

	public static class Runner {

		public static void Try(Action action)
		{
			if (Debugger.IsAttached)
			{
				action();
				return;
			}
			try
			{
				action();
			}
			catch (Exception ex)
			{
				var m = new StringBuilder();
				var ex1 = ex;
				var level = 0;
				while (ex1 != null)
				{
					if (level > 0) m.Append("--> ");
					m.AppendLine(ex1.Message);
					level++;
					ex1 = ex1.InnerException;
				}

				MessageBox.Show("Error", $"Error while converting folder.\n\nInternal Message:\n{m}");
			}
		}
	}

}