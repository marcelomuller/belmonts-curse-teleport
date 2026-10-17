import sys, re, dnfile
pe = dnfile.dnPE(sys.argv[1]); md = pe.net.mdtables; pat = re.compile(sys.argv[2])
for td in md.TypeDef:
    for m in (td.MethodList or []):
        n = str(m.row.Name)
        if pat.search(n) and not n.startswith("Native"):
            print(f"{td.TypeName}.{n}({', '.join(str(p.row.Name) for p in (m.row.ParamList or []))})")
