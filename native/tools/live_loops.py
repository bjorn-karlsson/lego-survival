#!/usr/bin/env python3
"""JS for-of over an array walks it BY INDEX: an element pushed during the loop is
reached, and nothing throws. A C# foreach over a List throws the moment the body
adds to it or removes from it -- and the game's bodies do (a hit that kills spawns
the splits, a burst spawns particles). Every foreach over one of the game's live
lists, or over enemiesNear's shared buffer, walks it with .Live() instead.

Run:  python3 native/tools/live_loops.py [--check]   (--check: list, change nothing)
"""
import os, re, sys
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LIVE = ('enemies projectiles props solids minions hazards particles pickups corpses gearDrops debris '
        'dmgTexts sched spikes pspikes bloodOrbs rmCores sboxPending breachExtra abyssExtra pendingPicks '
        'waters flowers choices buildLog waveMods').split()
CALLS = ['enemiesNear']
head = re.compile(r'foreach\s*\(\s*(?:var\s+(?:\([^)]*\)|\w+)|[\w<>,\[\] ]+\s+\w+)\s+in\s+')
def balanced(s, i):
    """the end of a call's argument list starting at s[i] == '('"""
    d = 0
    for j in range(i, len(s)):
        if s[j] == '(': d += 1
        elif s[j] == ')':
            d -= 1
            if d == 0: return j
    return -1
changed = 0; hits = []
check = '--check' in sys.argv
gdir = os.path.join(root, 'src', 'Game')
for f in sorted(os.listdir(gdir)):
    if not f.endswith('.cs'): continue
    p = os.path.join(gdir, f); s = open(p, encoding='utf-8').read(); out = []; pos = 0
    for m in head.finditer(s):
        k = m.end()
        name = re.match(r'[A-Za-z_]\w*', s[k:])
        if not name: continue
        nm = name.group(0); e = k + len(nm)
        if nm in LIVE and s[e] == ')':
            out.append(s[pos:e]); out.append('.Live()'); pos = e
            hits.append(f'{f}:{s.count(chr(10), 0, k) + 1} {nm}')
        elif nm in CALLS and s[e] == '(':
            z = balanced(s, e)
            if z > 0 and s[z + 1] == ')':
                out.append(s[pos:z + 1]); out.append('.Live()'); pos = z + 1
                hits.append(f'{f}:{s.count(chr(10), 0, k) + 1} {nm}(...)')
    out.append(s[pos:])
    t = ''.join(out)
    if t != s and not check: open(p, 'w', encoding='utf-8').write(t); changed += 1
print('\n'.join(hits) if check else f'{len(hits)} loops in {changed} files')
