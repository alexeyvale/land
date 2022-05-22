using Land.Control;
using Land.VisualStudioExtension.Listeners;

namespace Land.VisualStudioExtension
{
	using System;
	using System.Runtime.InteropServices;
	using Microsoft.VisualStudio.Shell;

	/// <summary>
	/// This class implements the tool window exposed by this package and hosts a user control.
	/// </summary>
	/// <remarks>
	/// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
	/// usually implemented by the package implementer.
	/// <para>
	/// This class derives from the ToolWindowPane class provided from the MPF in order to use its
	/// implementation of the IVsUIElementPane interface.
	/// </para>
	/// </remarks>
	[Guid("531dabfc-7ad8-4373-8ff7-b73b0d9d748d")]
	public class LandExplorer : ToolWindowPane
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LandExplorer"/> class.
		/// </summary>
		public LandExplorer() : base(null)
		{
			this.Caption = "LandExplorer";

			var control = new LandExplorerControl();
			var adapter = new EditorAdapter();

			control.Initialize(adapter);

			if (!control.AreSettingsSet())
			{
				var path = System.IO.Path.GetDirectoryName(
					System.Reflection.Assembly.GetExecutingAssembly().Location
				);

				control.ImportSettings($"{path}/Resources/DefaultSettings.xml");
			}

			this.Content = control;
		}
	}
}
