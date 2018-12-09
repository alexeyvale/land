using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Linq;

using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Shell.Interop;

using Land.Core;
using Land.Control;

using VisualStudioExtension.Highlighting;

namespace Land.VisualStudioExtension
{
	public class EditorAdapter : IEditorAdapter
	{
		private DTE _dteService;
		private DTE DteService
		{
			get
			{
				ThreadHelper.ThrowIfNotOnUIThread();

				return _dteService ?? (_dteService = (DTE)LandExplorerPackage.DteService);
			}
		}

		public delegate void SetSegmentstHandler(List<DocumentSegment> e);
		public static event SetSegmentstHandler OnSetSegments;

		public string GetActiveDocumentName()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			return DteService?.ActiveDocument?.FullName;
		}

		public int? GetActiveDocumentOffset()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			return DteService?.ActiveDocument?.Object("TextDocument") is TextDocument textDocument
				? textDocument.Selection.BottomPoint.AbsoluteCharOffset
				: (int?)null;
		}

		public string GetActiveDocumentText()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			return DteService?.ActiveDocument?.Object("TextDocument") is TextDocument textDocument
				? textDocument.CreateEditPoint(textDocument.StartPoint)
					.GetText(textDocument.EndPoint)
				: null;
		}

		public string GetDocumentText(string documentName)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			/// Среди открытых документов ищем указанный
			for (int i = 1; i <= DteService.Documents.Count; ++i)
				if (DteService.Documents.Item(i).FullName.Equals(documentName, StringComparison.CurrentCultureIgnoreCase))
				{
					if (DteService.Documents.Item(i).Object() is TextDocument textDoc)
						return textDoc.CreateEditPoint(textDoc.StartPoint).GetText(textDoc.EndPoint);
				}

			return null;
		}

		public bool HasActiveDocument()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			return DteService?.ActiveDocument != null;
		}

		public void ProcessMessages(List<Message> messages, bool skipTrace, bool resetPrevious)
		{
			IEnumerable<Message> toProcess = skipTrace
				? messages.Where(m => m.Type != MessageType.Trace) : messages;

			foreach (var msg in toProcess)
			{
				ProcessMessage(msg);
			}
		}

		public void ProcessMessage(Message message)
		{
			Console.WriteLine(message.ToString());
		}

		public void ResetSegments()
		{
			//foreach (var document in EditorWindow.Documents)
			//{
			//	document.Value.SegmentsColorizer.ResetSegments();
			//}

			ColorManager.Reset();
		}

		public void SetActiveDocumentAndOffset(string documentName, PointLocation location)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			/// Сервис для работы с открытыми документами
			var openDoc = LandExplorerPackage.GetGlobalService(typeof(IVsUIShellOpenDocument)) as IVsUIShellOpenDocument;

			Guid logicalView = VSConstants.LOGVIEWID_Code;

			/// Если не получилось открыть указанный документ, выходим из функции
			if (ErrorHandler.Failed(openDoc.OpenDocumentViaProject(documentName, 
				ref logicalView, out Microsoft.VisualStudio.OLE.Interop.IServiceProvider sp, 
				out IVsUIHierarchy hier, out uint itemid, out IVsWindowFrame frame)) || frame == null)
			{
				return;
			}

			/// В открытом документе устанавливаем курсор в нужную позицию
			frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocData, out object docData);

			var buffer = docData as VsTextBuffer;
			if (buffer == null)
			{
				if (docData is IVsTextBufferProvider bufferProvider)
				{
					ErrorHandler.ThrowOnFailure(bufferProvider.GetTextBuffer(out IVsTextLines lines));
					buffer = lines as VsTextBuffer;
					if (buffer == null)
						return;
				}
			}
			var mgr = LandExplorerPackage.GetGlobalService(typeof(VsTextManagerClass)) as IVsTextManager;

			mgr.NavigateToLineAndColumn(buffer, ref logicalView, 
				location.Line - 1, location.Column - 1, 
				location.Line - 1, location.Column - 1);
		}

		public Color SetSegments(List<DocumentSegment> segments)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			var color = ColorManager.NextColor();

			foreach (var group in segments.GroupBy(s => s.FileName))
			{
				for (int i = 1; i <= DteService.Documents.Count; ++i)
					if (DteService.Documents.Item(i).FullName.Equals(group.Key, StringComparison.CurrentCultureIgnoreCase))
					{
						if (DteService.Documents.Item(i).Object() is TextDocument textDoc)
						{
							OnSetSegments(group.ToList());
							break;
						}
					}
			}

			color.A = (byte)255;
			return color;
		}

		public void SaveSettings(LandExplorerSettings settings)
		{
			return;
		}

		public LandExplorerSettings LoadSettings()
		{
			return null;
		}

		public void RegisterOnDocumentSaved(Action<string> callback)
		{
			return;
		}

		public void RegisterOnDocumentChanged(Action<string> callback)
		{
			return;
		}

		public void RegisterOnWorkingSetChanged(Action<HashSet<string>> callback)
		{
			return;
		}
	}
}
