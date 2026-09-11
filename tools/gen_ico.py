from PIL import Image, ImageDraw

def create_icon():
    sizes = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)]
    images = []
    
    for w, h in sizes:
        img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        scale = w / 256.0
        
        # Outer watch bezel
        pad = int(14 * scale)
        rad = int(54 * scale)
        d.rounded_rectangle([(pad, pad), (w - pad, h - pad)], radius=rad, fill=(13, 17, 23, 255), outline=(0, 210, 255, 255), width=max(1, int(7 * scale)))
        
        # Tile 1 - Power (Amber/Cyan)
        d.rounded_rectangle([(int(40 * scale), int(42 * scale)), (int(216 * scale), int(96 * scale))], radius=max(2, int(14 * scale)), fill=(22, 27, 34, 255))
        d.rounded_rectangle([(int(52 * scale), int(78 * scale)), (int(160 * scale), int(84 * scale))], radius=max(1, int(3 * scale)), fill=(245, 158, 11, 255))
        
        # Tile 2 - Net (Blue)
        d.rounded_rectangle([(int(40 * scale), int(106 * scale)), (int(216 * scale), int(160 * scale))], radius=max(2, int(14 * scale)), fill=(22, 27, 34, 255))
        d.rounded_rectangle([(int(52 * scale), int(122 * scale)), (int(130 * scale), int(130 * scale))], radius=max(1, int(3 * scale)), fill=(88, 166, 255, 255))
        
        # Tile 3 - ZT (Emerald)
        d.rounded_rectangle([(int(40 * scale), int(170 * scale)), (int(216 * scale), int(214 * scale))], radius=max(2, int(12 * scale)), fill=(22, 27, 34, 255))
        d.ellipse([(int(54 * scale), int(186 * scale)), (int(68 * scale), int(200 * scale))], fill=(16, 185, 129, 255))
        
        images.append(img)
        
    images[0].save(r"C:\Users\4955\.gemini\antigravity\scratch\SysMonitor_Native\app.ico", format="ICO", sizes=sizes)
    print("app.ico created successfully!")

if __name__ == "__main__":
    create_icon()
