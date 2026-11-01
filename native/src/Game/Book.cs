// lego-survival.html, JS 20312-20463: the spellbook, and the two toggles that
// stop the wave while a screen of cards is open.

namespace Brickblade;

public static partial class G
{
    public static bool bookOn = false;
    /* one card face, at one rarity. `wire` marks it for the now -> next hover panel;
       a card inside an opened fan is a record of a pick and gets no such panel. */
    public static string bookFace(Upgrade u, string rarKey, double n, bool wire)
    {
        var R = RARITY[rarKey];
        string body = "";
        try { body = esc(u.desc(u.calc(R.mult), n)); } catch { body = ""; }
        return $@"<div class=""card mini""{(wire ? $" data-up=\"{u.id}\"" : "")}>
      <div class=""band"" style=""background:{R.col}""><i></i><i></i><i></i></div>
      <svg viewBox=""0 0 24 24"" fill=""none"" stroke=""{R.col}"" stroke-width=""2""
        stroke-linecap=""round"" stroke-linejoin=""round"">{ICONS.GetValueOrDefault(u.icon ?? "") ?? ""}</svg>
      <h3>{esc(u.name)}</h3><p>{body}</p>
      <div class=""ctag"" style=""background:{CLS_COL.GetValueOrDefault(u.cls ?? "") ?? "#555"}"">{u.cls}</div>
    </div>";
    }
    /* WHICH COPIES YOU ACTUALLY TOOK. The build log records a rarity per pick, so a
       deck can be opened into the real cards rather than n copies of a guess. A
       brick handed over by a favourite's starting kit has no pick behind it, so the
       fan is padded from the front and says so. */
    public static List<string> bookPicks(string id, double n)
    {
        var got = buildLog.Where(e => e.id == id).Select(e => e.rar).ToList();
        var outp = new List<string>();
        for (int i = got.Count; i < n; i++) outp.Add(null);     /* starting kit, unrolled */
        foreach (var r in got) outp.Add(r);
        int from = (int)Math.Max(0, outp.Count - n);
        return outp.Skip(from).ToList();
    }
    public static string bookCard(Upgrade u, double n)
    {
        var R = RARITY[benchRarOf(u)];
        /* one ghost per extra copy, up to four — past that the count badge says it
           better than a stack of slivers does */
        var ghosts = Math.min(4, Math.max(0, n - 1));
        string g = "";
        for (double i = ghosts; i > 0; i--)
            g += $"<div class=\"ghost\" style=\"transform:translate({S(i * 5)}px,{S(i * 6)}px) rotate({S(i * 1.3)}deg)\"></div>";
        var picks = bookPicks(u.id, n);
        var fan = string.Join("", picks.Select((rk, i) =>
        {
            var key = rk ?? benchRarOf(u);
            var lbl = rk != null ? RARITY[key].name : "starting kit";
            return $@"<div class=""fancard"">{bookFace(u, key, i + 1, false)}
      <div class=""pick"">{esc(S(lbl).ToUpperInvariant())}</div></div>";
        }));
        return $@"<div class=""deck"" data-deck=""{u.id}"">{g}
    {(n > 1 ? $"<div class=\"dcount\">×{S(n)}</div>" : "")}
    {bookFace(u, benchRarOf(u), n, true)}
    <div class=""fan"">{fan}</div></div>";
    }
    public static void renderSpellbook()
    {
        var body = document.getElementById("bookBody");
        if (body == null || player == null) return;
        var byId = new JsMap<string, Upgrade>(); foreach (var u in UPGRADES) byId[u.id] = u;
        var owned = taken.Keys.Where(id => byId.ContainsKey(id) && taken[id] > 0).ToList();
        double cards = 0; foreach (var id in owned) cards += taken[id];
        document.getElementById("bookSub").textContent =
          $"{S(owned.Count)} brick{(owned.Count == 1 ? "" : "s")} · {S(cards)} card{(cards == 1 ? "" : "s")} · " +
          $"{S(spellsOwned())} of {S(SPELL_MAX)} spell slots · {weapon.name} · level {S(player.level)}";

        /* the bench's own filing, so a brick is in the same place in both screens */
        var used = new HashSet<string>(); var secs = new List<(string name, List<string> ids)>();
        foreach (var sec in BENCH_SECTIONS)
        {
            var list = sec.ids.Where(id => owned.Contains(id) && !used.Contains(id)).ToList();
            foreach (var id in list) used.Add(id);
            if (list.Count > 0) secs.Add((sec.name, list));
        }
        var rest = owned.Where(id => !used.Contains(id)).ToList();
        if (rest.Count > 0) secs.Add(("EVERYTHING ELSE", rest));

        /* a spell section gets the spell's own live summary above its bricks, which
           is the one thing the bench has no reason to show and a spellbook does */
        var sums = new JsMap<string, Ability>(); foreach (var sp in spellSummaries()) sums[sp.id] = sp;
        var html = string.Join("", secs.Select(s =>
        {
            var (name, ids) = s;
            double n = 0; foreach (var id in ids) n += taken[id];
            var spk = SECTION_SPELL.GetValueOrDefault(name);
            var sp = spk != null ? sums.GetValueOrDefault(spk) : null;
            return $@"<div class=""booksec"">
      <div class=""bookhead"">{esc(name)}<em>{S(ids.Count)} brick{(ids.Count == 1 ? "" : "s")}, {S(n)} card{(n == 1 ? "" : "s")}</em></div>
      {(sp != null ? $@"<div class=""bookspell"">
         <svg viewBox=""0 0 24 24"" fill=""none"" stroke=""#d6a6ff"" stroke-width=""2""
           stroke-linecap=""round"" stroke-linejoin=""round"">{ICONS.GetValueOrDefault(sp.icon ?? "") ?? ""}</svg>
         <b>{esc(sp.name)}</b> <span>{esc(sp.head)}</span></div>" : "")}
      <div class=""bookrow"">{string.Join("", ids.Select(id => bookCard(byId[id], taken[id])))}</div>
    </div>";
        }));
        body.innerHTML = $"<div class=\"bookgrid\">{(html != "" ? html :
          "<div class=\"booknone\">Nothing taken yet. Bricks come from waves, levels, bosses and chests.</div>")}</div>";
        /* the same now -> next panel the bench and the reward screen use. Only the
           closed face of a deck carries it — a card in an opened fan is history, and
           "one more" is not a question you are asking of it. */
        foreach (var el in body.querySelectorAll(".card.mini[data-up]"))
        {
            var u = byId.GetValueOrDefault(el.dataset["up"] ?? "");
            if (u != null) cmpWire(el, u, u.calc(RARITY[benchRarOf(u)].mult),
              u.name + " · " + RARITY[benchRarOf(u)].name + " · one more");
        }
        /* click a stack to see the copies; opening one closes whatever was open */
        foreach (var deck in body.querySelectorAll(".deck"))
        {
            var dk = deck;
            dk.addEventListener("click", ev =>
            {
                ev.stopPropagation();
                var was = dk.classList.contains("open");
                foreach (var other in body.querySelectorAll(".deck.open")) other.classList.remove("open");
                if (!was) { dk.classList.add("open"); cmpHide(); }
                SFX.click();
            });
        }
        /* anywhere else in the book closes it again */
        body.onclick = _ => { foreach (var d in body.querySelectorAll(".deck.open")) d.classList.remove("open"); };
    }
    /* which bench section belongs to which spell, so the book can head a section
       with that spell's live numbers */
    public static readonly JsMap<string, string> SECTION_SPELL = new()
    {
        ["GUARDIAN BRICK"] = "orbit", ["STORM BRICK"] = "chain",
        ["BLADESTORM"] = "storm", ["BLADE VORTEX"] = "vortex", ["BOMB VOLLEY"] = "bombplus",
        ["BLOCK FREEZE"] = "freeze", ["BRICKBANE"] = "bane"
        /* NB: the bomb summary is filed under its brick id, not the spell key */
    };
    public static void toggleBook()
    {
        var el = document.getElementById("bookScreen");
        if (!bookOn && state == "play")
        {
            bookOn = true;
            renderSpellbook();
            el.classList.remove("hidden");
            state = "book"; cvEl.style.cursor = "default";
            SFX.click();
        }
        else if (bookOn)
        {
            closeBook();
            try { window.focus(); } catch { }
            SFX.click();
        }
    }
    /* THE BOOK STOPS THE WAVE, the way the character sheet does. It was built not
       to, and that was simply wrong: reading nine sections of cards while a wave 40
       pack walks into you is not a choice anyone makes twice. It takes its own
       state, so the update loop freezes and the world behind it stays drawn, and
       nothing in it needs refreshing any more because nothing is moving. */
    public static void closeBook()
    {
        if (!bookOn) return;
        bookOn = false;
        var el = document.getElementById("bookScreen");
        if (el != null) el.classList.add("hidden");
        if (state == "book") { state = "play"; cvEl.style.cursor = "none"; }
        cmpHide();
    }
    public static void toggleChar()
    {
        if (state == "play")
        {
            renderCharSheet();
            document.getElementById("charScreen").classList.remove("hidden");
            state = "char"; cvEl.style.cursor = "default";
            SFX.click();
        }
        else if (state == "char")
        {
            document.getElementById("charScreen").classList.add("hidden");
            state = "play"; cvEl.style.cursor = "none";
            try { window.focus(); } catch { }
            SFX.click();
        }
    }
}
