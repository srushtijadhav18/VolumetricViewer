import re

with open("CGG FA_compressed.pdf", "rb") as f:
    data = f.read()

text_blocks = re.findall(b"BT(.*?)ET", data, re.DOTALL)

extracted_strings = []
for b in text_blocks:
    strs = re.findall(b"\\((.*?)\\)", b)
    for s in strs:
        try:
            decoded = s.decode("utf-8", errors="ignore")
            if len(decoded.strip()) > 1:
                extracted_strings.append(decoded.strip())
        except:
            pass

full_text = "\n".join(extracted_strings)

with open("scratch/pdf_text.txt", "w", encoding="utf-8") as out:
    out.write(full_text)

print("Wrote", len(full_text), "characters to scratch/pdf_text.txt")
