# compare two rect dumps: print the elements whose boxes differ by more than tol px
import sys
def load(f):
    d = {}
    for line in open(f):
        k, *v = line.rstrip('\n').rsplit(' ', 4)
        if len(v) == 4:
            n = 0
            while (k, n) in d: n += 1
            d[(k, n)] = list(map(float, v))
    return d
a, b = load(sys.argv[1]), load(sys.argv[2])
tol = float(sys.argv[3]) if len(sys.argv) > 3 else 2
filt = sys.argv[4] if len(sys.argv) > 4 else ''
bad = 0
for k in a:
    if filt and filt not in k[0]: continue
    if k not in b: print('MISSING in mine:', k[0][-90:], a[k]); bad += 1; continue
    ra, rb = a[k], b[k]
    if any(abs(x - y) > tol for x, y in zip(ra, rb)):
        print(k[0][-90:], 'ref', ra, 'mine', rb); bad += 1
for k in b:
    if filt and filt not in k[0]: continue
    if k not in a: print('EXTRA in mine:', k[0][-90:], b[k])
print(bad, 'differ of', len(a))
