#!/usr/bin/env python3
"""A first pass of JS -> C# for the port: the mechanical part, so the hand part
is only the part that needs thought.

    python3 native/tools/js2cs.py FROM TO [--obj KEY=Class,...] > out.cs

What it does, token by token (strings and comments are never touched inside):
  * 'single' and "double" strings -> C# "strings"; \\u{1F525} escapes -> the character
  * `template ${literals}` -> $"template {S(literals)}" -- S() formats a number
    the way String(n) does (see Engine/JsExt.cs)
  * === !== -> == !=     const/let -> var     undefined -> null
  * .length -> .Count()  (works for strings, lists and arrays alike)
  * an integer literal on either side of / becomes a double (JS has no integer
    division; C# does)
  * object literals -> `new Class { key = value }` where the class is known from
    the key the literal is the value of (--obj calc=Calc,=Upgrade: '' is an array
    element at the top); anything else -> `new /*?*/ { ... }` to fix by hand
It does NOT type functions, fix `||` on numbers, or know a NumMap field from a
class field: the compiler finds those, and they are fixed by hand.
"""
import re, sys

JS = __file__.rsplit('/native/', 1)[0] + '/lego-survival.html'


def tokenize(s):
    toks, i, n = [], 0, len(s)
    while i < n:
        c = s[i]
        if s.startswith('/*', i):
            j = s.find('*/', i + 2); j = n if j < 0 else j + 2
            toks.append(('com', s[i:j])); i = j; continue
        if s.startswith('//', i):
            j = s.find('\n', i); j = n if j < 0 else j
            toks.append(('com', s[i:j])); i = j; continue
        if c in '\'"':
            j = i + 1
            while j < n and s[j] != c:
                j += 2 if s[j] == '\\' else 1
            toks.append(('str', s[i:j + 1])); i = j + 1; continue
        if c == '`':
            j, depth, parts, cur = i + 1, 0, [], ''
            while j < n:
                if s[j] == '\\':
                    cur += s[j:j + 2]; j += 2; continue
                if s[j] == '`':
                    break
                if s.startswith('${', j):
                    parts.append(('t', cur)); cur = ''
                    k, d = j + 2, 1
                    while k < n and d:
                        if s[k] == '{': d += 1
                        elif s[k] == '}': d -= 1
                        elif s[k] in '\'"`':
                            q = s[k]; k += 1
                            while k < n and s[k] != q:
                                k += 2 if s[k] == '\\' else 1
                        k += 1
                    parts.append(('e', s[j + 2:k - 1])); j = k; continue
                cur += s[j]; j += 1
            parts.append(('t', cur))
            toks.append(('tpl', parts)); i = j + 1; continue
        if c.isspace():
            j = i
            while j < n and s[j].isspace(): j += 1
            toks.append(('ws', s[i:j])); i = j; continue
        if c.isalpha() or c in '_$':
            j = i
            while j < n and (s[j].isalnum() or s[j] in '_$'): j += 1
            toks.append(('id', s[i:j])); i = j; continue
        if c.isdigit() or (c == '.' and i + 1 < n and s[i + 1].isdigit()):
            m = re.match(r'0x[0-9a-fA-F]+|\d*\.?\d+(?:[eE][-+]?\d+)?', s[i:])
            toks.append(('num', m.group(0))); i += len(m.group(0)); continue
        for op in ('===', '!==', '...', '=>', '==', '!=', '<=', '>=', '&&', '||', '??', '?.', '++', '--',
                   '+=', '-=', '*=', '/=', '**', '<<', '>>'):
            if s.startswith(op, i):
                toks.append(('op', op)); i += len(op); break
        else:
            toks.append(('op', c)); i += 1
    return toks


DICTS = {'ARROW_KINDS', 'HERO_STACKS', 'WEAPONS', 'RARITY', 'BOSSES', 'SUPERBOSSES', 'EDEF', 'GOLEM_CONF', 'CURSES',
         'BREACH_LORDS', 'SBOX_TIERS', 'GEAR_RAR', 'GEAR_CLS', 'META_MASTERY', 'GOLEM_TYPES', 'DIFFS', 'GAME_MODES',
         'ORB_KINDS', 'STUD_TIERS', 'LEGION_FACTIONS', 'BIOMES', 'META_STATS', 'GEAR_XSTATS', 'MINION_KINDS', 'FAV_PERKS',
         'TRACKS', 'BRAND_NAME', 'BRAND_COL', 'CONV_COL', 'CONV_NAME', 'ELEM_COL', 'DOT_COL', 'SPELL_NAME', 'PEN_NAME',
         'META_BY_ID', 'RARE_MOD_BY_ID', 'GEAR_BASE_BY_ID', 'GEAR_AFFIX_BY_ID', 'MODE_W', 'SHOT_SHARE', 'REJECT_STUDS',
         'CLS_COL', 'ICONS', 'OPT', 'SFX_SET', 'RPG_ZONES', 'RPG_USES', 'LOOT_PIP', 'ATTR_COL', 'MONSTER_COST'}
NUMMAP = {'mods', 'inc', 'attr', 'res', 'resFlat', 'pen', 'curse', 'curseN', 'capUp', 'brand', 'brandT', 'ailCd',
          'ailType', 'ailTypeFlat', 'stackMax', 'splinters', 'rings', 'buffs', 'expose'}


def cs_string(body, quote):
    """the inside of a JS string literal, as the inside of a C# one"""
    out, i = '', 0
    while i < len(body):
        ch = body[i]
        if ch == '\\':
            nx = body[i + 1] if i + 1 < len(body) else ''
            if nx == 'u' and body[i + 2:i + 3] == '{':
                j = body.index('}', i)
                out += chr(int(body[i + 3:j], 16)); i = j + 1; continue
            if nx == "'": out += "'"; i += 2; continue
            if nx == '`': out += '`'; i += 2; continue
            if nx == '$': out += '$'; i += 2; continue
            if nx == '\n': i += 2; continue
            out += ch + nx; i += 2; continue
        if ch == '"': out += '\\"'
        elif ch == '\n': out += '\\n'
        else: out += ch
        i += 1
    return out


class Conv:
    def __init__(self, objmap, arity=None):
        self.objmap = objmap
        self.arity = arity or {}

    def expr(self, src):
        return self.run(tokenize(src))

    def run(self, toks):
        out = []
        prev = None          # previous significant token
        keystack = []        # for each open brace or bracket: ('obj', class), ('blk',), ('arr',), ('idx',)
        pending_key = ''     # the object key whose value we are in
        i = 0
        while i < len(toks):
            k, v = toks[i]
            if k == 'ws' or k == 'com':
                out.append(v); i += 1; continue
            if k == 'str':
                out.append('"' + cs_string(v[1:-1], v[0]) + '"')
            elif k == 'tpl':
                s = '$"'
                for pk, pv in v:
                    if pk == 't':
                        s += cs_string(pv, '`').replace('{', '{{').replace('}', '}}')
                    else:
                        s += '{S(' + self.expr(pv).strip() + ')}'
                out.append(s + '"')
            elif k == 'num':
                nxt = self.peek(toks, i + 1)
                if re.fullmatch(r'\d+', v) and ((prev and prev[1] == '/') or (nxt and nxt[1] == '/')):
                    v = v + '.0'
                out.append(v)
            elif k == 'id':
                if v in ('const', 'let'): v = 'var'
                elif v == 'undefined': v = 'null'
                elif v in DICTS and (not prev or prev[1] != '.') and self.peek(toks, i + 1) and self.peek(toks, i + 1)[1] == '.':
                    j = i + 1
                    while toks[j][0] == 'ws': j += 1
                    j += 1
                    while toks[j][0] == 'ws': j += 1
                    if toks[j][0] == 'id':
                        out.append(v + '["' + toks[j][1] + '"]'); prev = ('id', toks[j][1]); i = j + 1; continue
                elif v in NUMMAP and prev and prev[1] == '.' and self.peek(toks, i + 1) and self.peek(toks, i + 1)[1] == '.':
                    # p.inc.atk -> p.inc["atk"]
                    j = i + 1
                    while toks[j][0] == 'ws': j += 1
                    j += 1
                    while toks[j][0] == 'ws': j += 1
                    if toks[j][0] == 'id':
                        out.append(v + '["' + toks[j][1] + '"]'); prev = ('id', toks[j][1]); i = j + 1; continue
                elif v == 'length' and prev and prev[1] == '.':
                    nxt = self.peek(toks, i + 1)
                    if not (nxt and nxt[1] == '='): v = 'Count()'
                out.append(v)
            elif k == 'op':
                if v == '===': v = '=='
                elif v == '!==': v = '!='
                elif v == '...': v = '..'
                elif v == '||':
                    nxt = self.peek(toks, i + 1)
                    if nxt and nxt[0] in ('str', 'tpl'): v = '??'
                if v == '{':
                    is_obj = prev is None or prev[1] in ('(', '[', ',', ':', '=', '?', 'return', '||', '&&', '??', '...') or prev[1] == '=>' and False
                    if prev and prev[1] == ':' and keystack and keystack[-1][0] == 'blk':
                        is_obj = True
                    if is_obj:
                        cls = self.objmap.get(pending_key)
                        if not cls and keystack and keystack[-1][0] == 'arr' and len([q for q in keystack if q[0] == 'arr']) == 1:
                            cls = self.objmap.get('[]')
                        keystack.append(('obj', cls))
                        out.append(('new ' + cls + ' {') if cls else 'new /*?*/ {')
                        pending_key = None
                        prev = (k, v); i += 1
                        # object key position follows
                        i = self.obj_key(toks, i, out)
                        continue
                    keystack.append(('blk',))
                    out.append(v)
                elif v == '}':
                    if keystack: keystack.pop()
                    out.append(v)
                elif v == ']':
                    top = keystack.pop() if keystack else ('idx',)
                    if top[0] == 'arr':
                        nxt = self.peek(toks, i + 1)
                        if nxt and nxt[1] in ('.', '['):
                            out[top[1]] = 'new[] {'; out.append('}')
                        else:
                            out.append(']')
                    else:
                        out.append(']')
                elif v == ',' and keystack and keystack[-1][0] == 'obj':
                    out.append(v)
                    prev = (k, v); i += 1
                    i = self.obj_key(toks, i, out)
                    continue
                elif v == '[':
                    if prev is None or prev[1] in ('(', '[', ',', ':', '=', 'return', '?', '=>', '{', '...', '||', '&&', '??'):
                        keystack.append(('arr', len(out))); out.append('[')
                    else:
                        keystack.append(('idx',)); out.append('[')
                else:
                    out.append(v)
            prev = (k, v)
            i += 1
        return ''.join(out)

    def peek(self, toks, i):
        while i < len(toks) and toks[i][0] in ('ws', 'com'): i += 1
        return toks[i] if i < len(toks) else None

    def obj_key(self, toks, i, out):
        """at the start of an object member: `key:` -> `key =`, remembering the key"""
        j = i
        while j < len(toks) and toks[j][0] in ('ws', 'com'):
            out.append(toks[j][1]); j += 1
        if j >= len(toks): return j
        k, v = toks[j]
        if k in ('id', 'str', 'num'):
            key = v.strip('\'"')
            nxt = j + 1
            while nxt < len(toks) and toks[nxt][0] == 'ws': nxt += 1
            if nxt < len(toks) and toks[nxt][1] == ':':
                out.append(key + ' = ')
                while nxt + 1 < len(toks) and toks[nxt + 1][0] == 'ws': nxt += 1
                if key in self.arity:
                    # `c=>` where the field is a two-argument delegate: `(c, owned)=>`
                    a1 = self.peek(toks, nxt + 1)
                    j1 = nxt + 1
                    while toks[j1][0] == 'ws': j1 += 1
                    if toks[j1][0] == 'id':
                        j2 = j1 + 1
                        while toks[j2][0] == 'ws': j2 += 1
                        if toks[j2][1] == '=>':
                            toks[j1] = ('id', '(' + toks[j1][1] + ', ' + self.arity[key] + ')')
                self.cur_key = key
                # the value: remember the key for an object literal inside it
                self.objmap_key = key
                self_pending = key
                # emulate: set pending key on the next run step by inserting a marker
                out.append('')
                self._next_key = key
                rest = nxt + 1
                # translate the value span up to the next top-level ',' or '}' of this object
                return self.value(toks, rest, out, key)
            if nxt < len(toks) and toks[nxt][1] in (',', '}'):
                out.append(key + ' = ' + key)
                return nxt
        return j

    def value(self, toks, i, out, key):
        depth, j = 0, i
        start = i
        while j < len(toks):
            v = toks[j][1]
            if toks[j][0] == 'op':
                if v in '([{': depth += 1
                elif v in ')]}':
                    if depth == 0: break
                    depth -= 1
                elif v == ',' and depth == 0: break
            j += 1
        sub = toks[start:j]
        save = dict(self.objmap)
        out.append(self.run_with_key(sub, key))
        return j

    def run_with_key(self, sub, key):
        # an object literal directly in this value -- possibly behind an arrow and a paren --
        # takes the class named for this key
        cls = self.objmap.get(key)
        s = self.run(sub)
        if cls:
            s = s.replace('new /*?*/ {', 'new ' + cls + ' {', 1)
        return s


def main():
    a = sys.argv[1:]
    lo, hi = int(a[0]), int(a[1])
    objmap = {}
    if '--obj' in a:
        for kv in a[a.index('--obj') + 1].split(','):
            k2, v2 = kv.split('=')
            objmap[k2] = v2
    lines = open(JS, encoding='utf-8').read().split('\n')[lo - 1:hi]
    src = '\n'.join(lines)
    arity = {}
    if '--arity' in a:
        for kv in a[a.index('--arity') + 1].split(','):
            k2, v2 = kv.split('=')
            arity[k2] = v2
    c = Conv(objmap, arity)
    s = c.expr(src)
    s = re.sub(r'for\s*\(\s*var\s+(\w+)\s+of\s+', r'foreach (var \1 in ', s)
    # (x||0) on a number is x: a field that was never set already reads 0
    s = re.sub(r'(?<![\w\]\)])\(\s*([\w.\[\]"]+)\s*\|\|\s*0\s*\)', r'\1', s)
    s = re.sub(r'([\w.\[\]"]+(?:\(\))?)\s*\|\|\s*(\d+(?:\.\d+)?)\b', r'Js.Or(\1, \2)', s)
    # var a = 1, b = 2;  ->  double a = 1, b = 2;
    def multi(m):
        body = m.group(2)
        depth = 0; top = 0
        for ch in body:
            if ch in '([{': depth += 1
            elif ch in ')]}': depth -= 1
            elif ch == ',' and depth == 0: top += 1
        return m.group(1) + ('double ' if top else 'var ') + body + ';'
    s = re.sub(r'(^|[;{]\s*|\n\s*)var ([^;]*);', multi, s)
    s = s.replace('dealtTypes().phys', 'dealtTypes()["phys"]')
    s = re.sub(r'for\s*\(\s*var\s+\[([\w, ]+)\]\s+of\s+', r'foreach (var (\1) in ', s)
    sys.stdout.write(s + '\n')


if __name__ == '__main__':
    main()
