using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using Task = System.Threading.Tasks.Task;

namespace Land.VisualStudioExtension
{
	/// <summary>
	///  Наследуем пакет от AsyncPackage, так как загрузка 
	///  при большом количестве парсеров может быть медленной
	/// </summary>
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[ProvideToolWindow(typeof(LandExplorer))]
	[Guid(LandExplorerPackage.PackageGuidString)]
	[SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
	public sealed class LandExplorerPackage : AsyncPackage, IVsShellPropertyEvents
	{
		public const string PackageGuidString = "92dd57dc-aa42-446e-b5bc-9cd875a9e9ec";

		/// <summary>
		/// Development Tools Environment object для взаимодействия со средой
		/// </summary>
		public static DTE2 DteService { get; private set; }

		public uint ShellPropertyEventsCookie { get; set; }

		public LandExplorerPackage()
		{
			// Inside this method you can place any initialization code that does not require
			// any Visual Studio service because at this point the package object is created but
			// not sited yet inside Visual Studio environment. The place to do all the other
			// initialization is the Initialize method.
		}

		#region Package Members

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

			InitializeDTE();
		}

		#endregion

		#region IVsShellPropertyEvents

		int IVsShellPropertyEvents.OnShellPropertyChange(int propid, object var)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (propid == (int)__VSSPROPID.VSSPROPID_Zombie)
			{
				var isZombie = (bool)var;
				if (!isZombie)
				{
					var shellService = this.GetService(typeof(SVsShell)) as IVsShell;
					Assumes.Present(shellService);

					/// В случае, если среда проинициализирована, 
					/// отвязываемся от события и вызываем инициализацию сервиса DTE
					var hr = shellService.UnadviseShellPropertyChanges(this.ShellPropertyEventsCookie);
					ErrorHandler.ThrowOnFailure(hr);

					this.ShellPropertyEventsCookie = 0;
					this.InitializeDTE();
				}
			}
			return VSConstants.S_OK;
		}

		#endregion

		#region Methods

		private void InitializeDTE()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			DteService = this.GetService(typeof(SDTE)) as DTE2;

			/// Если оболочка ещё не проинициализирована полностью
			if (DteService == null)
			{
				var shellService = this.GetService(typeof(SVsShell)) as IVsShell;

				/// Убеждаемся, что сервис получен, иначе - исключение
				Assumes.Present(shellService);

				/// Устанавливаем обработчик для события полной инициализации среды
				var hr = shellService.AdviseShellPropertyChanges(this, out uint cookie);
				this.ShellPropertyEventsCookie = cookie;

				/// Если вернулся не 0, а код ошибки, выбрасываем исключение
				ErrorHandler.ThrowOnFailure(hr);
			}
		}

		#endregion
	}
}
