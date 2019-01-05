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

namespace Land.VisualStudioExtension.Listeners
{
	[Microsoft.VisualStudio.Utilities.ContentType("code")]
	[Export(typeof(IWpfTextViewCreationListener))]
	[TextViewRole(PredefinedTextViewRoles.Editable)]
	internal sealed class TextViewCreationListener : IWpfTextViewCreationListener
	{
		[Import]
		public ITextDocumentFactoryService TextDocumentFactoryService { get; set; }

		[ImportingConstructor]
		public TextViewCreationListener(ITextDocumentFactoryService textDocumentFactoryService)
		{
			TextDocumentFactoryService = textDocumentFactoryService;
		}

		///  С каждым ITextView, созданным для некоторого текста-кода,
		///  связываем обработчик события изменения текста
		public void TextViewCreated(IWpfTextView textView)
		{
			textView.TextBuffer.Changed += OnTextChanged;
		}

		public void OnTextChanged(object sender, TextContentChangedEventArgs e)
		{
			if(TextDocumentFactoryService.TryGetTextDocument(sender as ITextBuffer, 
				out ITextDocument textDocument))
			{
				ServiceEventAggregator.Instance.OnDocumentChanged(textDocument.FilePath);
			}
		}
	}
}
