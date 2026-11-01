namespace Brickblade;

/* =============================== ENEMIES (JS 6234–6973) ===============================
   Spawning a monster, landing a hit on one, and every lingering thing a hit
   leaves on a body: poison, shock, chill and frozen, burning, bleeding. */

/* what a hit carries besides its size: the JS passes these as `opts` */
public sealed class HitOpts
{
    public bool spin, quiet, drain, reave, forceCrit, noCrit, noMit, noAil, ailSure;
    public double ailDepth, ailN;
}
/* a body's {n, t}: frozen, burnt and shocked are all a count with a clock */
public sealed class Mark { public double n, t; }
/* a boss's or a golem's shockwave as it rolls out */
public sealed class BossRing { public double r, max, speed, dmg = double.NaN; public bool hit, heroHit; public string golem; public HashSet<Ent> mHit; }
/* EVERY FLYING THING: an arrow, a bolt, a skull, an ember, a bomb, a boomerang...
   one class with every field any of them carries */
public sealed partial class Proj : Obj
{
    /* a lobbed thing is made with sx/sy and no x/y; the JS reads its x as undefined
       until the first frame puts it at sx (updateProjectiles) */
    public Proj() { x = double.NaN; y = double.NaN; }
    public string kind, owner; public double vx, vy, life, max, t, seed = double.NaN;
    public double px = double.NaN, py = double.NaN, hitR = double.NaN;
    public Ent tgt, src; public bool loop; public double depth, @out;
    public double a0, ang, arc, delay, dot, dotT, dur, flap, fr, fuse, gen, olife, pfall, rMax, rico, size, spin, stacks;
    public double sx = double.NaN, sy = double.NaN, tx, ty, tr, trailD, trailLife, turn, zapCd, cluster, legs;
    public bool arrow, big, gib, homing, missile, small;
    public string art, curse, el, phase;
    public HashSet<object> hit;
}

public static partial class G
{
    public static void spawnEnemy(string type, double x, double y, bool elite = false, bool magic = false, string forceBossKey = null)
    {
        double tier = Math.Max(1, Math.Floor(wave.n / 5));
        if (type == "boss")
        {
            /* a forced key (wave-50 raid) picks its own tier -- it may name an ultra */
            bool isSuper = forceBossKey != null ? SUPERBOSSES.ContainsKey(forceBossKey) : (wave.n % 10 == 0);
            EDef B; Ent e;
            if (isSuper)
            {
                string skey = forceBossKey ?? ultraForWave(wave.n);
                B = SUPERBOSSES[skey];
                double st = Math.Max(1, Math.Floor(wave.n / 10));
                /* tiers must feel wildly different; this is THE curve */
                double bhp = Math.Round(bossPool(B.hp, st) * Js.Or(B.hpMul, 1) * difficulty.hp * mobRankMul());
                e = new Ent
                {
                    type = "boss", x = x, y = y, bkey = skey, bname = B.name, super = true, sig = B.sig, btier = tier + 2,
                    col = B.col, style = B.style, r = B.r, scale = B.scale,
                    hp = bhp, maxHp = bhp, armour = (70 + st * 70 + st * st * 45) * Js.Or(B.armourMul, 1),
                    res = new NumMap(("fire", 0), ("frost", 0), ("shock", 0)).Assign(B.res),
                    suppress = B.style == "skel" ? 0.62 : 0.30,
                    speed = B.speed * (1 + tier * 0.03),
                    dmg = Math.Round((B.dmg * (1 + st * 1.5) + Math.Pow(st, 1.55) * 9) * Js.Or(B.dmgMul, 1) * mobRankMul()),
                    mlvl = mobRank(),
                    xp = Math.Round(B.xp * (1 + (st - 1) * 0.8)), studsDrop = new DropRange { a = B.studs[0], b = B.studs[1] },
                    atkS = Math.Max(0.42, (1 - (wave.n - 1) * 0.02) * difficulty.atk * Js.Or(B.atkMul, 1)), touchCd = 0.8,
                    chaosShare = B.chaos,
                    face = new Pt(0, 1), faceA = rnd(TAU), walk = rnd(TAU), moving = false, state = "build", buildT = 0,
                    flash = 0, hitCd = 0, atkCd = 0, kx = 0, ky = 0, mode = "chase", modeT = 0,
                    strafe = 1, strafeT = 2, elite = false, magic = false, dead = false, phase = rnd(TAU),
                    aimT = 0, docile = false, wx = 0, wy = 0, wanderT = 0, orbCd = 0, vortCd = 0, slowT = 0,
                    stuckT = 0, avoidT = 0, avoidA = 0, _lx = x, _ly = y, stunT = 0,
                    path = null, pathI = 0, pathT = 0, pathGoal = null, slideT = 0, slideSign = 1,
                    nextAtk = 1.8, ring = null, cycle = 0
                };
            }
            else
            {
                /* normal boss -- OR forced boss key (wave-50 raid uses every boss) */
                string bkey;
                if (forceBossKey != null) { bkey = forceBossKey; B = BOSSES[bkey]; }
                else
                {
                    var keys2 = BOSSES.Keys.ToList();
                    bkey = keys2[((int)tier - 1) % keys2.Count];
                    B = BOSSES[bkey];
                }
                /* HALF AN ULTRA, ON THE ULTRA'S OWN CURVE -- read at a half-step */
                double st = Math.Max(1, wave.n / 10);
                double bhp = Math.Round(bossPool(B.hp * BOSS_BASE_MUL, st) * BOSS_SHARE * difficulty.hp * mobRankMul());
                e = new Ent
                {
                    type = "boss", x = x, y = y, bkey = bkey, bname = B.name, btier = tier,
                    col = B.col, style = B.style,
                    r = B.r, scale = B.scale,
                    hp = bhp, maxHp = bhp, armour = 24 + wave.n * 2.2 + tier * tier * 14,
                    /* a bone boss suppresses half of every spell that lands on it */
                    suppress = B.style == "skel" ? 0.45 : 0.15,
                    speed = B.speed * (1 + tier * 0.05),
                    dmg = Math.Round((B.dmg * (1 + tier * 1.1) + tier * tier * 1.6) * Math.Pow(mobRankMul(), 0.6)),
                    xp = B.xp, studsDrop = new DropRange { a = B.studs[0], b = B.studs[1] },
                    atkS = Math.Max(0.6, 1 - (wave.n - 1) * 0.02), touchCd = 0.9,
                    chaosShare = B.chaos,
                    face = new Pt(0, 1), faceA = rnd(TAU), walk = rnd(TAU), moving = false, state = "build", buildT = 0,
                    flash = 0, hitCd = 0, atkCd = 0, kx = 0, ky = 0, mode = "chase", modeT = 0,
                    strafe = 1, strafeT = 2, elite = false, magic = false, dead = false, phase = rnd(TAU),
                    aimT = 0, docile = false, wx = 0, wy = 0, wanderT = 0, orbCd = 0, vortCd = 0, slowT = 0,
                    stuckT = 0, avoidT = 0, avoidA = 0, _lx = x, _ly = y, stunT = 0,
                    path = null, pathI = 0, pathT = 0, pathGoal = null, slideT = 0, slideSign = 1,
                    nextAtk = 2.2, ring = null, cycle = 0
                };
                e.res = new NumMap(); /* the JS gives an ordinary boss no `res` at all; reading one gives 0 */
            }
            /* the endgame multiplier, before the traits and the shield so both ride it */
            { double lm = bossLateMul(); if (lm > 1) { e.maxHp = Math.Round(e.maxHp * lm); e.hp = e.maxHp; } }
            rollBossTraits(e);
            applyWaveMods(e);
            /* THE SHIELD IS A SHARE OF WHAT IT ENDED UP WITH */
            if (B.es > 0)
            {
                e.esMax = Math.Round(e.maxHp * B.es);
                e.es = e.esMax; e.esT = 0; e.esFlash = 0;
            }
            enemies.Add(e);
            SFX.build();
            return;
        }
        var d = EDEF[type];
        double lvl = mobLevel();
        /* the wave curve, and then the monster's own LEVEL multiplied on top of it */
        double rank = mobRank(), rankMul = mobRankMul(rank);
        double hpS = (1 + (lvl - 1) * 0.30 + Math.Pow(Math.Max(0, lvl - 5), 1.6) * 0.05) * difficulty.hp * rankMul;
        /* flat armour, fed into the same DR curve the player uses */
        double armour = lvl * 1.1 * (type == "bastion" ? 1.8 : 1) * Js.Or(d.armourMul, 1)
            * (RANGED_SET.Contains(type) ? 0.45 : 1) * (elite ? 1.4 : 1) * (d.golem != null ? 1.6 : 1) * rankMul;
        /* spell suppression: the arcane ones shrug off half a spell hit */
        double suppress = magic ? 0.30 : type.EndsWith("lord") ? 0.20 : 0;
        double dmgB = ((lvl - 1) * 0.55 + Math.Pow(Math.Max(0, lvl - 8), 1.45) * 0.14) * rankMul;
        double spdS = (1.07 + Math.Min(0.55, (lvl - 1) * 0.028)) * difficulty.spd;
        double atkS = Math.Max(0.58, 1 - (lvl - 1) * 0.03) * difficulty.atk;
        if (magic) atkS *= 0.7;
        double hp0 = Math.Ceiling(d.hp * hpS * (elite ? 3.4 : 1) * (magic ? 1.18 : 1));
        var en = new Ent
        {
            type = type, x = x, y = y, r = d.r * (elite ? 1.15 : 1), scale = d.scale * (elite ? 1.12 : 1),
            dmg = Math.Max(1, Math.Round((d.dmg + dmgB) * (elite ? 1.5 : 1) * difficulty.dmg)),
            /* THE ELITE IS HARDER and the magic body is softer than they were */
            hp = hp0, maxHp = hp0,
            armour = armour, suppress = suppress, mlvl = rank,
            /* a COPY, never the EDEF object itself */
            res = new NumMap(("fire", 0), ("frost", 0), ("shock", 0)).Assign(d.res),
            speed = d.speed * spdS * (elite ? 0.95 : 1) * (magic ? 1.12 : 1),
            atkS = atkS, fire = Js.T(d.fire) ? d.fire * atkS : 0, touchCd = 0.9 * atkS,
            col = d.col, style = d.style, xp = d.xp, studsDrop = new DropRange { a = d.studs[0], b = d.studs[1] },
            face = new Pt(0, 1), faceA = rnd(TAU), walk = rnd(TAU), moving = false, state = "build", buildT = 0,
            flash = 0, hitCd = 0, atkCd = rnd(0.5, 1.5) * atkS, kx = 0, ky = 0, mode = "chase", modeT = 0,
            strafe = Js.R() < 0.5 ? 1 : -1, strafeT = rnd(1, 3), elite = elite, magic = magic, dead = false, phase = rnd(TAU),
            aimT = 0, docile = false, wx = 0, wy = 0, wanderT = 0, orbCd = 0, vortCd = 0, slowT = 0,
            stuckT = 0, avoidT = 0, avoidA = 0, _lx = x, _ly = y, golem = d.golem, stompT = -1, stunT = 0, totCd = 0,
            shade = d.shade, zorb = d.zorb,
            /* TWO RATINGS, DERIVED FROM WHAT IT IS */
            evasion = Math.Round(mobEvasionOf(d) * (elite ? EVA_ELITE : 1) * (magic ? EVA_MAGIC : 1)),
            accuracy = Math.Round(mobAccuracyOf(d) * (elite ? 1.15 : 1) * (magic ? 1.08 : 1)),
            mini = d.mini, touchElem = d.elem,
            /* BLOCK is a shield's, and only a body that carries one has any */
            block = d.shield ? (!double.IsNaN(d.block) ? d.block : BLOCK_BASE) : 0,
            path = null, pathI = 0, pathT = 0, pathGoal = null, slideT = 0, slideSign = 1
        };
        /* LAST, so a modifier multiplies the finished number */
        applyWaveMods(en);
        if (elite) rollRareMods(en);
        enemies.Add(en);
        SFX.build();
    }

    /* Where the player will BE, not where they are */
    public static Pt leadPlayer(double fromX, double fromY, double travelT, double accuracy = double.NaN)
    {
        var p = player;
        double acc = double.IsNaN(accuracy) ? 1 : accuracy;
        double vx = (p.x - (double.IsNaN(p.leadX) ? p.x : p.leadX)) / Math.Max(0.016, Js.Or(p.leadDt, 0.016));
        double vy = (p.y - (double.IsNaN(p.leadY) ? p.y : p.leadY)) / Math.Max(0.016, Js.Or(p.leadDt, 0.016));
        double sp = hypot(vx, vy);
        double cap = p.speed * 1.1;
        double k = sp > cap ? cap / sp : 1;
        return new Pt(clamp(p.x + vx * k * travelT * acc, W.wall + 40, W.w - W.wall - 40),
                      clamp(p.y + vy * k * travelT * acc, W.wall + 40, W.h - W.wall - 40));
    }
    public static Pt randomSpawnPos()
    {
        double lo = W.wall + 130, hiX = W.w - W.wall - 130, hiY = W.h - W.wall - 130;
        for (int i = 0; i < 40; i++)
        {
            double x, y;
            if (player != null && i < 32)
            {
                /* ring around the player -- far enough to be fair, close enough to matter */
                double a = rnd(TAU), rr = rnd(480, 1150);
                x = clamp(player.x + Math.Cos(a) * rr, lo, hiX);
                y = clamp(player.y + Math.Sin(a) * rr, lo, hiY);
            }
            else { x = rnd(lo, hiX); y = rnd(lo, hiY); }
            var pt = new Pt(x, y);
            if (player != null && dist(pt, player) < 440) continue;
            bool bad = false;
            foreach (var s in solids.Live()) if (dist(pt, s) < s.r + 30) bad = true;
            foreach (var w in waters.Live()) if (x > w.x - 30 && x < w.x + w.w + 30 && y > w.y - 30 && y < w.y + w.h + 30) bad = true;
            if (!bad) return pt;
        }
        return new Pt(clamp((player != null ? player.x : CX) + 520, lo, hiX), clamp((player != null ? player.y : CY) + 520, lo, hiY));
    }
    /* every knockback impulse goes through this one scale */
    public const double KB_SCALE = 0.45;
    public const double BLOCK_BASE = 0.5;      /* a shield's block chance, unless its row names its own */
    public const double BLOCK_ARC = 1.05;      /* radians either side of where it faces */
    sealed class HitPart { public string k; public double part; }
    public static void hitEnemy(Ent e, double dmg, double kdir, double kb = 220, string source = "sword", HitOpts opts = null)
    {
        opts ??= new HitOpts();
        kb *= KB_SCALE;
        if (e.dead || e.state != "active") return;
        if (e.breach != null && !breachSees(e)) return;   /* outside its circle it is not there to hit */
        if (ritualOutside(e)) return;                    /* beyond a running ritual's wall */
        /* PROXIMAL TANGIBILITY: out of its ring, nothing of yours touches it */
        if (Js.T(e.rmNear))
        {
            var h = heroObj ?? player;
            if (h != null && hypot(h.x - e.x, h.y - e.y) > e.rmNear)
            {
                if (e.rmNearT <= vtime) { e.rmNearT = vtime + 0.6; addHitNum(e.x, e.y - 44 * Js.Or(e.scale, 1), "INTANGIBLE", "#9fd4ff", 13); }
                return;
            }
        }
        /* a sleeping ritual guard wakes -- and its whole pack with it */
        if (e.dormant) { if (e.ritualGuard != null) ritualWake(e.ritualGuard); else if (Js.T(e.rpgPack)) rpgWakePack(e.rpgPack); e.dormant = false; }
        /* DOES THE SWING FIND IT? Accuracy against evasion, and ONLY for an attack */
        if (isAttack(source) && !opts.quiet && !opts.drain && !opts.reave
            && e.evasion > 0 && Js.R() > heroHitChance(e))
        {
            addHitNum(e.x, e.y - 40 * Js.Or(e.scale, 1), "MISS", "#c9b8e8", 14);
            clogRun.evaded++;
            logEventK("ev|" + (e.bname ?? e.type), (e.bname ?? e.type) + " slipped your swing", "#c9b8e8");
            addSparks(e.x, e.y - 18, 4, "#9b45d6");
            e.flash = 0.25;
            return;
        }
        /* BLOCK: a shield covers one side of you, not all of them */
        if (e.block > 0 && source != "bomb" && !opts.spin && !opts.quiet)
        {
            double fromAtt = kdir + Math.PI;
            if (Math.Abs(angDiff(e.faceA, fromAtt)) < BLOCK_ARC && Js.R() < e.block)
            {
                double px2 = e.x + Math.Cos(fromAtt) * 16 * e.scale, py2 = e.y - 20 * e.scale + Math.Sin(fromAtt) * 10 * e.scale;
                addSparks(px2, py2, 4, "#cfd6dd");
                addHitNum(e.x, e.y - 42 * e.scale, "BLOCKED", "#cfd6dd", 14);
                clogRun.blocked++;
                logEventK("bl|" + e.type, e.type + " blocked with its shield", "#cfd6dd");
                SFX.clank();
                e.kx += Math.Cos(kdir) * kb * 0.06; e.ky += Math.Sin(kdir) * kb * 0.06;
                e.flash = 0.3;
                return;
            }
        }
        /* the incoming number is already (base + flat) x increased x more */
        double d = Math.Max(0, dmg);
        bool crit = (player != null && player.noCrit) ? false
                  : opts.forceCrit ? true : (!opts.noCrit && Js.R() < critChance());
        if (crit) d *= critMult();
        if (e.magic) d *= 0.88;                      /* magic-touched: 12% less damage taken */
        bool suppressed = false;
        ConvSplit split = null;
        /* WHAT THE NUMBER IS MADE OF, as shares that add to one */
        NumMap mix = null;
        /* CONVERSION IS A PROPERTY OF THE HIT, not of the wound it leaves */
        bool lingering = opts.drain || opts.quiet;
        if (opts.noMit)
        {
            /* a wound already paid armour when it was opened; resistance still applies */
            var el0 = SOURCE_ELEM.GetValueOrDefault(source ?? "");
            if (el0 != null) d *= (1 - enemyRes(e, el0));
        }
        else if (lingering)
        {
            /* it still meets suppression and the monster's own resistance */
            if (convKind(source) == "spell")
            {
                if (Js.T(e.suppress) && Js.R() < e.suppress) { d *= SUPPRESS_TAKEN; suppressed = true; }
            }
            else d *= (1 - armourDR(armourVs(e, source), d));
            var el0 = SOURCE_ELEM.GetValueOrDefault(source ?? "");
            if (el0 != null) d *= (1 - enemyRes(e, el0));
            if (el0 == "fire") d *= burntMul(e);
        }
        else
        {
            /* HOW IT ARRIVED decides suppression or armour; WHAT IT IS decides resistance */
            bool asSpell = convKind(source) == "spell";
            if (asSpell && Js.T(e.suppress) && Js.R() < e.suppress) { d *= SUPPRESS_TAKEN; suppressed = true; }
            split = convSplit(source);
            d *= split.mul;                      /* what conversion did to the SIZE */
            double tot = 0; var by = new NumMap();
            foreach (var k in CONV_ORDER)
            {
                double shr = split.share[k];
                if (!(shr > 0)) continue;
                double part = d * shr;
                if (k == "phys" && !asSpell) part *= (1 - armourDR(armourVs(e, source), part));
                /* THE MONSTER'S OWN ELEMENTAL RESISTANCE; negative is a weakness */
                var el = CONV_RESIST.GetValueOrDefault(k);
                if (el != null) part *= (1 - penRes(e, el, source));
                /* a BURNT body takes every fire harder, whatever built the fire */
                if (k == "fire") part *= burntMul(e);
                by[k] = part;
                tot += part;
            }
            d = tot;
            if (tot > 0) { mix = new NumMap(); foreach (var (k, v) in by) if (v > 0) mix[k] = v / tot; }
        }
        /* SHOCK is a multiplier ON THE BODY, after mitigation */
        if (mix == null) mix = new NumMap((convFrom(source), 1));
        d *= shockMulOn(e);
        /* a breach lord's ring bites on its breach's bodies, and on nothing else */
        d *= ringMoreVs(e);
        /* A MONSTER'S OWN RAGE, if something gave it any */
        if (e.rageOnHurt > 0 && !opts.drain) stackAdd("rage", e.rageOnHurt, e);
        /* REAVING: a critical throws the axe */
        if (crit && !opts.quiet && !opts.drain && !opts.reave && isReaver()
            && source != null && MELEE_SOURCES.Contains(source)) throwReave(e, d * reaveMul());
        /* EVERY AILMENT, HERE, ONCE */
        if (!opts.quiet && !opts.drain && !opts.noAil)
        {
            applyAilments(e, d, source, mix, crit, opts);
            /* EXPOSURE IS NOT AN AILMENT, so it is its own call and its own chance */
            applyExposure(e, mix);
        }
        /* A POWER CHARGE IS EARNED BY CRITTING */
        if (crit && !opts.quiet && !opts.drain && player != null && player.chargeCrit > 0)
            stackAdd("power", player.chargeCrit);
        /* fractions survive all the way to the health bar; A DRAIN KEEPS ITS FRACTIONS */
        d = opts.drain ? Math.Max(0, d) : Math.Max(0.05, Math.Round(d * 100) / 100);
        /* the swing drinks off what ACTUALLY landed */
        if (source != null && LEECH_SOURCES.Contains(source) && !opts.quiet) leechFrom(d);
        /* THE SHIELD TAKES IT FIRST, and chaos walks through */
        if (e.esMax > 0)
        {
            double was = e.es;
            d = esAbsorb(e, d, null, chaosShareOf(source, mix));
            if ((was - e.es) > 0.01 && !opts.drain)
            {
                addSparks(e.x, e.y - 22 * Js.Or(e.scale, 1), 3, ES_COL);
                e.esFlash = 1;
            }
        }
        e.hp = opts.drain ? e.hp - d : Math.Round((e.hp - d) * 100) / 100;
        if (!opts.drain)
        {
            e.flash = 1;
            double kbm = (e.type == "brute" || e.type == "jugg" || e.type == "revenant" || e.type == "bastion") ? 0.3 : e.type == "boss" ? 0 : 1;
            e.kx += Math.Cos(kdir) * kb * kbm;
            e.ky += Math.Sin(kdir) * kb * kbm;
        }
        if (opts.drain)
        {
            /* nothing: the bar sliding IS the tell */
        }
        else if (opts.quiet)
        {
            /* a tick is a drip, not a blow */
            if (Js.R() < 0.4)
                addHitNum(e.x, e.y - 30 * e.scale, fmtHp(d), dmgColour(source, mix), 13);
        }
        else
        {
            /* THE NUMBER IS THE COLOUR OF WHAT LANDED, and a hit made of two things says TWO numbers */
            var parts = new List<HitPart>();
            foreach (var k in CONV_ORDER)
            {
                double shr = mix[k];
                if (!(shr > 0)) continue;
                double part = d * shr;
                if (part < DMG_SPLIT_MIN || shr < DMG_SPLIT_SHARE) continue;
                parts.Add(new HitPart { k = k, part = part });
            }
            Col lead = dmgColour(source, mix);
            addSparks(e.x, e.y - 18 * e.scale, 5, crit ? lead : (Col)"#fff2b0");
            if (parts.Count < 2)
            {
                addHitNum(e.x, e.y - 34 * e.scale, (suppressed ? "SUPPRESSED " : "") + (crit ? "CRIT " : "") + fmtHp(d),
                    lead, crit ? 23 : 16);
            }
            else
            {
                /* biggest first, each stacked a little above the last */
                parts.SortJs((a, b) => b.part - a.part);
                double shown = 0;
                foreach (var pt in parts) shown += pt.part;
                parts[0].part += d - shown;
                for (int i = 0; i < parts.Count; i++)
                {
                    var pt = parts[i];
                    addHitNum(e.x + (i != 0 ? (i % 2 != 0 ? 16 : -16) : 0), e.y - 34 * e.scale - i * 17,
                        (i == 0 ? (suppressed ? "SUPPRESSED " : "") + (crit ? "CRIT " : "") : "") + fmtHp(pt.part),
                        CONV_COL[pt.k], crit ? (i != 0 ? 19 : 23) : (i != 0 ? 14 : 16));
                }
            }
            addShake(crit ? 0.13 : 0.08);
            sfxAt(e.x).hit();
        }
        logDealt(e, d, source, crit, suppressed, mix);
        if (e.hp <= 0 && !legionHolds(e, source)) killEnemy(e, source);
    }
    /* LIFESTEAL: a chance, then a share, then the per-swing ceiling */
    public static void leechFrom(double d)
    {
        var p = player;
        if (p == null || p.dead || godMode) return;
        if (!(p.leech > 0) || !(p.leechChance > 0)) return;
        /* GHOST REAVER: the same steal, into the shield and never the hearts */
        if (p.ghostReaver)
        {
            if (!(p.esMax > 0) || p.es >= p.esMax) return;
            if (Js.R() >= p.leechChance) return;
            double gain = Math.Min(Math.Min(d * p.leech, p.esMax * LEECH_HIT_CAP), p.esMax - p.es);
            if (gain <= 0.005) return;
            p.es = Math.Round((p.es + gain) * 100) / 100;
            addHitNum(p.x, p.y - 58, "+" + fmtHp(gain), ES_COL, 14);
            clogRun.leeched = clogRun.leeched + gain;
            return;
        }
        if (p.hp >= p.maxHp) return;
        if (Js.R() >= p.leechChance) return;
        double heal = Math.Min(Math.Min(d * p.leech, p.maxHp * LEECH_HIT_CAP), p.maxHp - p.hp);
        if (heal <= 0.005) return;
        p.hp = Math.Round((p.hp + heal) * 100) / 100;
        addHitNum(p.x, p.y - 58, "+" + fmtHp(heal), LEECH_COL, 14);
        clogRun.leeched = clogRun.leeched + heal;
    }
    /* Lay poison on one body: a fresh dose refreshes the clock and takes the higher rate */
    public static double poisonCap(Ent e)
    {
        double b = e.type == "boss" ? POISON_MAX_BOSS : POISON_MAX;
        int rank = (int)(player != null && player.bane != null ? player.bane.burst : 0);
        return b * Js.Or(POISON_CAP_MUL[rank], 1);
    }
    public static void poisonEnemy(Ent e, double dps, double dur)
    {
        if (e.dead || e.state != "active") return;
        e.poison ??= new EPoison { n = 0, dps = 0, t = 0, tick = rnd(0.005, BANE_DRAIN_TICK) };
        var po = e.poison;
        double cap = poisonCap(e);
        if (po.n < cap) po.n++;
        /* the strongest dose sets the rate for every stack, and refreshes the clock */
        po.dps = Math.Max(po.dps, dps);
        po.t = Math.Max(po.t, dur);
    }
    /* ONE CONTACT, ONE STACK -- the dose goes on BEFORE the blow lands */
    public static void baneStrike(Ent e, double dmg, double dir, double kb, double dps, double dur, double stacks)
    {
        if (e.dead || e.state != "active") return;
        int n = Math.Max(1, Js.I32(stacks));
        for (int i = 0; i < n; i++) poisonEnemy(e, dps, dur);
        hitEnemy(e, dmg, dir, kb, "bane", new HitOpts { spin = true });
    }
    /* The odds this swing opens a wound: the one ailment table's physical row */
    public static double bleedChance(bool crit, string src = null) => ailChanceOf("phys", src, crit);
    /* how much more fire a branded body takes -- each BURNT mark is its own MORE */
    public static double burntMul(Ent e)
    {
        double n = (e.burnt != null && e.burnt.t > 0) ? e.burnt.n : 0;
        return Math.Pow(1 + BURNT_STEP, n);
    }
    public static double burntChance(double n)
    {
        if (n < EBURN_BRAND_AT) return 0;
        return Math.Min(1, BURNT_CHANCE + BURNT_CHANCE_PER * (n - EBURN_BRAND_AT));
    }
    public static double shockStacksOn(Ent e) => (Js.T(e.shockN) && e.shockT > 0) ? e.shockN : 0;
    public static double shockedMarks(Ent e) => (e != null && e.shocked != null && e.shocked.t > 0) ? e.shocked.n : 0;
    /* the stacks and the brand are separate MORE multipliers on the body */
    public static double shockedMul(Ent e) => Math.Pow(1 + SHOCKED_STEP, shockedMarks(e));
    public static double shockMulOn(Ent e) => (1 + ESHOCK_STEP * shockStacksOn(e)) * shockedMul(e);
    public static double shockedChance(double n)
    {
        if (n < ESHOCK_BRAND_AT) return 0;
        return Math.Min(1, SHOCKED_CHANCE + SHOCKED_CHANCE_PER * (n - ESHOCK_BRAND_AT));
    }
    public static void shockEnemy(Ent e, double n)
    {
        if (e == null || e.dead || e.state != "active") return;
        double before = shockStacksOn(e);
        e.shockN = Math.Min(ESHOCK_MAX, before + Math.Max(1, Js.Or(n, 1)));
        e.shockT = ESHOCK_T;
        if (e.shockN > before) addSparks(e.x, e.y - 18 * Js.Or(e.scale, 1), 2, "#ffe95a");
        /* AT THE THRESHOLD THE BOLT CAN BRAND -- and the brand does not eat the stacks */
        if (e.shockN >= ESHOCK_BRAND_AT && Js.R() < shockedChance(e.shockN))
        {
            e.shocked ??= new Mark();
            e.shocked.n = Math.Min(SHOCKED_MAX, e.shocked.n + 1);
            e.shocked.t = SHOCKED_T;
            addHitNum(e.x, e.y - 46 * Js.Or(e.scale, 1), "SHOCKED ×" + Js.Num(e.shocked.n), "#ffe95a", 15);
            addRing(e.x, e.y, 6, 180, 42, "#ffe95a", 4, 0.3);
            addSparks(e.x, e.y - 18 * Js.Or(e.scale, 1), 6, "#fff6a8");
        }
    }
    /* FROZEN SOLID, FROM WHEREVER IT CAME. What a frozen corpse is worth, and how far it reaches */
    public static double zeroShatterDmg(double marks) => frostDamage() * (1 + ZERO_BRITTLE_MUL * Math.Max(0, marks));
    public static double zeroShatterR() => (player != null && player.freeze != null ? player.freeze.r : 0) * ZERO_BURST_R_MUL;
    /* a shatter can kill, and a kill can shatter, so the recursion is bounded here */
    public static int zeroDepth = 0;
    public static bool zeroShatter(Ent e)
    {
        var p = player;
        if (p == null || p.freeze == null || !Js.T(p.freeze.stun) || !(p.freeze.n > 0)) return false;
        double marks = frozenMarks(e);
        if (marks <= 0 || zeroDepth >= ZERO_DEPTH) return false;
        zeroDepth++;
        try
        {
            double rr = zeroShatterR(), dmg = zeroShatterDmg(marks), FZ_T = 0.32;
            addRing(e.x, e.y, 12, (rr - 12) / FZ_T, rr, FX.friend.frostDeep, 7, FZ_T);
            addRing(e.x, e.y, 6, (rr * 0.6 - 6) / (FZ_T * 0.8), rr * 0.6, FX.friend.frost, 4, FZ_T * 0.8);
            addSparks(e.x, e.y - 16, (int)(8 + marks * 3), "#dff2ff");
            addHitNum(e.x, e.y - 52 * Js.Or(e.scale, 1), "SHATTER ×" + Js.Num(marks), "#dff2ff", 16);
            SFX.freeze();
            /* a straight scan, not the bucket grid */
            foreach (var e2 in enemies.ToList())
            {
                if (e2 == e || e2.dead || e2.state != "active") continue;
                if (dist(e2, e) > rr + hitR(e2)) continue;
                e2.slowT = Math.Max(e2.slowT, p.freeze.dur);
                freezeSolid(e2, true);
                hitEnemy(e2, dmg, angTo(e, e2), 30, "frost", new HitOpts { ailN = freezeChill() });
            }
        }
        finally { zeroDepth--; }
        return true;
    }
    public static void freezeSolid(Ent e, bool quiet = false)
    {
        if (e == null || e.dead || e.state != "active") return;
        e.frozen ??= new Frozen { n = 0, t = 0 };
        e.frozen.n = Math.Min(FROZEN_MAX, e.frozen.n + 1);
        e.frozen.t = FROZEN_T;
        if (e.type != "boss")
        {
            e.stunT = Math.Max(e.stunT, FREEZE_STUN_T);
            if (!quiet)
            {
                addHitNum(e.x, e.y - 46 * Js.Or(e.scale, 1), "FROZEN SOLID", "#dff2ff", 15);
                addSparks(e.x, e.y - 16, 7, "#ffffff");
            }
        }
        else if (!quiet)
        {
            addHitNum(e.x, e.y - 46 * Js.Or(e.scale, 1), "BRITTLE ×" + Js.Num(e.frozen.n), "#dff2ff", 15);
            addSparks(e.x, e.y - 16, 6, "#dff2ff");
        }
    }
    public static double chillStacksOn(Ent e) => (Js.T(e.chillN) && e.chillT > 0) ? e.chillN : 0;
    public static double chillMulOn(Ent e) => Math.Max(ECHILL_FLOOR, 1 - ECHILL_STEP * chillStacksOn(e));
    public static void chillEnemy(Ent e, double n)
    {
        if (e.dead || e.state != "active") return;
        for (int i = 0; i < Math.Max(1, Js.Or(n, 1)); i++)
        {
            e.chillN = chillStacksOn(e) + 1;
            e.chillT = ECHILL_T;
            if (e.chillN >= ECHILL_MAX)
            {
                /* THE FIFTH BREAKS: frozen solid, and the stacks go with it */
                e.chillN = 0; e.chillT = 0;
                freezeSolid(e);
            }
        }
        e.slowT = Math.Max(e.slowT, ECHILL_T);
    }
    public static double burnStacksOn(Ent e) => e.eburn != null ? e.eburn.list.Count : 0;
    public static double burnDpsOn(Ent e) { double d = 0; if (e.eburn != null) foreach (var f in e.eburn.list) d += f.dps; return d; }
    /* Light one. Every fire is its own stack with its own clock */
    public static void burnEnemy(Ent e, double hit, double depth = 0)
    {
        if (e.dead || e.state != "active" || !(hit > 0)) return;
        e.eburn ??= new DotList { tick = rnd(0.05, EBURN_TICK) };
        double dps = dotRate(hit * EBURN_SHARE, 0, false);
        var list = e.eburn.list;
        /* EMBER SCATTER triggers on a body that was ALREADY alight when this landed */
        bool wasLit = list.Count > 0;
        /* FIVE STACKS IS THE THRESHOLD -- and branding EATS five rather than the whole fire */
        if (list.Count >= EBURN_BRAND_AT && Js.R() < burntChance(list.Count))
        {
            list.Splice(0, (int)EBURN_BRAND_AT);
            e.burnt ??= new Mark();
            e.burnt.n = Math.Min(BURNT_MAX, e.burnt.n + 1);
            e.burnt.t = BURNT_T;
            addHitNum(e.x, e.y - 46 * Js.Or(e.scale, 1), "BURNT ×" + Js.Num(e.burnt.n), "#ff6a2a", 15);
            addRing(e.x, e.y, 6, 200, 46, "#ff8a3c", 4, 0.35);
            addSparks(e.x, e.y - 18, 7, "#ff9a3c");
        }
        if (list.Count >= burnCap())
        {
            /* at the cap the new fire replaces the feeblest one */
            int wi = 0;
            for (int i = 1; i < list.Count; i++) if (list[i].dps < list[wi].dps) wi = i;
            if (dps >= list[wi].dps) list[wi] = new Dot { dps = dps, t = EBURN_DUR };
            else list[wi].t = EBURN_DUR;
        }
        else list.Add(new Dot { dps = dps, t = EBURN_DUR });
        if (wasLit) emberScatter(e, hit, depth);
    }
    /* Knock the embers off a burning body */
    public static void emberScatter(Ent src, double hit, double depth)
    {
        int rank = (int)(player != null ? player.ember : 0);
        if (rank == 0 || depth >= EMBER_DEPTH) return;
        if (src.emberCd > 0) return;      /* one body is not a machine gun */
        src.emberCd = EMBER_CD;
        double n = EMBER_SHOTS[rank];
        /* one target CHOSEN PER EMBER, and each choice knows what the earlier ones took */
        var taken = new JsMap<Ent, double>();
        for (int i = 0; i < n; i++)
        {
            var tgt = emberTarget(src, src, taken);
            bool loop = tgt == null;                    /* nothing in reach: come home */
            if (tgt != null) taken[tgt] = taken.GetValueOrDefault(tgt) + 1;
            /* THE SPREAD IS NOT A FAN */
            double a = rnd(0, TAU);
            double spd = EMBER_SPD * rnd(0.28, 0.95);
            projectiles.Add(new Proj
            {
                kind = "ember", x = src.x + rnd(-7, 7), y = src.y - 16 + rnd(-8, 6),
                vx = Math.Cos(a) * spd, vy = Math.Sin(a) * spd,
                tgt = loop ? src : tgt, loop = loop, src = src,
                dmg = hit * EMBER_SHARE, depth = depth + 1,
                @out = (loop ? rnd(0.16, 0.40) : rnd(0.04, 0.26)) * rnd(0.75, 1.35),
                life = 2.2, seed = rnd(TAU)
            });
        }
        addSparks(src.x, src.y - 18, (int)(4 + n), "#ffb02e");
    }
    public static void burnTickEnemy(Ent e, double dt)
    {
        if (e.burnt != null) { e.burnt.t -= dt; if (e.burnt.t <= 0) e.burnt = null; }
        var bu = e.eburn;
        if (bu == null) return;
        double dps = 0; bool outd = false;
        foreach (var f in bu.list) { f.t -= dt; if (f.t > 0) dps += f.dps; else outd = true; }
        if (outd) bu.list = bu.list.Filter(f => f.t > 0);
        bu.tick -= dt;
        if (bu.tick <= 0 && dps > 0 && !e.dead && e.state == "active")
        {
            bu.tick = EBURN_TICK;
            hitEnemy(e, dps * EBURN_TICK, rnd(TAU), 0, "burn", new HitOpts { quiet = true, noCrit = true, noMit = true });
        }
        if (Js.R() < Math.Min(0.75, 0.18 + 0.12 * bu.list.Count))
            particles.Add(new Particle
            {
                kind = "spark", x = e.x + rnd(-9, 9), y = e.y - rnd(6, 30),
                vx = rnd(-12, 12), vy = rnd(-60, -20), life = rnd(0.25, 0.55), max = 0.55,
                color = Js.R() < 0.5 ? "#ff9a3c" : "#ffd166"
            });
        if (bu.list.Count == 0) e.eburn = null;
    }
    /* everything about the wounds currently open on a body, read off the list */
    static readonly List<Dot> NO_DOTS = new();
    public static List<Dot> bleedList(Ent e) => e.bleed != null ? e.bleed.list : NO_DOTS;
    public static double bleedStacks(Ent e) => bleedList(e).Count;
    public static double bleedDps(Ent e) { double d = 0; foreach (var w in bleedList(e)) d += w.dps; return d; }
    /* Cut one. Every wound is its own, and they all bite at once */
    public static void bleedEnemy(Ent e, double hit)
    {
        if (e.dead || e.state != "active" || !(hit > 0)) return;
        double dps = dotRate(hit * (player != null ? Js.Or(player.bleedShare, BLEED_SHARE_BASE) : BLEED_SHARE_BASE), 0, false);
        e.bleed ??= new DotList { tick = rnd(0.05, BLEED_TICK) };
        var list = e.bleed.list;
        if (list.Count < BLEED_STACK_MAX)
        {
            list.Add(new Dot { dps = dps, t = BLEED_T });
        }
        else
        {
            /* full: the new cut takes the SHALLOWEST wound's place, if it is deeper */
            int w = 0;
            for (int i = 1; i < list.Count; i++) if (list[i].dps < list[w].dps) w = i;
            if (dps <= list[w].dps)
            {
                addSparks(e.x, e.y - 22 * Js.Or(e.scale, 1), 1, "#6b2a2f");   /* it glanced off */
                return;
            }
            list[w] = new Dot { dps = dps, t = BLEED_T };
        }
        addSparks(e.x, e.y - 22 * Js.Or(e.scale, 1), 3, "#d2313c");
    }
    /* what one tick of every open wound takes off a body */
    public static void bleedTick(Ent e, double dt)
    {
        var bl = e.bleed;
        if (bl == null) return;
        double dps = 0; bool closed = false;
        foreach (var w in bl.list)
        {
            w.t -= dt;
            if (w.t > 0) dps += w.dps; else closed = true;
        }
        if (closed) bl.list = bl.list.Filter(w => w.t > 0);
        bl.tick -= dt;
        if (bl.tick <= 0 && dps > 0 && !e.dead && e.state == "active")
        {
            bl.tick = BLEED_TICK;
            /* a body that has stopped moving is not tearing its own wounds open */
            bool still = e.spdNow < BLEED_STILL_SPD;
            double mul = still ? (1 - BLEED_STILL_LESS) : 1;
            hitEnemy(e, dps * mul * BLEED_TICK, rnd(TAU), 0, "bleed", new HitOpts { quiet = true, noCrit = true, noMit = true });
        }
        if (Js.R() < Math.Min(0.8, 0.2 + 0.08 * bl.list.Count))
            particles.Add(new Particle
            {
                kind = "spark", x = e.x + rnd(-9, 9), y = e.y - rnd(8, 32),
                vx = rnd(-10, 10), vy = rnd(14, 46), life = rnd(0.25, 0.5), max = 0.5, color = "#c8202a"
            });
        if (bl.list.Count == 0) e.bleed = null;
    }
    /* what one tick of poison takes off a body */
    public static void poisonTick(Ent e, double dt)
    {
        var po = e.poison;
        if (po == null) return;
        po.t -= dt;
        po.tick -= dt;
        if (po.tick <= 0 && !e.dead && e.state == "active")
        {
            /* whatever time actually elapsed is what gets drained */
            double step = BANE_DRAIN_TICK - po.tick;
            po.tick = BANE_DRAIN_TICK;
            /* every stack bites at once */
            hitEnemy(e, po.dps * po.n * step, rnd(TAU), 0, "bane", new HitOpts { drain = true, noCrit = true });
        }
        if (Js.R() < 0.18 + 0.04 * po.n)
            particles.Add(new Particle
            {
                kind = "spark", x = e.x + rnd(-10, 10), y = e.y - rnd(6, 34),
                vx = rnd(-12, 12), vy = rnd(-34, -8), life = rnd(0.3, 0.6), max = 0.6, color = "#8ede4a"
            });
        if (po.t <= 0) e.poison = null;
    }
}
