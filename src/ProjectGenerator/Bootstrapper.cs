using System;
using System.Collections.Generic;
using System.Text;
using KsWare.Presentation.Lite;

namespace KsWare.ProjectGenerator
{
	internal class Bootstrapper : BootstrapperBase {
		/// <inheritdoc />
		protected override void OnStartup() {
			Show(new ShellViewModel());
		}
	}
}
