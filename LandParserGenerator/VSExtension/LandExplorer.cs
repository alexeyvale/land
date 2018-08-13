//------------------------------------------------------------------------------
// <copyright file="LandExplorer.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.Collections.Generic;
using Land.Control;

namespace Land.VSExtension
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
	[Guid("792a106f-05b1-45db-8898-bbb91d346577")]
	public class LandExplorer : ToolWindowPane
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LandExplorer"/> class.
		/// </summary>
		public LandExplorer() : base(null)
		{
			this.Caption = "LandExplorer";

			var control = new LandExplorerControl();
			control.Initialize(new EditorAdapter(), new Dictionary<string, string>());

			this.Content = control;
		}
	}
}
