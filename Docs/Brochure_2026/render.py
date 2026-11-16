import sys
from playwright.sync_api import sync_playwright
src, pdf, png = sys.argv[1], sys.argv[2], sys.argv[3]
with sync_playwright() as p:
    b = p.chromium.launch(); pg = b.new_page(viewport={"width": 1056, "height": 816}, device_scale_factor=1.25)
    pg.goto("file://" + __import__("os").path.abspath(src)); pg.wait_for_timeout(600)
    pg.pdf(path=pdf, width="11in", height="8.5in", print_background=True, margin={"top": "0", "bottom": "0", "left": "0", "right": "0"})
    for i, el in enumerate(pg.query_selector_all(".sheet")): el.screenshot(path=png % (i + 1))
    # overflow check: any panel whose content is taller than the panel
    print(pg.evaluate("""[...document.querySelectorAll('.panel')].map((p,i)=>i+':'+(p.scrollHeight-p.clientHeight)).join(' ')"""))
    b.close()
