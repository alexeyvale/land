using System;
using System.Collections.Generic;

using Land.Core.Markup;
using Land.Core;

namespace Land.VSExtension
{
	public class EditorAdapter : IEditorAdapter
	{
		public string GetActiveDocumentName()
		{
			throw new NotImplementedException();
		}

		public int? GetActiveDocumentOffset()
		{
			throw new NotImplementedException();
		}

		public string GetActiveDocumentText()
		{
			throw new NotImplementedException();
		}

		public string GetDocumentText(string documentName)
		{
			throw new NotImplementedException();
		}

		public bool HasActiveDocument()
		{
			throw new NotImplementedException();
		}

		public void ProcessMessages(List<Message> messages)
		{
			throw new NotImplementedException();
		}

		public void ProcessMessage(Message message)
		{
			throw new NotImplementedException();
		}

		public void ResetSegments()
		{
			throw new NotImplementedException();
		}

		public void SetActiveDocumentAndOffset(string documentName, int offset)
		{
			throw new NotImplementedException();
		}

		public void SetSegments(List<DocumentSegment> segments)
		{
			throw new NotImplementedException();
		}
	}
}
