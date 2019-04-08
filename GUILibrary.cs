// Requires: CGUI
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEngine;

namespace Oxide.Plugins
{
	using static CGUI;
	using CGUIExtensions;

	[Info("GUI Library", "romgerman", 0.1)]
	public class GUILibrary : RustPlugin
	{
		private static RustPlugin Owner = null;

		public GUILibrary()
		{
			Owner = this;
		}

		public class GUIGrid : GUIElementBase
		{
			private GUIElementBase[,] _items;
			private int _rows;
			private int _columns;

			private GUIObject _wrap;

			public GUIGrid(int rows, int columns, string elementName = null)
			{
				elementName = elementName ?? GUIObject.GenerateId();

				_items = new GUIElementBase[rows, columns];
				_rows = rows;
				_columns = columns;

				_wrap = new GUIObject()
				{
					new EmptyPanelComponent(),
					new RectTransformComponent()
				};
			}

			public void SetElement(int row, int column, GUIElementBase element)
			{
				float width = 1f / _rows;
				float height = 1f / _columns;

				element.Parent = _wrap.Name;
				element.Transform.AnchorMax = new Vector2(1.0f - row * width,		  1.0f - column * height);
				element.Transform.AnchorMin = new Vector2(1.0f - row * width - width, 1.0f - column * height - height);

				_items[row, column] = element;
			}

			public override ICollection<GUIObject> Render()
			{
				var list = new List<GUIObject>() { _wrap };
				
				for (int x = 0; x < _rows; x++)
				{
					for (int y = 0; y < _columns; y++)
					{
						var item = _items[x, y];

						if (item != null)
							list.AddRange(item.Render());
					}
				}

				return list;
			}
		}

		public class GUIList : GUIElementBase
		{
			public int VisibleElementCount { get; set; } = 10;
			public int Index { get; set; }

			public override string Parent
			{
				get { return _wrap.Parent; }
				set { _wrap.Parent = value; }
			}

			public override object Model
			{
				get { return _collection; }
				set { BindCollection(value as IEnumerable); }
			}

			private IEnumerable _collection;
			private List<GUIObject> _list;
			private GUIObject _wrap;
			private GUIObject _listWrap;
			private GUIButton _nextButton;
			private GUIButton _prevButton;
			private GUIElementBase _itemTemplate;

			public GUIList(string elementName = null, GUIElementBase itemTemplate = null)
			{
				elementName = elementName ?? GUIObject.GenerateId();

				_list = new List<GUIObject>();

				_wrap = new GUIObject(elementName + "_wrap")
				{
					new EmptyPanelComponent(),
					new RectTransformComponent()
				};

				_listWrap = new GUIObject(elementName + "_list", _wrap.Name)
				{
					new EmptyPanelComponent(),
					new RectTransformComponent() { OffsetMin = new Vector2(0f, 0.2f) }
				};

				_itemTemplate = itemTemplate ?? new GUIButton("", null, _listWrap.Name);

				_prevButton = new GUIButton("<", elementName + "_prev", _wrap.Name);
				var prevTransform = _prevButton.ButtonElement.GetComponent<RectTransformComponent>();
				//prevTransform.OffsetMax = new Vector2(0f, 0.5f);
				prevTransform.AnchorMax = new Vector2(0.5f, 0.2f);
				prevTransform.AnchorMin = new Vector2(0f, 0f);

				_nextButton = new GUIButton(">", elementName + "_next", _wrap.Name);
				var nextTransform = _nextButton.ButtonElement.GetComponent<RectTransformComponent>();
				//nextTransform.OffsetMax = new Vector2(0f, 0.5f);
				nextTransform.AnchorMin = new Vector2(0.5f, 0f);
				nextTransform.AnchorMax = new Vector2(1f, 0.2f);
			}

			public void BindCollection(IEnumerable collection)
			{
				_collection = collection;
				CollectionChanged();

				var p = collection.GetType().BaseType.GetProperty("PropertyChanged", BindingFlags.Instance | BindingFlags.Public);

				if (p == null)
					return;

				var e = (ModelPropertyChanged)p.GetValue(collection);

				e += (name) =>
				{
					CollectionChanged();
				};
			}

			private void CollectionChanged()
			{
				float height = 1.0f / VisibleElementCount;
				int current = 0;

				_list.Clear();

				foreach(var obj in _collection)
				{
					var item = _itemTemplate.Copy();
					item.Model = obj;
					item.Transform.AnchorMax = new Vector2(1f, 1 - current * height);
					item.Transform.AnchorMin = new Vector2(0f, 1 - current * height - height);
					item.Parent = _listWrap.Name;

					_list.AddRange(item.Render());

					current++;
				}
			}

			public override ICollection<GUIObject> Render()
			{
				var ret = new List<GUIObject>(VisibleElementCount + 6) { _wrap, _listWrap };
				ret.AddRange(_nextButton.Render());
				ret.AddRange(_prevButton.Render());
				ret.AddRange(_list);

				return ret;
			}
		}

		public class GUIWindow : GUIElementBase
		{
			public override string Parent
			{
				get { return _window.Parent; }
				set { _window.Parent = value; }
			}

			private GUIObject _window;
			private GUIObject _titleElement;
			private GUIButton _closeButton;
			private GUIObject _bodyElement;

			private GUIElementBase _body;

			public GUIWindow(string title = "", GUIElementBase body = null, string elementName = null)
			{
				elementName = elementName ?? GUIObject.GenerateId();

				_window = new GUIObject(elementName)
				{
					new ImageComponent() { Color = new Color(0.1f, 0.8f, 0.5f, 0.7f) },
					new CursorComponent(),
					new RectTransformComponent()
				};

				_titleElement = new GUIObject(elementName + "_title", _window.Name)
				{
					new TextComponent() { Text = title },
					new RectTransformComponent() { AnchorMin = new Vector2(0f, 0.9f) }
				};

				_closeButton = new GUIButton("X", elementName + "_close", _window.Name);
				_closeButton.Transform.AnchorMin = new Vector2(0.9f, 0.9f);
				_closeButton.Click += (o, player) =>
				{
					_window.HideUI(player);
				};

				_bodyElement = new GUIObject(elementName + "_body", _window.Name)
				{
					new EmptyPanelComponent(),
					new RectTransformComponent() { AnchorMax = new Vector2(1f, 0.9f) }
				};

				_body = body;

				if (body != null)
				{
					_body.Parent = _bodyElement.Name;
				}
			}

			public override ICollection<GUIObject> Render()
			{
				var items = new List<GUIObject>() { _window, _titleElement };
				items.AddRange(_closeButton.Render());
				items.Add(_bodyElement);

				if (_body != null)
					items.AddRange(_body.Render());

				return items;
			}
		}

		public class GUIProgressBar : GUIElementBase // TODO: finish
		{
			public override string Parent
			{
				get { return BackgroundElement.Parent; }
				set { BackgroundElement.Parent = value; }
			}

			public override object Model
			{
				get { return _model; }
				set { Bind(value); }
			}

			public GUIObject BackgroundElement;
			public GUIObject ForegroundElement;

			private object _model;

			public GUIProgressBar(string elementName = null, string parent = null)
			{
				elementName = elementName ?? GUIObject.GenerateId();

				BackgroundElement = new GUIObject(elementName + "_background", parent)
				{
					new ButtonComponent(),
					new RectTransformComponent()
				};

				ForegroundElement = new GUIObject(elementName + "_foreground", BackgroundElement.Name)
				{
					new ButtonComponent(),
					new RectTransformComponent()
					{
						OffsetMin = new Vector2(0.2f, 0.2f),
						OffsetMax = new Vector2(0.8f, 0.8f)
					}
				};
			}

			public void Bind(object item, string propName = "Progress")
			{
				_model = item;

				if (item is float)
				{
					//ForegroundElement.GetComponent<RectTransformComponent>().Top = (float)item;
				}
				else if (item.GetType().IsSubclassOf(typeof(ModelBase)))
				{
					Binding.Create(item as ModelBase, new BindingCollection
					{
						//[propName] = ForegroundElement.GetComponent<RectTransformComponent>().Bind(x => x.Top)
					});
				}
				else
				{
					//ForegroundElement.GetComponent<RectTransformComponent>().Top = (float)item.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance).GetValue(item);
				}
			}

			public override ICollection<GUIObject> Render()
			{
				return new GUIObject[] { BackgroundElement, ForegroundElement };
			}
		}

		public class GUIButton : GUIElementBase
		{
			public event EventHandler<BasePlayer> Click;

			public override string Parent
			{
				get { return ButtonElement.Parent; }
				set { ButtonElement.Parent = value; }
			}

			public override RectTransformComponent Transform
			{
				get { return ButtonElement.GetComponent<RectTransformComponent>(); }
			}

			public override object Model
			{
				get { return _model; }
				set
				{
					Bind(value);
				}
			}

			private object _model;

			public GUIObject TextElement;
			public GUIObject ButtonElement;

			public GUIButton(string text = "", string elementName = null, string parent = null)
			{
				elementName = elementName ?? GUIObject.GenerateId();

				ButtonElement = new GUIObject(elementName + "_button", parent)
				{
					new ButtonComponent().OnClick(player =>
					{
						Click?.Invoke(this, player);
					}, Owner),
					new RectTransformComponent()
				};

				TextElement = new GUIObject(elementName + "_text", ButtonElement.Name)
				{
					new TextComponent(text) { Align = TextAlign.MiddleCenter, Color = Color.black }
				};
			}

			public void Bind(object item, string propName = "Text")
			{
				_model = item;

				if (item is string)
				{
					TextElement.GetComponent<TextComponent>().Text = item as string;
				}
				else if (item.GetType().IsSubclassOf(typeof(ModelBase)))
				{
					Binding.Create(item as ModelBase, new BindingCollection
					{
						[propName] = TextElement.GetComponent<TextComponent>().Bind(x => x.Text)
					});
				}
				else
				{
					TextElement.GetComponent<TextComponent>().Text = item.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance).GetValue(item) as string;
				}
			}

			public override ICollection<GUIObject> Render()
			{
				return new GUIObject[] { ButtonElement, TextElement };
			}

			public override GUIElementBase Copy()
			{
				return new GUIButton(TextElement.GetComponent<TextComponent>().Text, null, ButtonElement.Parent);
			}

			~GUIButton()
			{
				ButtonElement.GetComponent<ButtonComponent>().RemoveOnClick(Owner);
			}
		}
	}
}
