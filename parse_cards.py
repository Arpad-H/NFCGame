"""Render Riftborn card assets (Unity YAML with SerializeReference graphs) as readable trees."""
import os, re, sys, json

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Assets")
CARDS = os.path.join(ROOT, "Adressables", "Cards")

GAME_EVENT = ["OnPlayed","OnRoundStart","OnRoundEnd","OnAboutToAttack","OnAttack",
              "OnAboutToTakeDamage","OnDamaged","OnAboutToBeHealed","OnHealed","OnKilled",
              "OnCombatResolution","OnCardDrawn","OnCardDiscarded","OnActivateEffectEvent",
              "OnDeactivateEffectEvent","OnStatusEffectApplied","OnStatusEffectRemoved",
              "OnPortalDamaged"]
RESONANCE = ["Darkness","Plague","Death","Psychic","Life","Holy"]
RUNE = ["None","Void","Shield","Lightning","Fire","Ice","Life","Time","Eye","Light","Cursed","Skull"]
STATUS_TYPE = ["Plague","Burn","Freeze","ItemPassive","Hidden","Stun","Sleep","Stealth",
               "Taunt","Cursed","Enraged"]
DMG_SRC = ["Attack","Effect","Spell","StatusEffect"]
POSITION = ["Front","Back"]
STAT = ["Health","Attack"]

def mask_names(v):
    v = int(v)
    if v == -1 or v == 0xFFFFFFFF: return "All"
    names = [STATUS_TYPE[i] for i in range(len(STATUS_TYPE)) if v & (1 << i)]
    extra = v & ~((1 << len(STATUS_TYPE)) - 1)
    if extra: names.append(f"?bits:{hex(extra)}")
    return "+".join(names) if names else "None(0)"

# ---------- guid -> asset name map ----------
GUIDMAP = {}
for base, dirs, files in os.walk(ROOT):
    if "\\Plugins" in base or "\\TextMesh Pro" in base or "\\Le Tai" in base or "Sherbbs" in base or "VarietyFX" in base:
        continue
    for f in files:
        if f.endswith(".meta") and (f.endswith(".asset.meta") or f.endswith(".prefab.meta")):
            p = os.path.join(base, f)
            try:
                with open(p, encoding="utf-8") as fh:
                    for line in fh:
                        if line.startswith("guid:"):
                            GUIDMAP[line.split()[1].strip()] = f[:-11]  # strip ".asset.meta"/keep name
                            break
            except Exception:
                pass

# ---------- minimal YAML-ish parser for these assets ----------
# We rely on the regular structure Unity writes: 2-space indents, "key: value",
# list items "- ", and the references/RefIds block.

def parse_card(path):
    with open(path, encoding="utf-8") as fh:
        lines = fh.read().splitlines()

    top = {}          # top-level scalar fields we care about
    refs = {}         # rid -> {"class":..., "data": {...}}
    audio = []

    i = 0
    n = len(lines)
    # find top-level fields (4-space? actually 2-space under MonoBehaviour)
    while i < n:
        line = lines[i]
        m = re.match(r"^  (\w+):\s*(.*)$", line)
        if m:
            key, val = m.group(1), m.group(2)
            if key == "cardType":
                # value on next line: "    rid: NNN"
                m2 = re.match(r"^\s+rid:\s*(-?\d+)", lines[i+1]) if i+1 < n else None
                top["cardType_rid"] = int(m2.group(1)) if m2 else None
            elif key == "audioOnEvents":
                j = i + 1
                while j < n and re.match(r"^  - ", lines[j]):
                    mt = re.match(r"^  - Trigger:\s*(\d+)", lines[j])
                    if mt: audio.append(int(mt.group(1)))
                    j += 1
                    while j < n and re.match(r"^    \w", lines[j]): j += 1
                i = j - 1
            elif key == "references":
                break
            else:
                top[key] = val
        i += 1

    # parse references block
    # locate "    RefIds:"
    while i < n and not re.match(r"^\s+RefIds:", lines[i]):
        i += 1
    i += 1
    cur = None
    while i < n:
        line = lines[i]
        m = re.match(r"^    - rid:\s*(-?\d+)", line)
        if m:
            cur = int(m.group(1))
            refs[cur] = {"class": None, "data": {}}
            i += 1
            continue
        m = re.match(r"^      type:\s*\{class:\s*([^,]*),", line)
        if m and cur is not None:
            refs[cur]["class"] = m.group(1).strip() or None
            i += 1
            continue
        if re.match(r"^      data:\s*$", line) or re.match(r"^      data:\s", line):
            # consume the data block (indent > 6)
            data = {}
            i += 1
            while i < n:
                dl = lines[i]
                if re.match(r"^    - rid:", dl) or not dl.startswith("        "):
                    break
                dm = re.match(r"^        (\w+):\s*(.*)$", dl)
                if dm:
                    k, v = dm.group(1), dm.group(2)
                    if v == "":
                        # nested: either "rid: N" on next line, or a list of "- rid: N" / "- scalar"
                        if i+1 < n and re.match(r"^          rid:\s*(-?\d+)", lines[i+1]):
                            data[k] = ("ref", int(re.match(r"^          rid:\s*(-?\d+)", lines[i+1]).group(1)))
                            i += 1
                        else:
                            items = []
                            j = i + 1
                            while j < n and re.match(r"^        - ", lines[j]):
                                lm = re.match(r"^        - rid:\s*(-?\d+)", lines[j])
                                if lm: items.append(("ref", int(lm.group(1))))
                                else: items.append(("scalar", lines[j].strip("- ").strip()))
                                j += 1
                            if items:
                                data[k] = ("list", items)
                                i = j - 1
                            else:
                                data[k] = ("empty", None)
                    elif v.startswith("{fileID"):
                        gm = re.search(r"guid:\s*([0-9a-f]+)", v)
                        if gm:
                            g = gm.group(1)
                            data[k] = ("guidref", GUIDMAP.get(g, "UNKNOWN:" + g))
                        else:
                            data[k] = ("guidref", "None" if "fileID: 0" in v else v)
                    else:
                        data[k] = ("scalar", v)
                i += 1
            refs[cur]["data"] = data
            continue
        i += 1

    return top, refs, audio

# ---------- pretty printer ----------
def fmt_value(cls, key, val):
    kind, v = val
    if kind == "scalar":
        try: iv = int(v)
        except (ValueError, TypeError): return v
        if key == "type": return GAME_EVENT[iv] if 0 <= iv < len(GAME_EVENT) else f"?event{iv}"
        if key == "sourceType": return DMG_SRC[iv] if 0 <= iv < len(DMG_SRC) else str(iv)
        if key in ("filter", "effects", "statusEffect"): return mask_names(iv)
        if key == "position": return POSITION[iv] if 0 <= iv < len(POSITION) else str(iv)
        if key == "resonance": return RESONANCE[iv] if 0 <= iv < len(RESONANCE) else str(iv)
        if key in ("isAlly","targetOpponent","spawnAtFront","spawnForOpponent","resetEachRound","randomDiscard","hidden","onlySelf"):
            return "true" if iv else "false"
        if key == "stat": return STAT[iv] if 0 <= iv < len(STAT) else str(iv)
        return v
    if kind == "guidref": return f"->[{v}]"
    return str(v)

def render(rid, refs, indent, out, seen):
    pad = "  " * indent
    if rid not in refs:
        out.append(f"{pad}<missing rid {rid}>")
        return
    node = refs[rid]
    cls = node["class"]
    if cls is None:
        out.append(f"{pad}(null)")
        return
    if rid in seen:
        out.append(f"{pad}{cls} <cycle>")
        return
    seen = seen | {rid}
    scalars = []
    children = []  # (key, rid) or (key, list)
    for k, val in node["data"].items():
        kind, v = val
        if kind == "ref":
            children.append((k, [v]))
        elif kind == "list":
            rids = [x[1] for x in v if x[0] == "ref"]
            if rids: children.append((k, rids))
            elif v: scalars.append(f"{k}=[{','.join(str(x[1]) for x in v)}]")
        elif kind == "empty":
            pass
        else:
            scalars.append(f"{k}={fmt_value(cls, k, val)}")
    head = f"{pad}{cls}"
    if scalars: head += " (" + ", ".join(scalars) + ")"
    out.append(head)
    for k, rids in children:
        out.append(f"{pad}  .{k}:")
        for r in rids:
            render(r, refs, indent + 2, out, seen)

def rune_pair(hexstr):
    if not hexstr or not re.fullmatch(r"[0-9a-fA-F]+", hexstr): return hexstr
    vals = []
    for off in range(0, len(hexstr), 8):
        chunk = hexstr[off:off+8]
        b = bytes.fromhex(chunk)
        vals.append(int.from_bytes(b, "little"))
    return "/".join(RUNE[v] if 0 <= v < len(RUNE) else str(v) for v in vals)

def main():
    outpath = sys.argv[1]
    blocks = []
    for base, dirs, files in os.walk(CARDS):
        for f in sorted(files):
            if not f.endswith(".asset"): continue
            path = os.path.join(base, f)
            top, refs, audio = parse_card(path)
            rel = os.path.relpath(path, ROOT)
            b = [f"===== {rel} ====="]
            b.append(f"cardName: {top.get('cardName')}   resonance: {fmt_value(None,'resonance',('scalar',top.get('resonance','0')))}")
            ct_rid = top.get("cardType_rid")
            ct = refs.get(ct_rid, {"class": "??", "data": {}})
            b.append(f"cardType: {ct['class']}")
            if audio:
                b.append("audioOnEvents: " + ", ".join(GAME_EVENT[a] if a < len(GAME_EVENT) else str(a) for a in audio))
            d = ct["data"]
            def sval(key):
                if key not in d: return None
                kind, v = d[key]
                return v if kind == "scalar" else v
            if ct["class"] in ("MinionType",):
                b.append(f"stats: {sval('baseHealth')} HP / {sval('baseAttack')} ATK")
            if "effectActivatingRunes" in d:
                b.append(f"effectActivatingRunes: {rune_pair(d['effectActivatingRunes'][1])}")
            if "suppliedActivatorRunes" in d:
                b.append(f"suppliedActivatorRunes: {rune_pair(d['suppliedActivatorRunes'][1])}")
            if "keywords" in d and d["keywords"][0] == "list":
                kw = [x[1] for x in d["keywords"][1] if x[0] == "scalar"]
                # keywords are guid refs serialized inline as "- {fileID...}" scalars
                b.append(f"keywords: {kw}")
            for slot, trig_key in (("PASSIVE","PassiveEventTriggers"),("EFFECT1","Effect1EventTriggers"),
                                    ("EFFECT2","Effect2EventTriggers"),("SPELL","SpellEffects")):
                desc_key = {"PASSIVE":"passiveDescription","EFFECT1":"effect1Description",
                            "EFFECT2":"effect2Description","SPELL":"SpellDescription"}[slot]
                has_desc = desc_key in d
                has_trig = trig_key in d
                if not has_desc and not has_trig: continue
                desc = d.get(desc_key, ("scalar",""))[1] if has_desc else ""
                b.append(f"--- {slot}: \"{desc}\"")
                if has_trig and d[trig_key][0] == "list":
                    rids = [x[1] for x in d[trig_key][1] if x[0] == "ref"]
                    if not rids:
                        b.append("    (no triggers)")
                    for r in rids:
                        out = []
                        render(r, refs, 2, out, set())
                        b.extend(out)
                else:
                    b.append("    (no triggers)")
            if "DefaultCombatBehaviour" in d:
                b.append("--- DefaultCombatBehaviour (legacy/unused):")
                kind, v = d["DefaultCombatBehaviour"]
                if kind == "ref":
                    out = []
                    render(v, refs, 2, out, set())
                    b.extend(out)
            blocks.append("\n".join(b))
    with open(outpath, "w", encoding="utf-8") as fh:
        fh.write("\n\n".join(blocks))
    print(f"Wrote {len(blocks)} cards to {outpath}")

main()
