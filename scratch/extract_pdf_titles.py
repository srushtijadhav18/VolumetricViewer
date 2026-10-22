import re, sys

sys.stdout.reconfigure(encoding='utf-8')

with open("CGG FA_compressed.pdf", "rb") as f:
    content = f.read()

pages = content.split(b"/Type /Page")

print(f"Total Page objects found: {len(pages)-1}")

for i, p in enumerate(pages[1:], 1):
    strs = re.findall(b"\\((.*?)\\)", p)
    clean_strs = []
    for s in strs:
        try:
            dec = s.decode("latin1", errors="ignore").strip()
            if len(dec) > 2:
                clean_strs.append(dec)
        except:
            pass
    print(f"\n--- PAGE {i} ---")
    print(" | ".join(clean_strs[:10]))
