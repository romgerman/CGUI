using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
	namespace CGUIExtensions
	{
		public static class BindingExtensions
		{
			//https://stackoverflow.com/a/32378113
			/// <summary>
			/// Helper for binding
			/// </summary>
			public static CGUI.Binding Bind<T, TProp>(this T o, Expression<Func<T, TProp>> propertySelector, CGUI.BindingConverter converter = null)
			{
				MemberExpression body = propertySelector.Body as MemberExpression;

				if (body == null)
					throw new ArgumentException(string.Format(
						"Expression '{0}' refers to a method, not a property.",
						propertySelector.ToString()));

				return new CGUI.Binding(o, body.Member.Name, converter);
			}
		}
	}

	[Info("CGUI", "romgerman", 0.1)]
	public class CGUI : RustPlugin
	{
		public class StateCollection : Dictionary<string, object> { }

		public delegate StateCollection StateSetter(StateCollection state);

		public class PlayerStateManager
		{
			public class PlayerState : Dictionary<string, StateCollection> { }

			private Dictionary<BasePlayer, PlayerState> _states = new Dictionary<BasePlayer, PlayerState>();
			private Dictionary<string, StateCollection> _defaultStates = new Dictionary<string, StateCollection>();

			public void RegisterDefaultState(GUIElementBase definition, bool full = false)
			{
				var key = GetElementHash(definition);
				var value = new StateCollection();

				var props = definition.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);

				foreach(var prop in props)
				{
					value.Add(prop.Name, prop.GetValue(definition));
				}

				if (full)
				{
					var objs = definition.Render();

					foreach (var obj in objs)
					{
						var name = obj.Name;
					}
				}

				_defaultStates.Add(key, value);
			}

			public void ChangeState(BasePlayer player, GUIElementBase definition, StateSetter setter)
			{
				var state = setter.Invoke(GetState(player, definition));
				if (state == null)
					return;

				_states[player][GetElementHash(definition)] = state;

				foreach(var kvp in state)
				{
					definition.GetType().GetProperty(kvp.Key, BindingFlags.Instance | BindingFlags.Public).SetValue(definition, kvp.Value);
				}
			}

			public void SetState(BasePlayer player, GUIElementBase definition)
			{
				var state = GetState(player, definition);

				if (state == null)
					return;

				foreach (var kvp in state)
				{
					definition.GetType().GetProperty(kvp.Key, BindingFlags.Instance | BindingFlags.Public).SetValue(definition, kvp.Value);
				}
			}

			public StateCollection GetState(BasePlayer player, GUIElementBase definition)
			{
				PlayerState state;
				StateCollection collection;

				if (_states.TryGetValue(player, out state))
				{
					if (state.TryGetValue(GetElementHash(definition), out collection))
					{
						return collection;
					}
					else
					{
						if (_defaultStates.TryGetValue(GetElementHash(definition), out collection))
						{
							return collection;
						}
					}
				}
				else
				{
					if (_defaultStates.TryGetValue(GetElementHash(definition), out collection))
					{
						return collection;
					}
				}

				return null;
				
				/*
				 
					var ui = new CGUI.PlayerState();
				
					var button = ... // definition
					button.OnClick += (o, player) => {
						ui.SetState(player, button, (state) => {
							return new StateCollection() {
								["Text"] = state["IsNew"] ? "Hello new player!" : "Hellow my fellow old friend",
								["IsNew"] = false
							};
						});
					}

					
					ui.SetState(player, button, () => {
						return new StateCollection() {
							["Text"] = "Hello new player!",
							["IsNew"] = true
						};
					});
					ui.Show(player, button);
					
				 
				 */
			}

			private string GetElementHash(GUIElementBase element)
			{
				return new StringBuilder().Append(element.GetType().FullName).Append(element.Render()?.First().Name).Append(element.GetHashCode()).ToString();
			}
		}

		public class ChangeableCollection<T> : ModelBase, IList<T>
		{
			public T this[int index]
			{
				get
				{
					return _list[index];
				}
				set
				{
					_list[index] = value;
					OnPropertyChanged();
				}
			}

			public int Count => _list.Count;

			public bool IsReadOnly => false;

			private List<T> _list = new List<T>();

			public void Add(T item)
			{
				_list.Add(item);
				OnPropertyChanged();
			}

			public void Clear()
			{
				_list.Clear();
				OnPropertyChanged();
			}

			public bool Contains(T item)
			{
				return _list.Contains(item);
			}

			public void CopyTo(T[] array, int arrayIndex)
			{
				throw new NotImplementedException();
			}

			public IEnumerator<T> GetEnumerator()
			{
				return _list.GetEnumerator();
			}

			public int IndexOf(T item)
			{
				return _list.IndexOf(item);
			}

			public void Insert(int index, T item)
			{
				_list.Insert(index, item);
				OnPropertyChanged();
			}

			public bool Remove(T item)
			{
				var ret = _list.Remove(item);
				OnPropertyChanged();
				return ret;
			}

			public void RemoveAt(int index)
			{
				_list.RemoveAt(index);
				OnPropertyChanged();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return _list.GetEnumerator();
			}
		}

		public delegate void ModelPropertyChanged(string name);

		public abstract class ModelBase
		{
			public ModelPropertyChanged PropertyChanged { get; set; }

			public void OnPropertyChanged([CallerMemberName] string name = null)
			{
				PropertyChanged?.Invoke(name);
			}
		}

		public class BindingCollection : Dictionary<string, Binding> { }

		public delegate object BindingConverter(object input);

		public class Binding
		{
			public object Obj { get; set; }
			public string Property { get; set; }
			public BindingConverter Converter { get; set; }

			private Type _type;

			public Binding(object instance, string propertyName, BindingConverter converter = null)
			{
				this.Obj = instance;
				this._type = instance.GetType();
				this.Property = propertyName;
				this.Converter = converter;
			}

			public static void Create(ModelBase model, BindingCollection relations)
			{
				var modelProps = model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

				foreach (var prop in modelProps)
				{
					if (!relations.ContainsKey(prop.Name))
						continue;

					ModelPropertyChanged setValue = (string name) =>
					{
						var binding = relations[name];
						var value = modelProps.First(x => x.Name == name).GetValue(model);

						if (binding.Converter != null)
							value = binding.Converter(value);

						binding._type.GetProperty(binding.Property, BindingFlags.Public | BindingFlags.Instance).SetValue(binding.Obj, value);
					};
					
					setValue(prop.Name);
					model.PropertyChanged += setValue;
				}
			}

			public static void Remove(ModelBase model)
			{
				model.PropertyChanged = null;
			}
		}

		public class Representation : ICollection<GUIElementBase>
		{
			public List<GUIElementBase> Elements { get; } = new List<GUIElementBase>();

			public int Count => Elements.Count;
			public bool IsReadOnly => false;

			public JToken ToJson()
			{
				var json = new JArray();

				foreach(var e in Elements)
				{
					if (e == null)
						continue;

					var objs = e.Render();

					if (objs == null)
						continue;

					foreach(var o in objs)
					{
						if (o != null && !o.Hidden)
							json.Add(o.ToJson());
					}					
				}

				return json;
			}

			public void AddUI(BasePlayer player)
			{
				//CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo(player.net.connection), null, "AddUI", ToJson().ToString(Formatting.None));
				CommunityEntity.ServerInstance.ClientRPCPlayer(null, player, GUIObject.ShowUICommand, ToJson().ToString(Formatting.None));
			}

			public void RemoveUI(BasePlayer player)
			{
				foreach (GUIElementBase element in Elements)
				{
					foreach(var obj in element.Render())
					{
						obj.HideUI(player);
					}
				}
			}

			public void UpdateUI(BasePlayer player)
			{
				RemoveUI(player);
				AddUI(player);
			}

			// Implementation of ICollection interfaces

			public void Add(GUIElementBase item)
			{
				Elements.Add(item);
			}

			public void Clear() => Elements.Clear();
			public bool Contains(GUIElementBase item) => Elements.Contains(item);
			public bool Remove(GUIElementBase item) => Elements.Remove(item);
			public IEnumerator<GUIElementBase> GetEnumerator() => Elements.GetEnumerator();
			IEnumerator IEnumerable.GetEnumerator() => Elements.GetEnumerator();

			// Not used

			public void CopyTo(GUIElementBase[] array, int arrayIndex)
			{
				throw new NotImplementedException();
			}
		}

		public abstract class GUIElementBase
		{
			public virtual string Parent { get; set; }
			public virtual RectTransformComponent Transform { get; }
			public virtual object Model { get; set; }

			public abstract ICollection<GUIObject> Render();

			public virtual GUIElementBase Copy() => null;
		}
		
		public class GUIObject : ICollection<GUIComponentBase>
		{
			public const string ShowUICommand = "AddUI";
			public const string HideUICommand = "DestroyUI";

			public const string DefaultParent = "Hud";

			public string Name { get; }
			public string Parent { get; set; }
			public float FadeOutTime;
			public bool Hidden { get; private set; }

			public List<GUIComponentBase> Components { get; } = new List<GUIComponentBase>(1);

			// ICollection stuff
			public int Count => Components.Count;
			public bool IsReadOnly => false;

			private bool _isDirty = true;
			private JObject _json = new JObject();

			public GUIObject(string name = null, string parent = DefaultParent)
			{
				if (name == null)
					name = GenerateId();

				if (parent == null)
					parent = DefaultParent;

				Name = name;
				Parent = parent;
			}

			public JToken ToJson()
			{
				if (!_isDirty)
					return _json;

				_isDirty = false;

				_json = new JObject
				{
					{ "name", Name },
					{ "parent", Parent }
				};

				if (FadeOutTime != 0)
					_json["fadeOut"] = FadeOutTime;

				var components = new JArray();

				foreach(var c in Components)
				{
					components.Add(c.ToJson());
				}

				_json["components"] = components;

				return _json;
			}

			public void ShowUI(BasePlayer player)
			{
				CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo(player.net.connection), null, ShowUICommand, "[" + ToJson().ToString(Formatting.None) + "]");
			}

			public void HideUI(BasePlayer player)
			{
				CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo(player.net.connection), null, HideUICommand, Name);
			}

			public void UpdateUI(BasePlayer player)
			{
				HideUI(player);
				ShowUI(player);
			}

			public T GetComponent<T>() where T : GUIComponentBase
			{
				return Components.Find(x => x.GetType() == typeof(T)) as T;
			}

			public static string GenerateId()
			{
				return Guid.NewGuid().ToString().Replace("-", "");
			}

			// Implementation of ICollection interfaces

			public void Add(GUIComponentBase item)
			{
				item.StateChanged += (o, e) =>
				{
					if (e.NeedsUpdate && e.From != null)
						UpdateUI(e.From);
				};

				Components.Add(item);
			}

			public void Clear() => Components.Clear();
			public bool Contains(GUIComponentBase item) => Components.Contains(item);
			public bool Remove(GUIComponentBase item) => Components.Remove(item);
			IEnumerator IEnumerable.GetEnumerator() => Components.GetEnumerator();
			IEnumerator<GUIComponentBase> IEnumerable<GUIComponentBase>.GetEnumerator() => Components.GetEnumerator();

			public void CopyTo(GUIComponentBase[] array, int arrayIndex)
			{
				throw new NotImplementedException();
			}
		}

		public class EmptyPanelComponent : ImageComponent
		{
			public EmptyPanelComponent()
			{
				Color = new Color(0, 0, 0, 0);
			}
		}

		public class OutlineComponent : GUIComponentBase
		{
			public Vector2 Distance
			{
				get { return _distance; }
				set { IsDirty = true; _distance = value; }
			}
			public Color Color
			{
				get { return _color; }
				set { IsDirty = true; _color = value; }
			}

			private Vector2 _distance;
			private Color _color = new Color(1.0f, 1.0f, 1.0f, 1.0f);

			public OutlineComponent()
			{
				Type = "UnityEngine.UI.Outline";
			}

			public override JObject ToJson()
			{
				if (!IsDirty)
					return Json;

				IsDirty = false;

				Json = new JObject
				{
					{ "type", Type },
					{ "color", $"{_color.r} {_color.g} {_color.b} {_color.a}" },
					{ "distance", $"{_distance.x} {_distance.y}" }
				};

				return Json;
			}
		}

		public class InputFieldComponent : GUIComponentBase
		{
			public string Text
			{
				get { return _text; }
				set { IsDirty = true; _text = value; }
			}
			public int FontSize
			{
				get { return _fontSize; }
				set { IsDirty = true; _fontSize = value; }
			}
			public TextAlign Align
			{
				get { return _align; }
				set { IsDirty = true; _align = value; }
			}
			public int CharacterLimit
			{
				get { return _limit; }
				set { IsDirty = true; _limit = value; }
			}
			public string Command
			{
				get { return _command; }
				set { IsDirty = true; _command = value; }
			}
			public bool IsPassword
			{
				get { return _password; }
				set { IsDirty = true; _password = value; }
			}
			public Color Color
			{
				get { return _color; }
				set { IsDirty = true; _color = value; }
			}

			private string _text = string.Empty;
			private int _fontSize = 14;
			private TextAlign _align = TextAlign.MiddleLeft;
			private int _limit = 0;
			private string _command;
			private bool _password;
			private Color _color = new Color(1, 1, 1);

			public InputFieldComponent()
			{
				Type = "UnityEngine.UI.InputField";
			}

			private int _inputFieldNum = 0;

			public InputFieldComponent OnTextChange(Action<string> callback, RustPlugin plugin)
			{
				var cmd = (Game.Rust.Libraries.Command)plugin.GetType().GetField("cmd", BindingFlags.Instance).GetValue(plugin);
				cmd.AddConsoleCommand($"gui_input_change_${_inputFieldNum}", plugin, (args) =>
				{
					callback.Invoke(args.GetString(0));
					OnStateChanged(args.Player());
					return true;
				});

				_inputFieldNum++;

				return this;
			}

			public override JObject ToJson()
			{
				if (!IsDirty)
					return Json;

				IsDirty = false;

				Json = new JObject
				{
					{ "type", Type },
					{ "text", _text },
					{ "fontSize", _fontSize },
					{ "align", _align.ToString() },
					{ "characterLimit", _limit },
					{ "color", $"{_color.r} {_color.g} {_color.b} {_color.a}" }
				};

				if (_command != null)
					Json["command"] = _command;

				if (_password)
					Json["password"] = true;

				return Json;
			}
		}

		public enum TextAlign
		{
			UpperLeft,
			UpperCenter,
			UpperRight,
			MiddleLeft,
			MiddleCenter,
			MiddleRight,
			LowerLeft,
			LowerCenter,
			LowerRight
		}

		public class TextComponent : GUIComponentBase
		{
			public string Text
			{
				get { return text; }
				set { IsDirty = true; text = value; }
			}
			public int FontSize
			{
				get { return fontSize; }
				set { IsDirty = true; fontSize = value; }
			}
			public TextAlign Align
			{
				get { return align; }
				set { IsDirty = true; align = value; }
			}
			public Color Color
			{
				get { return color; }
				set { IsDirty = true; color = value; }
			}

			private string text = string.Empty;
			private int fontSize = 14;
			private TextAlign align = TextAlign.MiddleCenter;
			private Color color = new Color(1, 1, 1);

			public TextComponent()
			{
				Type = "UnityEngine.UI.Text";
			}

			public TextComponent(string text, int fontSize = 14) : this()
			{
				Text = text;
				FontSize = fontSize;
			}

			public override JObject ToJson()
			{
				if (!IsDirty)
					return Json;

				IsDirty = false;

				Json = new JObject
				{
					{ "type", Type },
					{ "text", Text },
					{ "fontSize", FontSize },
					{ "align", align.ToString() },
					{ "color", $"{color.r} {color.g} {color.b} {color.a}" }
				};

				if (FadeInTime != 0)
					Json["fadeIn"] = FadeInTime;

				return Json;
			}
		}

		public enum ImageType
		{
			Simple,
			Sliced,
			Tiled,
			Filled
		}

		public class ButtonComponent : GUIComponentBase
		{
			public string CloseElementName
			{
				get { return _closeElement; }
				set { IsDirty = true; _closeElement = value; }
			}
			public ImageType ImageType
			{
				get { return _imageType; }
				set { IsDirty = true; _imageType = value; }
			}
			public Color Color
			{
				get { return _color; }
				set { IsDirty = true; _color = value; }
			}
			public string Command
			{
				get { return _command; }
				set { IsDirty = true; _command = value; }
			}

			private string _closeElement, _command;
			private ImageType _imageType = ImageType.Simple;
			private Color _color = new Color(1, 1, 1);

			public ButtonComponent()
			{
				Type = "UnityEngine.UI.Button";
			}

			private static int _buttonNum = 0;

			public ButtonComponent OnClick(Action<BasePlayer> callback, RustPlugin plugin)
			{
				var cmd = (Game.Rust.Libraries.Command)plugin.GetType().GetField("cmd", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(plugin);

				var commandName = $"gui_button_click_${_buttonNum}";
				Command = commandName;
				cmd.AddConsoleCommand(commandName, plugin, args =>
				{
					callback?.Invoke(args.Player());
					return true;
				});

				_buttonNum++;

				return this;
			}

			public void RemoveOnClick(RustPlugin plugin)
			{
				var cmd = (Game.Rust.Libraries.Command)plugin.GetType().GetField("cmd", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(plugin);
				cmd.RemoveConsoleCommand(Command, plugin);
			}

			public override JObject ToJson()
			{
				if (!IsDirty)
					return Json;

				IsDirty = false;

				Json = new JObject
				{
					{ "type", Type },
					{ "imagetype", _imageType.ToString() },
					{ "color", $"{_color.r} {_color.g} {_color.b} {_color.a}" }
				};

				if (FadeInTime != 0)
					Json["fadeIn"] = FadeInTime;

				if (CloseElementName != null)
					Json["close"] = CloseElementName;

				if (Command != null)
					Json["command"] = Command;

				return Json;
			}
		}

		public class ImageComponent : GUIComponentBase
		{
			public Color Color
			{
				get { return color; }
				set { IsDirty = true; color = value; }
			}
			private Color color = new Color(1.0f, 1.0f, 1.0f, 1.0f);

			public ImageComponent()
			{
				Type = "UnityEngine.UI.Image";
			}

			public override JObject ToJson()
			{
				if (!IsDirty)
					return Json;

				IsDirty = false;

				Json = new JObject
				{
					{ "type", Type },
					{ "color", $"{color.r} {color.g} {color.b} {color.a}" }
				};

				return Json;
			}
		}

		public class RawImageComponent : GUIComponentBase
		{
			public string Url
			{
				get { return url; }
				set { IsDirty = true; url = value; }
			}
			public Color Color
			{
				get { return color; }
				set { IsDirty = true; color = value; }
			}
			
			private string url;
			private Color color = new Color(1.0f, 1.0f, 1.0f, 1.0f);

			public RawImageComponent()
			{
				Type = "UnityEngine.UI.RawImage";
			}

			public override JObject ToJson()
			{
				if (!IsDirty)
					return Json;

				IsDirty = false;

				Json = new JObject
				{
					{ "type", Type },
					{ "color", $"{color.r} {color.g} {color.b} {color.a}" }
				};

				if (FadeInTime != 0)
					Json["fadeIn"] = FadeInTime;

				if (Url != null)
					Json["url"] = Url;

				return Json;
			}
		}

		/// <summary>
		/// Take this if you need visible cursor on your UI
		/// </summary>
		public class CursorComponent : GUIComponentBase
		{
			public CursorComponent()
			{
				Type = "NeedsCursor";
				Json = new JObject
				{
					{ "type", Type }
				};
			}

			public override JObject ToJson()
			{
				return Json;
			}
		}

		/// <summary>Must be placed at the end of element component list</summary>
		public class RectTransformComponent : GUIComponentBase
		{
			/// <summary>
			/// Position relative to parent from lower left corner
			/// </summary>
			public Vector2 AnchorMin
			{
				get { return _anchorMin; }
				set
				{
					IsDirty = true;
					_anchorMin = value;
					OnStateChanged(null, true);
				}
			}

			private Vector2 _anchorMin = new Vector2(0f, 0f);

			/// <summary>
			/// Position relative to parent from top right corner
			/// </summary>
			public Vector2 AnchorMax
			{
				get { return _anchorMax; }
				set
				{
					IsDirty = true;
					_anchorMax = value;
					OnStateChanged(null, true);
				}
			}

			private Vector2 _anchorMax = new Vector2(1f, 1f);

			/// <summary>
			/// Position relative to anchor from bottom left corner
			/// </summary>
			public Vector2 OffsetMin
			{
				get { return _offsetMin; }
				set
				{
					IsDirty = true;
					_offsetMin = value;
					OnStateChanged(null, true);
				}
			}

			private Vector2 _offsetMin = new Vector2(0f, 0f);

			/// <summary>
			/// Position relative to anchor from top right corner
			/// </summary>
			public Vector2 OffsetMax
			{
				get { return _offsetMax; }
				set
				{
					IsDirty = true;
					_offsetMax = value;
					OnStateChanged(null, true);
				}
			}

			private Vector2 _offsetMax = new Vector2(1f, 1f);

			public RectTransformComponent()
			{
				Type = "RectTransform";
			}

			public override JObject ToJson()
			{
				if (!IsDirty)
					return Json;

				IsDirty = false;

				Json = new JObject
				{
					{ "type", Type },
					{ "anchormin", $"{AnchorMin.x} {AnchorMin.y}" },
					{ "anchormax", $"{AnchorMax.x} {AnchorMax.y}" },
					{ "offsetmin", $"{OffsetMin.x} {OffsetMin.y}" },
					{ "offsetmax", $"{OffsetMax.x} {OffsetMax.y}" },
				};

				return Json;
			}
		}

		public class GUIStateChangedArgs : EventArgs
		{
			public bool NeedsUpdate { get; set; }
			public BasePlayer From { get; set; }
		}

		/// <summary>
		/// Base class for components
		/// </summary>
		public abstract class GUIComponentBase
		{
			/// <summary>
			/// Fires when any important property has changed
			/// </summary>
			public event EventHandler<GUIStateChangedArgs> StateChanged;

			public float FadeInTime
			{
				get { return fadeInTime; }
				set
				{
					IsDirty = true;
					fadeInTime = value;
				}
			}
			private float fadeInTime;

			protected string Type;
			protected bool IsDirty = true;
			protected JObject Json = new JObject();

			public virtual JObject ToJson() => Json;

			protected void OnStateChanged(BasePlayer player = null, bool needsUpdate = false)
			{
				StateChanged?.Invoke(this, new GUIStateChangedArgs { From = player, NeedsUpdate = needsUpdate });
			}
		}
	}
}
