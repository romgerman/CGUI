
# CGUI
This plugin is heavily WIP.

## Files
`CGUI` — Base plugin. Contains base classes.

`GUILibrary` — Requires `CGUI`. Contains basic UI elements.

`GUITest` — Is for testing.

## Usage
`GUIObject` represents a basic UI element and acts as `ICollection` for components derived from `GUIComponentBase`.
**Example:**
```charp
// Basic text UI element
var element = new GUIObject()
{
	new TextComponent("Example"),
	new RectTransformComponent()
};
```
To show your UI to a player you can use `GUIObject.ShowUI(BasePlayer)`.

To make more advanced elements you should make your own classes derived from `GUIElementBase` or use classes from `GUILibrary`.

`GUIElementBase` allows you to easily combine basic `GUIObject` elements. To show a `GUIElementBase` element to a player you should to use `Representation` class which acts as `ICollection` for `GUIElementBase`.
**For example** this is a constructor for `GUIButton` class (`GUILibrary`):
```csharp
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
```
Every class derived from `GUIElementBase` should implement its own `Render` method.
**For example:**
```csharp
public override ICollection<GUIObject> Render()
{
	return new GUIObject[] { ButtonElement, TextElement };
}
```
The method is pretty straightforward. But one thing is — you should follow the inheritance order. So the previous element should be a parent of the next element.

If no name is present for `GUIObject` it generates the name automatically using `GUIObject.GenerateId()`.

## Data binding
Data binding is WIP. Examples can be found inside `GUILibrary`.

## Markup language
WIP