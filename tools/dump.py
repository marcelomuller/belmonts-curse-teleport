import sys, re, dnfile

path, pattern = sys.argv[1], re.compile(sys.argv[2])
pe = dnfile.dnPE(path)
md = pe.net.mdtables

def s(x):
    return str(x) if x is not None else ""

for td in md.TypeDef:
    full = (s(td.TypeNamespace) + "." if s(td.TypeNamespace) else "") + s(td.TypeName)
    if not pattern.search(full):
        continue
    ext = td.Extends.row if td.Extends and td.Extends.row is not None else None
    base = ""
    if ext is not None:
        base = s(getattr(ext, "TypeName", ""))
    print(f"== {full} : {base}")
    for m in td.MethodList or []:
        n = s(m.row.Name)
        if n.startswith("NativeMethodInfoPtr") or n == ".cctor":
            continue
        params = [s(p.row.Name) for p in (m.row.ParamList or [])]
        print(f"   {n}({', '.join(params)})")
