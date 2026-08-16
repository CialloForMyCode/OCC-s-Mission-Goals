import re, glob

files = [p for p in glob.glob('**/*.xaml', recursive=True) if '/obj/' not in p and '/bin/' not in p]
seen = []
seen_set = set()
for p in sorted(files):
    data = open(p, encoding='utf-8').read()
    for m in re.finditer(r'\{loc:Loc\s+([^}]*?)\}', data, re.S):
        body = m.group(1)
        en = re.search(r'En="([^"]*)"', body)
        zh = re.search(r'Zh="([^"]*)"', body)
        if not zh:
            # shorthand {loc:Loc "中文", En=...}
            zh = re.search(r'^\s*"([^"]*)"', body)
        key = en.group(1) if en else (zh.group(1) if zh else body)
        if key not in seen_set:
            seen_set.add(key)
            seen.append((p, zh.group(1) if zh else '', key))

for p, zh, en in seen:
    print(f"{zh}\t{en}")
