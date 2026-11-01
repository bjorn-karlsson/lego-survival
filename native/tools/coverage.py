#!/usr/bin/env python3
"""Is anything from the browser game missing in the native port?

Lists every top-level declaration in lego-survival.html's script (functions,
consts, lets) and looks for the same name declared in native/src/Game/*.cs.
A name that is deliberately not carried over (DOM wiring the native UI replaces,
browser-only plumbing) is listed in tools/coverage-map.txt with the reason:

    jsName -> csName              ported under another name
    jsName -- reason              not needed natively, and why

Run:  python3 native/tools/coverage.py [--missing] [--section NAME]
"""
import os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
JS = os.path.join(ROOT, 'lego-survival.html')
CS = os.path.join(ROOT, 'native', 'src')
MAP = os.path.join(HERE, 'coverage-map.txt')


def split_top(s):
    """split a declarator list at commas that are not inside brackets/strings"""
    out, depth, cur, q = [], 0, '', None
    i = 0
    while i < len(s):
        c = s[i]
        if q:
            cur += c
            if c == '\\' and i + 1 < len(s):
                cur += s[i + 1]; i += 2; continue
            if c == q: q = None
        elif c in '\'"`':
            q = c; cur += c
        elif c in '([{':
            depth += 1; cur += c
        elif c in ')]}':
            depth -= 1; cur += c
        elif c == ',' and depth == 0:
            out.append(cur); cur = ''
        elif c == ';' and depth == 0:
            break
        else:
            cur += c
        i += 1
    if cur.strip(): out.append(cur)
    return out


def js_decls():
    lines = open(JS, encoding='utf-8').read().split('\n')
    start = next(i for i, l in enumerate(lines) if l.strip() == '<script>')
    end = max(i for i, l in enumerate(lines) if l.strip() == '</script>')
    section = 'PRELUDE'
    decls = []
    for i in range(start + 1, end):
        l = lines[i]
        m = re.match(r'^/\* =+ (.+?) =+ \*/', l)
        if m:
            section = m.group(1).strip(); continue
        m = re.match(r'^(?:async\s+)?function\s*\*?\s*([A-Za-z_$][\w$]*)\s*\(', l)
        if m:
            decls.append((m.group(1), 'function', i + 1, section)); continue
        m = re.match(r'^(const|let|var)\s+(.*)$', l)
        if m:
            for d in split_top(m.group(2)):
                d = d.strip()
                n = re.match(r'^([A-Za-z_$][\w$]*)', d)
                if n: decls.append((n.group(1), m.group(1), i + 1, section))
                elif d.startswith('{') or d.startswith('['):
                    for nm in re.findall(r'([A-Za-z_$][\w$]*)\s*(?:[,}\]=]|$)', d.split('=')[0]):
                        decls.append((nm, m.group(1), i + 1, section))
    return decls


def cs_names():
    names = set()
    pat = re.compile(r'\b(?:static|const|readonly|public|internal|private)\b[^;=(){}\n]*?\b([A-Za-z_]\w*)\s*(?:=|;|\(|\{|=>)')
    for root, _, files in os.walk(CS):
        for f in files:
            if not f.endswith('.cs'): continue
            src = open(os.path.join(root, f), encoding='utf-8').read()
            for m in pat.finditer(src):
                names.add(m.group(1))
            # several fields on one line: public static double cw = 0, ch = 0;
            # (and consts: public const double A = 1, B = 2;) -- split at the
            # top-level commas, so an initializer's own braces do not end it
            for m in re.finditer(r'\b(?:const|static)\s+(?:readonly\s+)?[\w.?\[\]]+(?:<[^;={}]*?>)?(?:\[\])?\??\s+(?=[A-Za-z_]\w*\s*(?:=[^=>]|,|;))', src):
                for part in split_top(src[m.end():]):
                    n = re.match(r'\s*([A-Za-z_]\w*)', part)
                    if n: names.add(n.group(1))
            # a tuple-typed field: static readonly (string a, string b) NAME = ...
            for m in re.finditer(r'\bstatic\s+(?:readonly\s+)?\([^;={}]*?\)(?:\[\])?\s+([A-Za-z_]\w*)\s*=', src):
                names.add(m.group(1))
            # a generic method: static T name<T>(...)
            for m in re.finditer(r'\bstatic\s+[\w<>\[\],?]+\s+([A-Za-z_]\w*)<\w+(?:\s*,\s*\w+)*>\s*\(', src):
                names.add(m.group(1))
            # a method with a tuple or nested-generic return type
            for m in re.finditer(r'\bstatic\s[^\n;{=]*?\s([A-Za-z_]\w*)\s*\(', src):
                names.add(m.group(1))
            # the port's own marker: // js: name1, name2
            for m in re.finditer(r'//\s*js:\s*([\w$, ]+)', src):
                for n in m.group(1).split(','): names.add(n.strip())
    return names


def mapping():
    mp = {}
    if not os.path.exists(MAP): return mp
    for l in open(MAP, encoding='utf-8'):
        l = l.split('#', 1)[0].rstrip()
        if not l.strip(): continue
        m = re.match(r'^\s*([\w$]+)\s*(->|--)\s*(.*)$', l)
        if m: mp[m.group(1)] = (m.group(2), m.group(3).strip())
    return mp


def main():
    decls = js_decls()
    have = cs_names()
    mp = mapping()
    by_sec = {}
    missing = []
    seen = set()
    for name, kind, line, sec in decls:
        key = (name, line)
        if key in seen: continue
        seen.add(key)
        st = 'ok' if name in have else 'map' if name in mp and (mp[name][0] == '--' or mp[name][1] in have) else 'missing'
        by_sec.setdefault(sec, [0, 0, 0])
        by_sec[sec][['ok', 'map', 'missing'].index(st)] += 1
        if st == 'missing': missing.append((name, kind, line, sec))
    total = len(seen)
    done = total - len(missing)
    print(f'JS top-level declarations: {total}   carried over: {done}   missing: {len(missing)}   ({done * 100 / max(1, total):.1f}%)')
    print()
    print(f'{"section":<42} {"ported":>7} {"mapped":>7} {"missing":>8}')
    for sec, (a, b, c) in by_sec.items():
        print(f'{sec[:42]:<42} {a:>7} {b:>7} {c:>8}')
    if '--missing' in sys.argv:
        only = sys.argv[sys.argv.index('--section') + 1] if '--section' in sys.argv else None
        print()
        for name, kind, line, sec in missing:
            if only and only.lower() not in sec.lower(): continue
            print(f'  {line:>6}  {kind:<8} {name:<36} [{sec}]')
    return 0 if not missing else 1


if __name__ == '__main__':
    sys.exit(main())
