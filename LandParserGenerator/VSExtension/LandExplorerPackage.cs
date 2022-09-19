using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Task = System.Threading.Tasks.Task;

namespace Land.VisualStudioExtension
{
	/// <summary>
	/// This is the class that implements the package exposed by this assembly.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The minimum requirement for a class to be considered a valid package for Visual Studio
	/// is to implement the IVsPackage interface and register itself with the shell.
	/// This package uses the helper classes defined inside the Managed Package Framework (MPF)
	/// to do it: it derives from the Package class that provides the implementation of the
	/// IVsPackage interface and uses the registration attributes defined in the framework to
	/// register itself and its components with the shell. These attributes tell the pkgdef creation
	/// utility what data to put into .pkgdef file.
	/// </para>
	/// <para>
	/// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
	/// </para>
	/// </remarks>
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[ProvideToolWindow(typeof(LandExplorer))]
	[Guid(LandExplorerPackage.PackageGuidString)]
	public sealed class LandExplorerPackage : AsyncPackage
	{
		/// <summary>
		/// VSExtensionPackage GUID string.
		/// </summary>
		public const string PackageGuidString = "2b298fdf-c3b8-4a8d-beb6-ea0d82bcafb9";

		#region Package Members

		public LandExplorerPackage()
		{
			// Inside this method you can place any initialization code that does not require
			// any Visual Studio service because at this point the package object is created but
			// not sited yet inside Visual Studio environment. The place to do all the other
			// initialization is the Initialize method.
		}

		/// <summary>
		/// Initialization of the package; this method is called right after the package is sited, so this is the place
		/// where you can put all the initialization code that rely on services provided by VisualStudio.
		/// </summary>
		/// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
		/// <param name="progress">A provider for progress updates.</param>
		/// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			// When initialized asynchronously, the current thread may be a background thread at this point.
			// Do any initialization that requires the UI thread after switching to the UI thread.
			await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
			await LandExplorerCommand.InitializeAsync(this);

			ServiceEventAggregator.InitializeInstance(
				await GetServiceAsync(typeof(SVsSolution)) as IVsSolution,
				await GetServiceAsync(typeof(SVsShell)) as IVsShell,
				() => GetService(typeof(SDTE)) as DTE2
			);
		}

		protected override void Dispose(bool disposing)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			base.Dispose(disposing);
			ServiceEventAggregator.DisposeInstance();
		}

		protected override int QueryClose(out bool pfCanClose)
		{
			var toolWindow = (LandExplorer)this.FindToolWindow(typeof(LandExplorer), 0, false);

			if (toolWindow != null
				&& toolWindow.Control.IsLoaded
				&& toolWindow.Control.HasUnsavedChanges)
			{
				switch (MessageBox.Show(
					"Сохранить изменения текущей разметки?",
					"Закрытие панели разметки",
					MessageBoxButton.YesNoCancel,
					MessageBoxImage.Question))
				{
					case MessageBoxResult.Yes:
						toolWindow.Control.Save();
						break;
					case MessageBoxResult.Cancel:
						pfCanClose = false;
						return VSConstants.S_OK;
				}
			}

			pfCanClose = true;
			return VSConstants.S_OK;
		}

		#endregion
	}
}
