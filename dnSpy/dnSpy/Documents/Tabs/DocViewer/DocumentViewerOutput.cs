﻿/*
    Copyright (C) 2014-2016 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Documents.Tabs.DocViewer;
using dnSpy.Contracts.Text;
using Microsoft.VisualStudio.Text;

namespace dnSpy.Documents.Tabs.DocViewer {
	sealed class DocumentViewerOutput : IDocumentViewerOutput {
		public bool CanBeCached => canBeCached;

		readonly CachedTextColorsCollection cachedTextColorsCollection;
		readonly StringBuilder stringBuilder;
		readonly Dictionary<string, object> customDataDict;
		SpanDataCollectionBuilder<ReferenceInfo> referenceBuilder;
		int indentation;
		bool canBeCached;
		bool addIndent = true;
		bool hasCreatedResult;

		int IDecompilerOutput.Length => stringBuilder.Length;
		public int NextPosition => stringBuilder.Length + GetIndentSize();

		public string GetCachedText() => cachedText ?? (cachedText = stringBuilder.ToString());
		string cachedText;

		public static DocumentViewerOutput Create() => new DocumentViewerOutput();

		DocumentViewerOutput() {
			this.cachedTextColorsCollection = new CachedTextColorsCollection();
			this.stringBuilder = new StringBuilder();
			this.referenceBuilder = SpanDataCollectionBuilder<ReferenceInfo>.CreateBuilder();
			this.canBeCached = true;
			this.customDataDict = new Dictionary<string, object>(StringComparer.Ordinal);
		}

		internal Dictionary<string, object> GetCustomDataDictionary() => customDataDict;

		public DocumentViewerContent CreateContent(Dictionary<string, object> dataDict) {
			if (hasCreatedResult)
				throw new InvalidOperationException(nameof(CreateContent) + " can only be called once");
			hasCreatedResult = true;
			Debug.Assert(GetCachedText() == stringBuilder.ToString());
			return new DocumentViewerContent(GetCachedText(), cachedTextColorsCollection, referenceBuilder.Create(), dataDict);
		}

		void IDocumentViewerOutput.DisableCaching() => canBeCached = false;

		bool IDecompilerOutput.UsesCustomData => true;

		public void AddCustomData<TData>(string id, TData data) {
			object listObj;
			List<TData> list;
			if (customDataDict.TryGetValue(id, out listObj))
				list = (List<TData>)listObj;
			else
				customDataDict.Add(id, list = new List<TData>());
			list.Add(data);
		}

		public void IncreaseIndent() => indentation++;

		public void DecreaseIndent() {
			Debug.Assert(indentation > 0);
			if (indentation > 0)
				indentation--;
		}

		public void WriteLine() {
			addIndent = true;
			cachedTextColorsCollection.Append(BoxedTextColor.Text, Environment.NewLine);
			stringBuilder.Append(Environment.NewLine);
			Debug.Assert(stringBuilder.Length == cachedTextColorsCollection.TextLength);
		}

		int GetIndentSize() => addIndent ? indentation : 0;// Tabs are used

		void AddIndent() {
			if (!addIndent)
				return;
			addIndent = false;
			int count = indentation;
			while (count > 0) {
				switch (count) {
				case 1:
					AddText("\t", BoxedTextColor.Text);
					return;
				case 2:
					AddText("\t\t", BoxedTextColor.Text);
					return;
				case 3:
					AddText("\t\t\t", BoxedTextColor.Text);
					return;
				case 4:
					AddText("\t\t\t\t", BoxedTextColor.Text);
					return;
				case 5:
					AddText("\t\t\t\t\t", BoxedTextColor.Text);
					return;
				case 6:
					AddText("\t\t\t\t\t\t", BoxedTextColor.Text);
					return;
				default:
					AddText("\t\t\t\t\t\t\t", BoxedTextColor.Text);
					count -= 7;
					break;
				}
			}
		}

		void AddText(string text, object color) {
			if (addIndent)
				AddIndent();
			stringBuilder.Append(text);
			cachedTextColorsCollection.Append(color, text);
		}

		void AddText(string text, int index, int count, object color) {
			if (addIndent)
				AddIndent();
			stringBuilder.Append(text, index, count);
			cachedTextColorsCollection.Append(color, text, index, count);
		}

		public void Write(string text, object color) => AddText(text, color);
		public void Write(string text, int index, int count, object color) => AddText(text, index, count, color);

		public void Write(string text, object reference, DecompilerReferenceFlags flags, object color) {
			if (addIndent)
				AddIndent();
			if (reference == null) {
				AddText(text, color);
				return;
			}
			Debug.Assert(!(reference.GetType().FullName ?? string.Empty).Contains("ICSharpCode"), "Internal decompiler data shouldn't be passed to Write()-ref");
			referenceBuilder.Add(new Span(stringBuilder.Length, text.Length), new ReferenceInfo(reference, flags));
			AddText(text, color);
		}

		public void AddUIElement(Func<UIElement> createElement) {
			if (createElement == null)
				throw new ArgumentNullException(nameof(createElement));
			if (addIndent)
				AddIndent();
			canBeCached = false;
			AddCustomData(DocumentViewerUIElementConstants.CustomDataId, new DocumentViewerUIElement(NextPosition, createElement));
		}

		public void AddButton(string buttonText, Action clickHandler) {
			if (buttonText == null)
				throw new ArgumentNullException(nameof(buttonText));
			if (clickHandler == null)
				throw new ArgumentNullException(nameof(clickHandler));
			AddUIElement(() => {
				var button = new Button { Content = buttonText };
				button.SetResourceReference(FrameworkElement.StyleProperty, "TextEditorButton");
				button.Click += (s, e) => {
					e.Handled = true;
					clickHandler();
				};
				return button;
			});
		}
	}
}
