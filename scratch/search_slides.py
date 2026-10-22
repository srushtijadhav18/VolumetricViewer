import pypdf

reader = pypdf.PdfReader("CGG FA_compressed.pdf")
print("Total Slides:", len(reader.pages))

for idx, page in enumerate(reader.pages):
    lines = page.extract_text().split("\n")
    print(f"\n--- SLIDE {idx+1} ---")
    for line in lines[:5]:
        if line.strip():
            print("  ", line.strip())
