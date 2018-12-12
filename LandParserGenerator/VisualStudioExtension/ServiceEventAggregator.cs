using System;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;

using Land.VisualStudioExtension.Listeners;

namespace Land.VisualStudioExtension
{
	internal class ServiceEventAggregator
	{
		public static ServiceEventAggregator Instance { get; private set; }

		public IVsSolution SolutionService { get; private set; }
		public IVsShell ShellService { get; private set; }
		/// Development Tools Environment object для взаимодействия со средой
		public DTE2 DteService { get; private set; }

		private uint SolutionEventsCookie { get; set; }
		private uint ShellPropertyEventsCookie { get; set; }

		private Func<DTE2> DteServiceCallback { get; set; }

		public static void InitializeInstance(
			IVsSolution solutionService, 
			IVsShell shellService, 
			Func<DTE2> dteServiceCallback)
		{
			Instance = new ServiceEventAggregator();

			ThreadHelper.ThrowIfNotOnUIThread();

			Instance.SolutionService = solutionService;
			Instance.SolutionService.AdviseSolutionEvents(
				new SolutionEventsListener(),
				out uint solutionEventsListenerPtr
			);
			Instance.SolutionEventsCookie = solutionEventsListenerPtr;

			Instance.ShellService = shellService;
			Instance.DteService = dteServiceCallback.Invoke();

			if (Instance.DteService == null)
			{
				Instance.DteServiceCallback = dteServiceCallback;

				Instance.ShellService.AdviseShellPropertyChanges(
					new ShellPropertyEventsListener(),
					out uint shellPropertyEventsListenerPtr
				);
				Instance.ShellPropertyEventsCookie = shellPropertyEventsListenerPtr;
			}
		}

		public static void DisposeInstance()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if(Instance?.SolutionService != null
				&& Instance.SolutionEventsCookie != 0)
			{
				Instance.SolutionService
					.UnadviseSolutionEvents(Instance.SolutionEventsCookie);
			}
		}

		private ServiceEventAggregator() { }

		#region DocumentChanged

		private Action<string> DocumentChangedCallback { get; set; }

		public void RegisterOnDocumentChanged(Action<string> callback)
		{
			DocumentChangedCallback = callback;
		}

		public void OnDocumentChanged(string documentName)
		{
			DocumentChangedCallback?.Invoke(documentName);
		}

		#endregion

		#region SolutionOpened

		private Action<string> SolutionOpenedCallback { get; set; }

		public void RegisterOnSolutionOpened(Action<string> callback)
		{
			SolutionOpenedCallback = callback;
		}

		public void OnSolutionOpened()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			this.SolutionService.GetSolutionInfo(out string solutionDirectory, out string solutionFile, out string optsFile);

			if(!String.IsNullOrEmpty(solutionDirectory))
				SolutionOpenedCallback?.Invoke(solutionDirectory);
		}

		#endregion

		#region ShellLoaded

		private Action ShellLoadedCallback { get; set; }

		public void RegisterOnShellLoaded(Action callback)
		{
			ShellLoadedCallback = callback;
		}

		public void OnShellLoaded()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			/// В случае, если среда проинициализирована, 
			/// отвязываемся от события и вызываем инициализацию сервиса DTE
			var hr = ShellService.UnadviseShellPropertyChanges(this.ShellPropertyEventsCookie);
			ErrorHandler.ThrowOnFailure(hr);
			this.ShellPropertyEventsCookie = 0;

			Instance.DteService = DteServiceCallback.Invoke();
		}

		#endregion
	}
}
