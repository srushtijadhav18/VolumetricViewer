import sys

try:
    import pypdf
    reader = pypdf.PdfReader("CGG FA_compressed.pdf")
    print(f"Total Slides/Pages: {len(reader.pages)}")
    for i, page in enumerate(reader.pages):
        print(f"=== SLIDE {i+1} ===")
        text = page.extract_text()
        print(text if text else "[No text extracted]")
except Exception as e:
    print("Error:", e)
    # try pdfminer / fitz or raw extraction if pypdf not available
