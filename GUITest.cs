// Requires: CGUI
// Requires: GUILibrary
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Oxide.Plugins
{
	[Info("GUI Test", "", 0.1)]
	public class GUITest : RustPlugin
	{
		private CGUI.Representation _display = new CGUI.Representation();
		private bool _show = false;
		private CGUI.PlayerStateManager state = new CGUI.PlayerStateManager();
		private GUILibrary.GUIList list;
		private GUILibrary.GUIWindow window;
		private GUILibrary.GUIGrid grid;

		private void Init()
		{
			grid = new GUILibrary.GUIGrid(2, 2);
			list = new GUILibrary.GUIList();
			var collection = new CGUI.ChangeableCollection<string>();
			collection.Add("Hello");
			collection.Add("Hello1");
			collection.Add("Hello2");
			collection.Add("Hello3");
			collection.Add("Hello4");
			collection.Add("Hello5");
			list.BindCollection(collection);

			grid.SetElement(0, 0, list);

			window = new GUILibrary.GUIWindow("Test window", list, null);
			_display.Add(grid);
			
			//Puts(_display.ToJson().ToString(Newtonsoft.Json.Formatting.Indented));
		}

		[ChatCommand("test")]
		private void TestCommand(BasePlayer player, string command, string[] args)
		{
			if (!_show)
				_display.AddUI(player);
			else
				_display.RemoveUI(player);

			_show = !_show;

			/*state.ChangeState(player, button, (state) =>
			{
				state["Text"] = "heh";
				return state;
			});

			state.SetState(player, button);*/
		}
	}
}
