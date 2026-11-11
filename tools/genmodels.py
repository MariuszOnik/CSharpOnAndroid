# Generuje modele przykladowej sceny 3D (OBJ + MTL + tekstury PNG). Wszystko z kodu - bez obcych zasobow.
import os, sys, zlib, struct, random

out = sys.argv[1]
os.makedirs(out, exist_ok=True)


def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def mean(points): return tuple(sum(p[i] for p in points) / len(points) for i in range(3))


class Obj:
    def __init__(self, mtl):
        self.lines = ["# Wygenerowane przez genmodels.py", "mtllib " + mtl]
        self.nv = 0
        self.nvt = 0

    def part(self, name): self.lines.append("o " + name)
    def material(self, name): self.lines.append("usemtl " + name)

    # Bryla wypukla: sciany ustawiamy przeciwnie do ruchu wskazowek zegara patrzac z zewnatrz (konwencja OBJ).
    def solid(self, verts, faces, uvs=None):
        center = mean(verts)
        base = self.nv
        for v in verts:
            self.lines.append("v %.4f %.4f %.4f" % v)
        self.nv += len(verts)
        for fi, face in enumerate(faces):
            pts = [verts[i] for i in face]
            normal = cross(sub(pts[1], pts[0]), sub(pts[2], pts[0]))
            uv = list(uvs[fi]) if uvs and uvs[fi] else None
            face = list(face)
            if dot(normal, sub(mean(pts), center)) < 0:
                face.reverse()
                if uv: uv.reverse()
            if uv:
                for t in uv:
                    self.lines.append("vt %.4f %.4f" % t)
                refs = ["%d/%d" % (base + i + 1, self.nvt + k + 1) for k, i in enumerate(face)]
                self.nvt += len(uv)
            else:
                refs = [str(base + i + 1) for i in face]
            self.lines.append("f " + " ".join(refs))

    def box(self, lo, hi, uv=None):
        x0, y0, z0 = lo
        x1, y1, z1 = hi
        self.frustum(((x0 + x1) / 2, (z0 + z1) / 2), y0, y1, (x1 - x0) / 2, (z1 - z0) / 2, (x1 - x0) / 2, (z1 - z0) / 2, uv)

    # Sciety ostroslup o prostokatnej podstawie; rowne wymiary dolu i gory daja pudelko.
    def frustum(self, c, y0, y1, bx, bz, tx, tz, uv=None):
        cx, cz = c
        v = [(cx - bx, y0, cz - bz), (cx + bx, y0, cz - bz), (cx + bx, y0, cz + bz), (cx - bx, y0, cz + bz),
             (cx - tx, y1, cz - tz), (cx + tx, y1, cz - tz), (cx + tx, y1, cz + tz), (cx - tx, y1, cz + tz)]
        sides = [(0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        faces = sides + [(0, 1, 2, 3), (4, 5, 6, 7)]
        uvs = None
        if uv:
            quad = [(0, 0), (uv[0], 0), (uv[0], uv[1]), (0, uv[1])]
            uvs = [quad] * 4 + [None, None]
        self.solid(v, faces, uvs)

    def pyramid(self, c, y0, y1, bx, bz):
        cx, cz = c
        v = [(cx - bx, y0, cz - bz), (cx + bx, y0, cz - bz), (cx + bx, y0, cz + bz), (cx - bx, y0, cz + bz), (cx, y1, cz)]
        self.solid(v, [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4), (0, 1, 2, 3)])

    def save(self, name):
        with open(os.path.join(out, name), "w", newline="\n") as f:
            f.write("\n".join(self.lines) + "\n")


def png(name, size, pixel):
    raw = bytearray()
    for y in range(size):
        raw.append(0)
        for x in range(size):
            raw += bytes(pixel(x, y))
    def chunk(kind, data):
        body = kind + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xffffffff)
    data = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 2, 0, 0, 0))
    data += chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")
    with open(os.path.join(out, name), "wb") as f:
        f.write(data)


random.seed(7)

# ---- tekstury 64x64 (potega dwojki, zeby mozna je bylo powtarzac) ----
noise = [[random.random() for _ in range(64)] for _ in range(64)]

def bricks(x, y):
    row = y // 8
    bx = (x + (16 if row % 2 else 0)) % 32
    if y % 8 == 7 or bx == 31:
        return (196, 190, 178)
    shade = 0.82 + 0.18 * noise[row][(x + (16 if row % 2 else 0)) // 32 * 7 % 64] + 0.06 * noise[y][x]
    return (min(255, int(178 * shade)), min(255, int(84 * shade)), min(255, int(58 * shade)))

def grass(x, y):
    n = noise[y][x] * 0.6 + noise[(y * 3 + 11) % 64][(x * 5 + 3) % 64] * 0.4
    return (int(62 + 40 * n), int(128 + 55 * n), int(48 + 30 * n))

png("cegly.png", 64, bricks)
png("trawa.png", 64, grass)

# ---- wiatrak: wieza z cegiel, dach, smigla jako osobna czesc ----
o = Obj("wiatrak.mtl")
o.part("wieza")
o.material("cegly")
o.frustum((0, 0), 0, 5, 1.1, 1.1, 0.75, 0.75, uv=(2, 5))
o.material("dach")
o.pyramid((0, 0), 5, 6.4, 1.0, 1.0)
o.material("drewno")
o.box((-0.3, 0, 1.02), (0.3, 1.2, 1.12))            # drzwi
o.box((-0.12, 4.38, 0.7), (0.12, 4.62, 1.0))        # os smigiel
o.part("smigla")
o.material("smigla")
hub = (0, 4.5, 1.05)
o.box((hub[0] - 0.22, hub[1] - 0.22, hub[2] - 0.06), (hub[0] + 0.22, hub[1] + 0.22, hub[2] + 0.06))
L, W, T = 3.0, 0.42, 0.03
o.box((0.2, hub[1] - W / 2, hub[2] - T), (0.2 + L, hub[1] + W / 2, hub[2] + T))
o.box((-0.2 - L, hub[1] - W / 2, hub[2] - T), (-0.2, hub[1] + W / 2, hub[2] + T))
o.box((-W / 2, hub[1] + 0.2, hub[2] - T), (W / 2, hub[1] + 0.2 + L, hub[2] + T))
o.box((-W / 2, hub[1] - 0.2 - L, hub[2] - T), (W / 2, hub[1] - 0.2, hub[2] + T))
o.save("wiatrak.obj")
with open(os.path.join(out, "wiatrak.mtl"), "w", newline="\n") as f:
    f.write("newmtl cegly\nKd 1 1 1\nmap_Kd cegly.png\n\nnewmtl dach\nKd 0.55 0.2 0.16\n\n"
            "newmtl drewno\nKd 0.42 0.28 0.16\n\nnewmtl smigla\nKd 0.93 0.88 0.74\n")

# ---- drzewo ----
o = Obj("drzewo.mtl")
o.part("pien")
o.material("pien")
o.box((-0.18, 0, -0.18), (0.18, 1.1, 0.18))
o.part("korona")
o.material("liscie")
o.pyramid((0, 0), 0.9, 2.4, 1.0, 1.0)
o.pyramid((0, 0), 1.8, 3.2, 0.75, 0.75)
o.pyramid((0, 0), 2.7, 3.9, 0.5, 0.5)
o.save("drzewo.obj")
with open(os.path.join(out, "drzewo.mtl"), "w", newline="\n") as f:
    f.write("newmtl pien\nKd 0.4 0.27 0.15\n\nnewmtl liscie\nKd 0.16 0.5 0.2\n")

# ---- ziemia: jeden kwadrat 60x60 z powtarzana tekstura trawy ----
S, R = 30, 20
lines = ["# Wygenerowane przez genmodels.py", "mtllib ziemia.mtl", "o ziemia", "usemtl trawa",
         "v %d 0 %d" % (-S, S), "v %d 0 %d" % (S, S), "v %d 0 %d" % (S, -S), "v %d 0 %d" % (-S, -S),
         "vt 0 0", "vt %d 0" % R, "vt %d %d" % (R, R), "vt 0 %d" % R,
         "vn 0 1 0", "f 1/1/1 2/2/1 3/3/1 4/4/1"]
with open(os.path.join(out, "ziemia.obj"), "w", newline="\n") as f:
    f.write("\n".join(lines) + "\n")
with open(os.path.join(out, "ziemia.mtl"), "w", newline="\n") as f:
    f.write("newmtl trawa\nKd 1 1 1\nmap_Kd trawa.png\n")
print("ok")
