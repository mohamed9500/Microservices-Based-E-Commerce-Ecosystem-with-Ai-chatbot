#!/usr/bin/env python3
"""Generates the catalog seed JSON and local placeholder SVG images (no external image URLs are used).

Run from the repository root:  python3 scripts/generate_seed_assets.py
Outputs:
  services/catalog/Data/seed-products.json
  frontend/public/images/products/pNN.svg, pNN-b.svg, pNN-c.svg
  frontend/public/images/categories/<slug>.svg
  frontend/public/images/hero.svg
"""
import json, os, html

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

CATEGORIES = [
    (1, "phones-tablets", "Phones & Tablets", "Smartphones, tablets and the accessories that keep them running.", 215),
    (2, "computers", "Computers & Laptops", "Laptops, monitors and peripherals for work and play.", 232),
    (3, "audio", "Audio & Headphones", "Headphones, earbuds and speakers with room-filling sound.", 275),
    (4, "smart-home", "Smart Home", "Connected lighting, climate, security and networking.", 165),
    (5, "fashion", "Fashion & Accessories", "Everyday carry and wardrobe staples built to last.", 25),
    (6, "home-kitchen", "Home & Kitchen", "Appliances and cookware that make home life easier.", 12),
    (7, "sports-outdoors", "Sports & Outdoors", "Gear for running, hiking, yoga and tracking progress.", 140),
    (8, "beauty-care", "Beauty & Personal Care", "Skincare and grooming essentials.", 330),
]

# (n, sku, name, category, brand, price, featured, description, specs)
P = [
 (1,"PH-AUR-001","Aurora X5 Smartphone 128GB","phones-tablets","Aurora",599.00,True,"A bright 6.4-inch OLED smartphone with a dual-camera system, all-day battery and fast 5G connectivity.",{"Storage":"128 GB","Display":"6.4in OLED 120Hz","Battery":"4,500 mAh","Camera":"50 MP + 12 MP"}),
 (2,"PH-AUR-002","Aurora X5 Pro Smartphone 256GB","phones-tablets","Aurora",799.00,False,"The Pro model adds a telephoto lens, 256 GB of storage and a titanium-look frame for demanding users.",{"Storage":"256 GB","Display":"6.7in OLED 120Hz","Battery":"5,000 mAh","Camera":"50 MP + 48 MP + 12 MP"}),
 (3,"PH-NIM-003","Nimbus Tab 11 Tablet","phones-tablets","Nimbus",349.00,False,"An 11-inch tablet with a sharp 2K display and quad speakers, ideal for reading, streaming and note taking.",{"Storage":"128 GB","Display":"11in LCD 2K","Battery":"8,000 mAh","Weight":"490 g"}),
 (4,"PH-NIM-004","Nimbus Fast Charger 65W","phones-tablets","Nimbus",39.90,False,"A compact 65 W GaN charger with two USB-C ports that fast-charges phones, tablets and ultrabooks.",{"Output":"65 W","Ports":"2x USB-C","Technology":"GaN","Cable":"1 m USB-C included"}),
 (5,"CP-VLT-005","Voltix Air 14 Laptop","computers","Voltix",899.00,True,"A light 14-inch laptop with a fast 12-core processor, 16 GB of memory and up to 15 hours of battery life.",{"Processor":"12-core","Memory":"16 GB","Storage":"512 GB SSD","Weight":"1.3 kg"}),
 (6,"CP-VLT-006","Voltix Pro 16 Laptop","computers","Voltix",1499.00,False,"A 16-inch performance laptop with a dedicated graphics card and a colour-accurate display for creators.",{"Processor":"14-core","Memory":"32 GB","Storage":"1 TB SSD","Graphics":"Dedicated 8 GB"}),
 (7,"CP-LUM-007","Lumen 27in 4K Monitor","computers","Lumen",429.00,False,"A 27-inch 4K IPS monitor with wide colour coverage, USB-C power delivery and a height-adjustable stand.",{"Resolution":"3840x2160","Panel":"IPS","Refresh":"60 Hz","Connectivity":"USB-C 65W, HDMI, DP"}),
 (8,"CP-KEY-008","Keystone Mechanical Keyboard","computers","Keystone",89.00,False,"A compact wireless mechanical keyboard with hot-swappable switches and per-key backlighting.",{"Layout":"75%","Switches":"Hot-swappable tactile","Connectivity":"Bluetooth + 2.4 GHz + USB-C","Battery":"Up to 200 h"}),
 (9,"AU-AUR-009","Aurora Wireless Headphones","audio","Aurora",199.00,True,"Over-ear wireless headphones with adaptive noise cancelling, plush ear cushions and 40 hours of playback.",{"Type":"Over-ear","Noise cancelling":"Adaptive ANC","Battery":"40 h","Connectivity":"Bluetooth 5.3"}),
 (10,"AU-ECH-010","Echo Pods True Wireless Earbuds","audio","Echo",129.00,False,"Pocketable true wireless earbuds with clear calls, a secure fit for workouts and a wireless charging case.",{"Type":"In-ear","Noise cancelling":"Active","Battery":"8 h + 24 h case","Water resistance":"IPX4"}),
 (11,"AU-ECH-011","Echo Studio Bluetooth Speaker","audio","Echo",149.00,False,"A rugged portable speaker with 360-degree sound, deep bass and 18 hours of battery life.",{"Output":"30 W","Battery":"18 h","Water resistance":"IP67","Connectivity":"Bluetooth 5.3"}),
 (12,"AU-SON-012","Sonara Soundbar 2.1","audio","Sonara",249.00,False,"A slim 2.1-channel soundbar with a wireless subwoofer that makes movies and dialogue clearer.",{"Channels":"2.1","Output":"220 W","Inputs":"HDMI eARC, optical, Bluetooth","Subwoofer":"Wireless"}),
 (13,"SH-LUM-013","Lumen Smart Bulb 4-Pack","smart-home","Lumen",34.99,False,"Wi-Fi colour bulbs you can dim, schedule and control by voice or app, with no hub required.",{"Pack":"4 bulbs","Brightness":"800 lumens","Colours":"16 million","Connectivity":"Wi-Fi 2.4 GHz"}),
 (14,"SH-HAV-014","Haven Smart Thermostat","smart-home","Haven",159.00,True,"A learning thermostat that builds an energy-saving schedule and can be controlled from anywhere.",{"Display":"Colour touch","Compatibility":"Most 24V systems","Sensors":"Temperature, humidity, motion","Connectivity":"Wi-Fi"}),
 (15,"SH-HAV-015","Haven Wi-Fi Security Camera","smart-home","Haven",79.00,False,"A 2K indoor/outdoor security camera with colour night vision, two-way audio and motion alerts.",{"Resolution":"2K","Night vision":"Colour","Weather rating":"IP65","Storage":"Local microSD or cloud"}),
 (16,"SH-NIM-016","Nimbus Mesh Wi-Fi Router 2-Pack","smart-home","Nimbus",189.00,False,"A two-node Wi-Fi 6 mesh system covering up to 4,500 sq ft with simple app-based setup.",{"Standard":"Wi-Fi 6","Coverage":"Up to 4,500 sq ft","Speed":"AX3000","Ports":"2x Gigabit per node"}),
 (17,"FA-TRA-017","Trailmark Leather Backpack","fashion","Trailmark",129.00,True,"A full-grain leather backpack with a padded 15-inch laptop sleeve and weatherproof lining.",{"Material":"Full-grain leather","Capacity":"22 L","Laptop sleeve":"15in","Colour":"Tan"}),
 (18,"FA-TRA-018","Trailmark Classic Sunglasses","fashion","Trailmark",89.00,False,"Polarised sunglasses with UV400 lenses and a lightweight acetate frame.",{"Lenses":"Polarised UV400","Frame":"Acetate","Fit":"Medium","Case":"Hard case included"}),
 (19,"FA-URB-019","Urbana Merino Wool Sweater","fashion","Urbana",99.00,False,"A soft, breathable crew-neck sweater in 100% merino wool that resists odour and keeps its shape.",{"Material":"100% merino wool","Fit":"Regular","Care":"Machine wash cold","Colour":"Charcoal"}),
 (20,"FA-URB-020","Urbana Minimalist Watch","fashion","Urbana",179.00,False,"A slim 40 mm stainless steel watch with a sapphire-coated glass and a genuine leather strap.",{"Case":"40 mm steel","Glass":"Sapphire coated","Water resistance":"5 ATM","Movement":"Quartz"}),
 (21,"HK-CAS-021","Casa Espresso Machine","home-kitchen","Casa",329.00,True,"A 15-bar espresso machine with a built-in grinder and steam wand for cafe-quality drinks at home.",{"Pressure":"15 bar","Grinder":"Built-in conical burr","Tank":"1.8 L","Milk":"Manual steam wand"}),
 (22,"HK-CAS-022","Casa Air Fryer 5L","home-kitchen","Casa",119.00,False,"A 5-litre air fryer with eight presets that cooks crisp meals with little or no oil.",{"Capacity":"5 L","Power":"1700 W","Presets":"8","Basket":"Dishwasher safe"}),
 (23,"HK-PUR-023","Purely Robot Vacuum","home-kitchen","Purely",279.00,False,"A mapping robot vacuum with strong suction, a mopping pad and scheduling from your phone.",{"Suction":"4000 Pa","Runtime":"150 min","Navigation":"Lidar mapping","Mopping":"Yes"}),
 (24,"HK-CAS-024","Casa Ceramic Cookware Set 10-Piece","home-kitchen","Casa",189.00,False,"A ten-piece non-stick ceramic cookware set that is oven safe and works on every hob type.",{"Pieces":"10","Coating":"Ceramic non-stick","Oven safe":"Up to 260 C","Hob":"Induction compatible"}),
 (25,"SP-ALP-025","Alpine Trail Running Shoes","sports-outdoors","Alpine",119.00,True,"Cushioned trail running shoes with a grippy outsole, rock plate and breathable mesh upper.",{"Drop":"6 mm","Weight":"280 g","Outsole":"Multi-directional lugs","Use":"Trail"}),
 (26,"SP-ALP-026","Alpine 40L Hiking Backpack","sports-outdoors","Alpine",109.00,False,"A ventilated 40-litre hiking pack with a rain cover, hydration sleeve and adjustable harness.",{"Capacity":"40 L","Weight":"1.4 kg","Rain cover":"Included","Hydration":"Compatible"}),
 (27,"SP-PUL-027","Pulse Fitness Tracker","sports-outdoors","Pulse",59.00,False,"A slim fitness tracker with heart-rate and sleep tracking, GPS connect and a 10-day battery.",{"Battery":"10 days","Sensors":"Heart rate, SpO2","Water resistance":"5 ATM","Display":"AMOLED"}),
 (28,"SP-PUL-028","Pulse Yoga Mat Premium","sports-outdoors","Pulse",39.00,False,"A 6 mm non-slip yoga mat made from natural rubber with alignment markings and a carry strap.",{"Thickness":"6 mm","Material":"Natural rubber","Size":"183 x 61 cm","Grip":"Non-slip"}),
 (29,"BE-GLO-029","Glow Vitamin C Serum 30ml","beauty-care","Glow",24.00,False,"A lightweight brightening serum with 15% vitamin C and hyaluronic acid for daily use.",{"Volume":"30 ml","Key ingredient":"15% vitamin C","Skin type":"All","Fragrance":"Fragrance-free"}),
 (30,"BE-GLO-030","Glow Electric Toothbrush","beauty-care","Glow",69.00,False,"A sonic electric toothbrush with five modes, a two-minute timer and a three-week battery.",{"Modes":"5","Battery":"21 days","Heads":"2 included","Charging":"USB-C"}),
 (31,"BE-PUR-031","Purely Hair Dryer Ionic","beauty-care","Purely",89.00,False,"A fast-drying ionic hair dryer with three heat settings, a cool shot and a diffuser attachment.",{"Power":"1800 W","Technology":"Ionic","Settings":"3 heat, 2 speed","Attachments":"Concentrator, diffuser"}),
 (32,"BE-PUR-032","Purely Hydrating Face Cream","beauty-care","Purely",32.00,True,"A rich daily moisturiser with ceramides and niacinamide that hydrates for 24 hours.",{"Volume":"50 ml","Key ingredients":"Ceramides, niacinamide","Skin type":"Dry to normal","Fragrance":"Fragrance-free"}),
]

def hsl(h, s, l): return f"hsl({h % 360},{s}%,{l}%)"

ICONS = {  # simple 120x120 icon paths per category (stroke icons)
 "phones-tablets": '<rect x="38" y="14" width="44" height="92" rx="9"/><line x1="52" y1="96" x2="68" y2="96"/>',
 "computers": '<rect x="20" y="28" width="80" height="52" rx="5"/><path d="M10 92h100l-8 8H18z"/>',
 "audio": '<path d="M24 70v-14a36 36 0 0 1 72 0v14"/><rect x="20" y="68" width="18" height="30" rx="6"/><rect x="82" y="68" width="18" height="30" rx="6"/>',
 "smart-home": '<path d="M16 62 60 24l44 38"/><path d="M28 56v44h64V56"/><circle cx="60" cy="78" r="9"/>',
 "fashion": '<path d="M38 36h44l8 66H30z"/><path d="M48 36a12 12 0 0 1 24 0"/>',
 "home-kitchen": '<path d="M26 54h68v34a14 14 0 0 1-14 14H40a14 14 0 0 1-14-14z"/><path d="M94 62h10a8 8 0 0 1 0 22H94"/><path d="M44 40c0-8 8-8 8-16M64 40c0-8 8-8 8-16"/>',
 "sports-outdoors": '<path d="M14 90 48 36l20 30 14-18 24 42z"/><circle cx="86" cy="30" r="8"/>',
 "beauty-care": '<rect x="44" y="40" width="32" height="58" rx="7"/><rect x="50" y="22" width="20" height="18" rx="4"/>',
}

def svg(w, h, hue, icon, title, sub="", variant=0):
    a, b = hsl(hue, 70, 92 - variant * 3), hsl(hue + 30, 65, 80 - variant * 4)
    fg = hsl(hue, 55, 28)
    ang = ["0", "1", "0"][variant]
    x2, y2 = ("1", "1") if variant != 1 else ("0", "1")
    t = html.escape(title); s = html.escape(sub)
    extra = {0: '', 1: f'<circle cx="{w*0.82}" cy="{h*0.2}" r="{h*0.18}" fill="{hsl(hue,60,70)}" opacity=".35"/>',
             2: f'<rect x="{w*0.05}" y="{h*0.7}" width="{w*0.9}" height="{h*0.2}" rx="{h*0.05}" fill="{hsl(hue,60,70)}" opacity=".25"/>'}[variant]
    return f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" role="img" aria-label="{t}">
<defs><linearGradient id="g" x1="0" y1="0" x2="{x2}" y2="{y2}"><stop offset="0" stop-color="{a}"/><stop offset="1" stop-color="{b}"/></linearGradient></defs>
<rect width="{w}" height="{h}" fill="url(#g)"/>{extra}
<g transform="translate({w/2-60*(w/200):.1f},{h*0.10:.1f}) scale({w/200:.2f})" fill="none" stroke="{fg}" stroke-width="3" stroke-linecap="round" stroke-linejoin="round">{icon}</g>
<text x="{w/2}" y="{h*0.78}" text-anchor="middle" font-family="Inter,Arial,sans-serif" font-size="{w*0.052:.0f}" font-weight="700" fill="{fg}">{t}</text>
<text x="{w/2}" y="{h*0.86}" text-anchor="middle" font-family="Inter,Arial,sans-serif" font-size="{w*0.036:.0f}" fill="{fg}" opacity=".75">{s}</text>
</svg>'''

def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f: f.write(text)

hue_by_cat = {c[1]: c[4] for c in CATEGORIES}
seed = {
    "categories": [{"n": n, "slug": s, "name": nm, "description": d} for n, s, nm, d, _ in CATEGORIES],
    "products": [{"n": n, "sku": sku, "name": nm, "category": cat, "brand": br, "price": pr, "featured": ft,
                  "description": desc, "specs": specs} for n, sku, nm, cat, br, pr, ft, desc, specs in P],
}
write(os.path.join(ROOT, "services/catalog/Data/seed-products.json"), json.dumps(seed, indent=2, ensure_ascii=False))

for n, sku, nm, cat, br, pr, ft, desc, specs in P:
    hue = hue_by_cat[cat]
    for variant, suffix in enumerate(["", "-b", "-c"]):
        write(os.path.join(ROOT, f"frontend/public/images/products/p{n:02d}{suffix}.svg"),
              svg(800, 800, hue + n * 3, ICONS[cat], nm, br + " · " + ["Front view", "Detail view", "In context"][variant], variant))
for n, slug, nm, d, hue in CATEGORIES:
    write(os.path.join(ROOT, f"frontend/public/images/categories/{slug}.svg"), svg(600, 400, hue, ICONS[slug], nm, "Shop the collection"))
write(os.path.join(ROOT, "frontend/public/images/hero.svg"),
      f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1200 700" role="img" aria-label="Nexus Commerce hero">
<defs><linearGradient id="h" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#1e1b4b"/><stop offset="1" stop-color="#4338ca"/></linearGradient></defs>
<rect width="1200" height="700" fill="url(#h)"/>
<circle cx="900" cy="250" r="230" fill="#818cf8" opacity=".25"/><circle cx="1000" cy="480" r="140" fill="#38bdf8" opacity=".25"/><circle cx="760" cy="520" r="90" fill="#f0abfc" opacity=".25"/>
<g fill="none" stroke="#e0e7ff" stroke-width="5" stroke-linecap="round" stroke-linejoin="round" transform="translate(760,170) scale(2.4)">{ICONS["audio"]}</g>
</svg>''')
print("generated", len(P), "products,", len(CATEGORIES), "categories")
