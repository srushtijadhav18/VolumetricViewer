import sys

sys.stdout.reconfigure(encoding='utf-8')

with open("CGG FA_compressed.pdf", "rb") as f:
    data = f.read()

import re

# Split by PDF page markers or streams
pages = data.split(b"/Type /Page")
print(f"Total Pages Found: {len(pages)-1}\n")

for idx, p in enumerate(pages[1:], 1):
    strs = re.findall(b"\\((.*?)\\)", p)
    clean_strs = []
    for s in strs:
        try:
            dec = s.decode("utf-8", errors="ignore").strip()
            if len(dec) > 1 and not dec.startswith("Font") and not dec.startswith("Obj"):
                clean_strs.append(dec)
        except:
            pass
    print(f"=== SLIDE {idx} ===")
    print(" ".join(clean_strs))
    print("\n")
