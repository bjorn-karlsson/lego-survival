// lego-survival.html, JS 21195-21304: opening a reward screen, rerolling it,
// taking a card from it, and turning it down for studs.
namespace Brickblade;

public static partial class G
{
    public static void openUpgrades(string mode = "wave", string forceRar = null)
    {
        /* CLOSE THE BOOK FIRST, then guard. The book holds its own state, so doing the
           guard first meant a level-up or a cleared wave that landed while the book
           was open hit `state !== 'play'` and was thrown away — the reward simply
           never appeared. closeBook() puts the state back, and then the guard is
           asking the question it means to ask. */
        closeBook();
        if (state != "play") return;
        upgradeMode = mode ?? "wave";
        forcedRarity = string.IsNullOrEmpty(forceRar) ? null : forceRar;
        screenRerolls = 0;
        var stats = new (string k, string v)[]
        {
            ("Max HP", S(player.maxHp)),
            ("Regen", player.regenRate > 0 ? player.regenRate.toFixed(2) + "/s" : "—"),
            ("Move spd", (player.speed / 215).toFixed(2) + "×"),
            ("Crit", (critChance() * 100).toFixed(1) + "%"),
            ("Crit mult", S(critMultPct()) + "%"),
            ("Armour", S(Math.round(armourValue()))),
            (isCaster() ? "Bolt hit" : isSlammer() ? "One tooth" : "Sword hit",
             (isCaster() ? boltDamage() : isSlammer() ? slamSpikeDamage() : swordDamage()).toFixed(1)),
            ("Inc. melee", S(Math.round(incTotal("melee") * 100)) + "%"),
            ("Inc. spell", S(Math.round(incTotal("spell") * 100)) + "%"),
            ("More dmg", "×" + moreShown().toFixed(2)),
            ("Suppress", S(Math.round(suppressChance() * 100)) + "%"),
            ("Stud gain", "×" + player.studMul.toFixed(2)),
            ("XP gain", "×" + player.xpMul.toFixed(2)),
            ("Heart drop", (heartDropChance() * 100).toFixed(2) + "%"),
            ("Spells", S(spellsOwned()) + " / " + S(SPELL_MAX))
        };
        var sp = document.getElementById("statsPanel");
        if (sp != null) sp.innerHTML = string.Join("", stats.Select(s =>
          $"<div class=\"chip\"><b style=\"color:#ffd76e\">{s.k}</b> {s.v}</div>"));
        pickChoices(upgradeMode);
        renderCards();
        document.getElementById("rerollRow").style.display = mode == "chest" ? "none" : "flex";
        /* THE BRICKS YOU ALREADY HAVE are not listed under the cards any more: the
           spellbook (and the C sheet) already show them, and on a long run the list
           grew until it pushed the cards themselves off the screen. The build stats
           stay above the cards. */
        var chips = document.getElementById("buildChips");
        if (chips != null) chips.innerHTML = "";
        document.getElementById("upTitle").textContent =
          mode == "chest" ? "TREASURE CHEST!" :
          mode == "bonus" ? "CALLED-IN WAVE!" :
          mode == "wave" ? "WAVE " + S(wave.n) + " CLEARED!" : mode == "boss" ? "BOSS REWARD!" : "LEVEL UP!";
        document.getElementById("upSub").textContent =
              mode == "chest" ? "One brick inside — take it" :
          mode == "bonus" ? "You called it down on yourself — this one is the fee"
          : mode == "boss" ? "Four crown bricks to choose from — R rerolls twice for studs"
          : mode == "wave" ? "Grab a new brick — 1, 2 or 3 · R rerolls (max 2) for studs"
          : "Level up! Pick a brick — 1, 2 or 3 · R rerolls (max 2) for studs";
        document.getElementById("upgradeScreen").classList.remove("hidden");
        state = "upgrade"; cvEl.style.cursor = "default";
    }
    /* HOW MANY REROLLS THIS SCREEN HAS LEFT -- one rule, read by the button AND by
       the key. The key used to count only the plain two a screen, so Scavenger's
       "no rerolls, ever" greyed the button out while R went on rerolling. */
    public static double rerollsLeft()
    {
        if (player == null || player.noReroll) return 0;
        return Math.max(0, (2 + player.rerollBonus) - screenRerolls);
    }
    public static void doReroll()
    {
        if (state != "upgrade" || upgradeMode == "chest") return;   /* chests offer one brick, no reroll */
        if (rerollsLeft() <= 0) return;
        var cost = rerollCost();
        if (player.studs < cost) return;
        player.studs -= cost;
        rerollsUsed++;
        screenRerolls++;
        SFX.pick();
        shunCards(choices);          /* before the redraw, or it shuns what it just drew */
        pickChoices(upgradeMode);
        renderCards();
    }
    public static void chooseUpgrade(int i)
    {
        if (state != "upgrade" || i < 0 || i >= choices.Count || choices[i] == null) return;
        cmpHide();          /* however the screen was left — click, 1/2/3 or X */
        var card = choices[i];
        applyPick(card.t, card.rar);
        SFX.pick();
        document.getElementById("upgradeScreen").classList.add("hidden");
        state = "play"; cvEl.style.cursor = "none";
        try { window.focus(); } catch { }
        addRing(player.x, player.y, 10, 400, 130, RARITY[card.rar].col, 6, 0.5);
        lootNext();
    }
    /* Turning a reward down. You are paid for the best thing you walked away from,
       so refusing a legendary is worth real money and refusing three commons is not. */
    public static double rejectValue()
    {
        double best = 0;
        foreach (var c in choices) best = Math.max(best, REJECT_STUDS[c.rar]);
        return Math.round(best * Js.Or(player.studMul, 1));
    }
    public static void rejectReward()
    {
        if (state != "upgrade" || choices.Count == 0) return;
        cmpHide();
        var paid = rejectValue();
        player.studs += paid;
        SFX.stud(6); SFX.pick();
        document.getElementById("upgradeScreen").classList.add("hidden");
        state = "play"; cvEl.style.cursor = "none";
        try { window.focus(); } catch { }
        addDmg(player.x, player.y - 56, "+" + S(paid) + " STUDS", "#ffd166", 20);
        addRing(player.x, player.y, 10, 380, 120, "#ffd166", 6, 0.45);
        spawnStuds(player.x, player.y, 6);
        lootNext();
    }
    /* JS 21303-21304, run at load */
    public static void RewardButtonsWire()
    {
        document.getElementById("rejectBtn").addEventListener("click", _ => { SFX.click(); rejectReward(); });
        document.getElementById("rerollBtn").addEventListener("click", _ => doReroll());
    }
}
