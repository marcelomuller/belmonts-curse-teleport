import sys, re, dnfile
pe = dnfile.dnPE(sys.argv[1]); md = pe.net.mdtables; pat = re.compile(sys.argv[2])
names = {}
for pm in md.PropertyMap:
    t = pm.Parent.row
    names.setdefault(str(t.TypeName), []).extend(str(p.row.Name) for p in pm.PropertyList)
for td in md.TypeDef:
    n = str(td.TypeName)
    if pat.search(n):
        flds = [str(f.row.Name) for f in (td.FieldList or []) if not str(f.row.Name).startswith(("NativeFieldInfoPtr","NativeMethodInfoPtr"))]
        print("==", n, "props:", names.get(n, []), "fields:", flds)
