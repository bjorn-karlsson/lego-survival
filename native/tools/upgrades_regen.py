import re, subprocess
subprocess.run("python3 tools/js2cs.py 11594 12665 --obj 'calc=Calc,[]=Upgrade' --arity desc=_owned > /tmp/js2cs_upg.cs", shell=True, check=True)
p='src/Game/Upgrades.cs'; s=open(p).read()
i=s.index('    public static readonly List<Upgrade> UPGRADES = [')
head=s[:i]
upg=open('/tmp/js2cs_upg.cs').read()
R=[
('var UPGRADES = [','public static readonly List<Upgrade> UPGRADES = ['),
('icon = (new /*?*/ { fire = "flame", frost = "icecube", shock = "chain", chaos = "bane" })[el]','icon = new Dictionary<string, string> { ["fire"] = "flame", ["frost"] = "icecube", ["shock"] = "chain", ["chaos"] = "bane" }[el]'),
('''  ..[["fire", "ignitechance", "Kindling", "flame", "IGNITE", "fire"],
      ["frost", "chillchance", "Numbing Cold", "snow", "CHILL", "cold"],
      ["shock", "shockchance", "Static Charge", "bolt", "SHOCK", "lightning"],
      ["chaos", "poisonchance", "Venom Glands", "bane", "POISON", "chaos"]].map(([t, id, name, icon, AIL, dmg])=>(new Upgrade {''','''  ..new[] { ("fire", "ignitechance", "Kindling", "flame", "IGNITE", "fire"),
      ("frost", "chillchance", "Numbing Cold", "snow", "CHILL", "cold"),
      ("shock", "shockchance", "Static Charge", "bolt", "SHOCK", "lightning"),
      ("chaos", "poisonchance", "Venom Glands", "bane", "POISON", "chaos") }.map(row => { var (t, id, name, icon, AIL, dmg) = row; return new Upgrade {'''),
('apply = c=>{ player.ailTypeFlat[t] = player.ailTypeFlat[t] + c.v; syncStats(); } })),','apply = c=>{ player.ailTypeFlat[t] = player.ailTypeFlat[t] + c.v; syncStats(); } }; }),'),
('''    req = ()=>new[] {["frenzy","chargeKill"],["power","chargeCrit"],["endurance","chargeHurt"]}
            .some(([k, g])=>player[g] > 0 && stackAllows(k) && stackCap(k) < CHARGE_CAP_MAX),''','''    req = ()=>new[] { ("frenzy","chargeKill"), ("power","chargeCrit"), ("endurance","chargeHurt") }
            .some(kg => Fields.Num(player, kg.Item2) > 0 && stackAllows(kg.Item1) && stackCap(kg.Item1) < CHARGE_CAP_MAX),'''),
('double M = player.minions, add = Math.min(c.n, MINION_VOLLEY_MAX - M.volleys);','var M = player.minions; double add = Math.min(c.n, MINION_VOLLEY_MAX - M.volleys);'),
('var M = player.minions; M.gmp = true;','var M = player.minions; M.gmp = true;'),
('!player.orbit.aegis','!Js.T(player.orbit.aegis)'),
('&& player.orbit.aegis &&','&& Js.T(player.orbit.aegis) &&'),
('!player.orbit.twin','!Js.T(player.orbit.twin)'),
('!player.freeze.stun','!Js.T(player.freeze.stun)'),
('!player.bow.kind','player.bow.kind == null'),
('(was ? $" (was','(Js.T(was) ? $" (was'),
('+ ((r=>$" And always:','+ (((Func<double, string>)(r=>$" And always:'),
('your blade grows with the reach")(Math.min(WHIRL_MAX, player.blademaster+1))),','your blade grows with the reach"))(Math.min(WHIRL_MAX, player.blademaster+1))),'),
]
for a,b in R:
    if a not in upg: print('MISSING:', a[:70])
    upg=upg.replace(a,b)
def indent(t, n=4):
    return '\n'.join((' '*n + l) if l.strip() else l for l in t.split('\n'))
open(p,'w').write(head + indent(upg.rstrip()) + '\n}\n')
