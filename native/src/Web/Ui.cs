// What the already-ported game code asked of the screens before the DOM existed;
// each is now the DOM call the JavaScript made.
namespace Brickblade;

public static class Ui
{
    public static void Hide(string id) => Dom.document?.getElementById(id)?.classList.add("hidden");
    public static void Cursor(string c) { var cv = Dom.document?.getElementById("game"); if (cv != null) cv.style.cursor = c; }
    public static void TitleDirty() => G.fitTitleSoon();
    public static bool TextFocused => Dom.TextFocused;
    /* a text field owns its keys; Esc leaves it (the JS keydown handler's first lines) */
    public static void TextKey(KeyEvt e) { if (e.code == "Escape") Dom.focused?.blur(); }
}
