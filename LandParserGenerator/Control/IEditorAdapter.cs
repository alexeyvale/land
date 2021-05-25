using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using System.Runtime.Serialization;

using Land.Core;
using Land.Core.Parsing.Preprocessing;

namespace Land.Control
{
	public class DocumentSegment
	{
		public string FileName { get; set; }
		public int StartOffset { get; set; }
		public int EndOffset { get; set; }
		public bool CaptureWholeLine { get; set; }
	}

	public interface IEditorAdapter
	{
		#region Active document

		/// <summary>
		/// Ести ли активный документ
		/// </summary>
		bool HasActiveDocument();

		/// <summary>
		/// Получение имени активного документа
		/// </summary>
		string GetActiveDocumentName();

		/// <summary>
		/// Получение текста активного документа
		/// </summary>
		string GetActiveDocumentText();

		/// <summary>
		/// Получение позиции курсора в активном документе
		/// </summary>
		int? GetActiveDocumentOffset();

		/// <summary>
		/// Получение позиции курсора в активном документе
		/// </summary>
		SegmentLocation GetActiveDocumentSelection(bool adjustByLine);

		#endregion


		#region Document by name

		/// <summary>
		/// Получение признака конца строки в документе
		/// </summary>
		string GetDocumentLineEnd(string documentName);

		/// <summary>
		/// Получение текста документа
		/// </summary>
		string GetDocumentText(string documentName);

		/// <summary>
		/// Изменение текста документа
		/// </summary>
		void SetDocumentText(string documentName, string text);

		/// <summary>
		/// Задание активного документа и установка курсора
		/// </summary>
		void SetActiveDocumentAndOffset(string documentName, PointLocation location);

		/// <summary>
		/// Вставка текста в указанную позицию в документе
		/// </summary>
		void InsertText(string documentName, string text, PointLocation point);

		#endregion


		#region Text highlighting

		/// <summary>
		/// Выделить участки текста в файле
		/// </summary>
		void SetSegments(List<DocumentSegment> segments, Color color);

		/// <summary>
		/// Сбросить выделение
		/// </summary>
		void ResetSegments();

		#endregion


		#region Messages

		void ProcessMessages(List<Message> messages, bool skipTrace, bool resetPrevious);

		void ProcessMessage(Message message);

		#endregion


		#region Callbacks

		void RegisterOnDocumentChanged(Action<string> callback);

		HashSet<string> GetWorkingSet();

		#endregion
	}
}
