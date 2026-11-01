#!/usr/bin/env python3
"""Second pass over js2cs output: JS array literals in the places C# cannot
target-type a collection expression (the arms of ?:, the operand of a spread)
become `new T[] { ... }`, `[]` there becomes `new T[0]`, and `...x` becomes `..x`.

    python3 native/tools/cs_arrays.py IN OUT [--elem string]
"""
import sys

def scan_string(s, i):
    """s[i] is the opening quote (after any $); return index after the closing quote"""
    interp = i > 0 and s[i - 1] == '$'
    j = i + 1
    while j < len(s):
        c = s[j]
        if c == '\\': j += 2; continue
        if c == '"': return j + 1
        if interp and c == '{':
            if j + 1 < len(s) and s[j + 1] == '{': j += 2; continue
            j = scan_code(s, j + 1, '}')
            continue
        j += 1
    return j

def scan_code(s, i, close):
    """scan code until the matching `close`; return the index after it"""
    depth = 0
    while i < len(s):
        c = s[i]
        if c == '"': i = scan_string(s, i); continue
        if c == "'" and i + 2 < len(s) and s[i + 2] == "'": i += 3; continue
        if s.startswith('/*', i): j = s.find('*/', i + 2); i = len(s) if j < 0 else j + 2; continue
        if c in '([{': depth += 1
        elif c in ')]}':
            if depth == 0 and c == close: return i + 1
            depth -= 1
        i += 1
    return i

def convert(s, elem):
    out = []
    i = 0
    n = len(s)
    def prev_sig(buf):
        k = len(buf) - 1
        while k >= 0 and buf[k] in ' \t\r\n': k -= 1
        # skip a trailing comment
        return buf[k] if k >= 0 else ''
    buf = ''
    while i < n:
        c = s[i]
        if c == '"':
            j = scan_string(s, i)
            # recursively convert inside interpolation holes
            buf += s[i:j]; i = j; continue
        if s.startswith('/*', i):
            j = s.find('*/', i + 2); j = n if j < 0 else j + 2
            buf += s[i:j]; i = j; continue
        if s.startswith('...', i):
            buf += '..'; i += 3; continue
        if c == '[':
            p = prev_sig(buf)
            if p in '?:':
                j = scan_code(s, i + 1, ']')
                inner = s[i + 1:j - 1]
                if inner.strip() == '':
                    buf += f'new {elem}[0]'
                else:
                    buf += f'new {elem}[] {{' + convert(inner, elem) + '}'
                i = j; continue
        buf += c; i += 1
    return buf

if __name__ == '__main__':
    a = sys.argv[1:]
    elem = 'string'
    if '--elem' in a: k = a.index('--elem'); elem = a[k + 1]; del a[k:k + 2]
    s = open(a[0]).read()
    open(a[1], 'w').write(convert(s, elem))
