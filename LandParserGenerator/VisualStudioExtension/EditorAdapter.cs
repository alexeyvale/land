using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;

using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Shell.Interop;

using Land.Core;
using Land.Control;

namespace Land.VisualStudioExtension
{
	public class EditorAdapter : IEditorAdapter
	{
		private const string LINE_END_SYMBOLS = "\u000A\u000D\u0085\u2028\u2029";
		private const string DEFAULT_LINE_END = "\r\n";

		private DTE2 DteService => ServiceEventAggregator.Instance.DteService;

		public delegate void SetSegmentstHandler(List<DocumentSegment> e);
		public static event SetSegmentstHandler OnSetSegments;

		public EditorAdapter()
		{
			ServiceEventAggregator.Instance.RegisterOnSolutionOpened(
				(string path) => WorkingDirectory = path
			);
		}

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

		public SegmentLocation GetActiveDocumentSelection(bool adjustByLine)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (DteService?.ActiveDocument?.Object("TextDocument") is TextDocument doc)
			{
				var lineEndLength = GetDocumentLineEnd(doc).Length;
				var isOneLineDocument = doc.StartPoint.Line == doc.EndPoint.Line;
				var startPoint = doc.Selection.TopPoint.CreateEditPoint();
				var endPoint = doc.Selection.BottomPoint.CreateEditPoint();

				if (adjustByLine)
				{
					startPoint.StartOfLine();
					endPoint.EndOfLine();
				}

				return new SegmentLocation
				{
					Start = new PointLocation(
							startPoint.Line,
							startPoint.LineCharOffset,
							/// Махинации, связанные с особенностями учёта конца строки
							startPoint.AbsoluteCharOffset +
								(isOneLineDocument ? 0 : startPoint.Line * (lineEndLength - 1) - lineEndLength)
						),
					End = new PointLocation(
							endPoint.Line,
							endPoint.LineCharOffset,
							endPoint.AbsoluteCharOffset +
								(isOneLineDocument ? 0 : endPoint.Line * (lineEndLength - 1) - lineEndLength)
						),
				};
			}

			return null;
		}

		public string GetActiveDocumentText()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			return DteService?.ActiveDocument?.Object("TextDocument") is TextDocument textDocument
				? textDocument.CreateEditPoint(textDocument.StartPoint)
					.GetText(textDocument.EndPoint)
				: null;
		}

		public string GetDocumentLineEnd(string documentName)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			var doc = GetTextDocument(documentName);

			return doc != null
				? GetDocumentLineEnd(doc)
				: null;
		}

		public string GetDocumentText(string documentName)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			var doc = GetTextDocument(documentName);

			if (doc != null)
			{
				return doc.CreateEditPoint(doc.StartPoint).GetText(doc.EndPoint);
			}

			return null;
		}

		public void SetDocumentText(string documentName, string text)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			var doc = GetTextDocument(documentName);

			if (doc != null)
			{
				doc.CreateEditPoint(doc.StartPoint).ReplaceText(doc.EndPoint, text, 8);
			}
		}

		public void InsertText(string documentName, string text, PointLocation point)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			var doc = GetTextDocument(documentName);

			if (doc != null)
			{
				var lineEndLength = GetDocumentLineEnd(doc).Length;
				var isOneLineDocument = doc.StartPoint.Line == doc.EndPoint.Line;
				var editPoint = doc.CreateEditPoint();

				editPoint.MoveToAbsoluteOffset(GetVSOffset(point, isOneLineDocument ? 0 : lineEndLength));
				editPoint.Insert(text);
			}
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
			OnSetSegments(new List<DocumentSegment>());
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

			if (location != null)
			{
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
					location.Line.Value - 1, location.Column.Value,
					location.Line.Value - 1, location.Column.Value);
			}
		}

		public void SetSegments(List<DocumentSegment> segments, Color color)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			foreach (var group in segments.GroupBy(s => s.FileName))
			{
				for (int i = 1; i <= DteService.Documents.Count; ++i)
				{
					var doc = GetTextDocument(group.Key);

					if (doc != null)
						OnSetSegments(group.ToList());
				}
			}
		}

		#region Settings

		public string WorkingDirectory { get; set; }

		#endregion

		public void RegisterOnDocumentChanged(Action<string> callback)
		{
			ServiceEventAggregator.Instance.RegisterOnDocumentChanged(callback);
		}

		public HashSet<string> GetWorkingSet()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			var allProjects = GetProjects(DteService.Solution)
				.Where(p =>
				{
					ThreadHelper.ThrowIfNotOnUIThread();
					return !string.IsNullOrEmpty(p.FileName);
				})
				.ToList();
			var allFiles = new HashSet<string>();

			foreach(var project in allProjects)
			{
				foreach(ProjectItem item in project.ProjectItems)
				{
					allFiles.UnionWith(GetFiles(item));
				}
			}

			return allFiles;
		}

		#region Methods

		private TextDocument GetTextDocument(string documentName)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			/// Среди открытых документов ищем указанный
			for (int i = 1; i <= DteService.Documents.Count; ++i)
				if (DteService.Documents.Item(i).FullName.Equals(documentName, StringComparison.CurrentCultureIgnoreCase))
				{
					if (DteService.Documents.Item(i).Object() is TextDocument textDoc)
						return textDoc;

					break;
				}

			return null;
		}

		private IEnumerable<Project> GetProjects(Solution sln)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			return sln.Projects
				.Cast<Project>()
				.SelectMany(GetProjects);
		}

		private IEnumerable<Project> GetProjects(Project project)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			/// Если имеем дело с каталогом, посещаем его и получаем вложенные проекты
			if (project.Kind == ProjectKinds.vsProjectKindSolutionFolder)
			{
				return project.ProjectItems
					.Cast<ProjectItem>()
					.Select(x =>
					{
						ThreadHelper.ThrowIfNotOnUIThread();
						return x.SubProject;
					})
					.Where(x => x != null)
					.SelectMany(GetProjects);
			}

			return new[] { project };
		}

		private IEnumerable<string> GetFiles(ProjectItem item)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			switch(item.Kind)
			{
				case EnvDTE.Constants.vsProjectItemKindPhysicalFile:
					return new[] { item.FileNames[0] };
				case EnvDTE.Constants.vsProjectItemKindPhysicalFolder:
				case EnvDTE.Constants.vsProjectItemKindSubProject:
				case EnvDTE.Constants.vsProjectItemKindVirtualFolder:
					var files = new HashSet<string>();
					foreach(ProjectItem pi in item.ProjectItems)
					{
						files.UnionWith(GetFiles(pi));
					}
					return files;
				default:
					return Array.Empty<string>();
			}
		}

		private int GetVSOffset(PointLocation loc, int lineEndLength) =>
			loc.Offset - loc.Line.Value * (lineEndLength - 1) + lineEndLength;

		private string GetDocumentLineEnd(TextDocument doc)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			return doc.StartPoint.Line == doc.EndPoint.Line ? DEFAULT_LINE_END
				: String.Join("", doc.CreateEditPoint(doc.StartPoint)
					.GetLines(1, 3)
					.SkipWhile(c => !LINE_END_SYMBOLS.Contains(c))
					.TakeWhile(c => LINE_END_SYMBOLS.Contains(c)));
		}

		#endregion
	}
}
