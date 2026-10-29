#!/usr/bin/env python3
r"""
pack.py - build the two release zips for the Hospital Challenge.

Run it with no arguments from anywhere:

    python tools\pack.py

WHAT IT PRODUCES, and why there are two of them. Fril, 29 Oct 2026:
"Nexus and 7D2DMods require all the mods to be in a zipped folder called Mods. The launcher
requires all the mods to be put into a folder called Mods and then that is zipped into a
folder called Mods."

    dist\nexus\Mods.zip       the mod folders at the ROOT of the zip
    dist\launcher\Mods.zip    one Mods\ folder at the root, mod folders inside it

Both are called Mods.zip, which is why they go in separate folders. Do not rename them.

Getting this wrong has cost play sessions before. The devlog's install saga, failure 3: "a
'helpful' repackage that put a top-level Hospital/ folder INSIDE the zip, so extracting into
Mods\Hospital\ produced Mods\Hospital\Hospital\". The two shapes above are the only two that
are correct, and the script prints the first three entries of each zip so you can see it.

WHAT IT DOES TO THE FILES

1. Copies Mods\ to a staging folder. THE REPO IS NEVER TOUCHED.
2. Strips every XML comment from the staging copy.
3. Merges playtest\zzzz_FrilBlocks into Hospital\Config.
4. Writes the two zips.

Any check failing deletes the staging folder and writes nothing at all. A half-built release
is worse than no release.
"""

import os
import re
import shutil
import sys
import xml.etree.ElementTree as ET
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
MODS = os.path.join(REPO, "Mods")
EXTRAS = os.path.join(REPO, "playtest")
DIST = os.path.join(REPO, "dist")

COMMENT = re.compile(r"<!--.*?-->", re.S)
BLANKS = re.compile(r"\n[ \t]*\n(?:[ \t]*\n)+")

# ---------------------------------------------------------------------------
# NEVER SHIPPED. Each line here is a mistake that already happened once.
#
# names.txt      the server owner's own file. Config\NAMES_README.txt tells players to
#                create it themselves and says plainly "names.txt is NOT shipped with this
#                mod, on purpose". It has been in every Config zip anyway, and the devlog
#                records it overwriting Fril's own file and reverting his class-name
#                conversion. The README ships; the file does not.
# stats\         runtime output, the generated debrief HTML from Fril's own runs.
# hospital_state_*.txt   runtime save state for a world on Fril's machine.
# *.zip          Mods\ currently holds a 954MB Mods.zip. Without this the build would zip
#                the previous release into the new one.
# zzzz_FrilBlocks   merged into Hospital\Config instead, see MERGE below. Excluded here as
#                well so a leftover copy in Mods\ cannot ship as a second folder AND be
#                merged, which would apply every rule twice.
# *.cs           Fril, 29 Oct 2026: "There should be no cs sent out to players." The repo
#                keeps the source next to each DLL, which is right; a player's Mods folder
#                has no use for it. Three were shipping: Gyrorepairmod\GyroRepairMod.cs and
#                Frilragdollfloorfix\{FrilRagdollFloorFix.cs, ragdoll.cs}. The repo is not
#                touched, only the release.
# *.pdb          debug symbols, same reasoning. 0_TFP_Harmony\TfpHarmony.pdb is the only one.
# ---------------------------------------------------------------------------
SKIP_DIRS = {"stats", "zzzz_FrilBlocks", ".git"}
SKIP_FILES = {"names.txt"}
SKIP_GLOBS = (re.compile(r"^hospital_state_.*\.txt$", re.I), re.compile(r".*\.zip$", re.I),
              re.compile(r".*\.cs$", re.I), re.compile(r".*\.pdb$", re.I))


def skip_file(name):
    if name in SKIP_FILES:
        return True
    return any(g.match(name) for g in SKIP_GLOBS)


def version():
    with open(os.path.join(MODS, "Hospital", "ModInfo.xml"), encoding="utf-8") as fh:
        m = re.search(r'<Version\s+value="([^"]+)"', fh.read())
    if not m:
        raise SystemExit("FAIL could not read the version from Mods/Hospital/ModInfo.xml")
    return m.group(1)


def shape(e):
    """A comparable skeleton of an element: tag, attributes, text, children, in order."""
    return (
        e.tag,
        tuple(sorted(e.attrib.items())),
        (e.text or "").strip(),
        (e.tail or "").strip(),
        tuple(shape(c) for c in e),
    )


def strip(text):
    out = COMMENT.sub("", text)
    out = BLANKS.sub("\n\n", out)
    return out


def copy_tree(src, dst):
    """Copy Mods\\ into staging, dropping everything in the skip lists."""
    dropped = []
    for base, dirs, files in os.walk(src):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for d in sorted(set(os.listdir(base)) - set(dirs)):
            full = os.path.join(base, d)
            if os.path.isdir(full):
                dropped.append(os.path.relpath(full, src))
        rel = os.path.relpath(base, src)
        out = dst if rel == "." else os.path.join(dst, rel)
        os.makedirs(out, exist_ok=True)
        for f in sorted(files):
            if skip_file(f):
                dropped.append(os.path.join(rel, f) if rel != "." else f)
                continue
            shutil.copy2(os.path.join(base, f), os.path.join(out, f))
    return sorted(set(dropped))


def is_strippable(root, path):
    """Only mod CONFIG is stripped. Everything else is left byte for byte.

    The first run of this script processed 63 XML files rather than 41, because it also
    caught the five worlds' prefabs.xml, map_info.xml, spawnpoints.xml and water_info.xml,
    and the hospital prefab definition. It removed 728 bytes of comments from them and the
    shape check proved nothing else went, so nothing was damaged. It should still not have
    touched them: world and prefab data are not ours to rewrite while packaging, and a file
    that is never modified cannot be modified wrongly.
    """
    rel = os.path.relpath(path, root).replace("\\", "/").split("/")
    if rel[-1].lower() == "modinfo.xml" and len(rel) == 2:
        return True                      # <Mod>/ModInfo.xml
    return "Config" in rel[:-1]          # anything under a Config folder


def strip_comments(root):
    """Remove every XML comment in the mod config, and prove nothing else went.

    A regex that eats comments can in principle eat something else, so every stripped file
    is re-parsed and compared to its original element by element: tag, attributes, text,
    children, in order. ElementTree drops comments on both sides, so the two trees match if
    and only if nothing but comments was removed. The stripping is three lines; this check
    is the reason it is safe to do at all.
    """
    before = after = 0
    count = 0
    failures = []
    for base, _dirs, files in os.walk(root):
        for f in sorted(files):
            if not f.lower().endswith(".xml"):
                continue
            path = os.path.join(base, f)
            if not is_strippable(root, path):
                continue
            with open(path, encoding="utf-8") as fh:
                original = fh.read()
            cleaned = strip(original)
            try:
                a = shape(ET.fromstring(original))
                b = shape(ET.fromstring(cleaned))
            except ET.ParseError as ex:
                failures.append("%s did not parse after stripping: %s"
                                % (os.path.relpath(path, root), ex))
                continue
            if a != b:
                failures.append("%s CHANGED SHAPE. Something other than a comment was removed."
                                % os.path.relpath(path, root))
                continue
            with open(path, "w", encoding="utf-8") as fh:
                fh.write(cleaned)
            before += len(original)
            after += len(cleaned)
            count += 1
    return count, before, after, failures


def merge_extras(root):
    """Fold playtest\\zzzz_FrilBlocks into Hospital\\Config.

    WHY. Fril, 29 Oct: he has been adding zzzz_FrilBlocks to the repo by hand before every
    push and would rather it went into the main mod, as was done for the Ostrich. Merging
    here rather than in the repo is what makes that free: his own install keeps its creative
    menu, and he never has to remember to pull anything out before working, which the mod's
    own ModInfo warns about ("Remove this mod when updating items.xml or blocks.xml").

    APPENDED AT THE END, deliberately. These are blanket xpath rules over every block, item
    and item modifier, so whatever runs last wins. Last means they also catch OURS, which is
    exactly what the separate zzzz_ folder did.

    WHAT THE MERGE COSTS HERE: nothing. The zzzz_ prefix existed so the rules loaded after
    every other mod. "Hospital" already sorts after all eight other folders in this pack, so
    the load position is unchanged. That was NOT true for the Ostrich, where merging cost the
    guarantee; see that project's make_playtest.py.
    """
    merged = []
    failures = []
    cfg_dst = os.path.join(root, "Hospital", "Config")
    if not os.path.isdir(EXTRAS):
        return merged, ["playtest/ does not exist, so zzzz_FrilBlocks was NOT merged"]
    for entry in sorted(os.listdir(EXTRAS)):
        cfg = os.path.join(EXTRAS, entry, "Config")
        if not os.path.isdir(cfg):
            continue
        for f in sorted(os.listdir(cfg)):
            if not f.lower().endswith(".xml"):
                continue
            theirs = strip(open(os.path.join(cfg, f), encoding="utf-8").read())
            mine_path = os.path.join(cfg_dst, f)

            if not os.path.exists(mine_path):
                with open(mine_path, "w", encoding="utf-8") as fh:
                    fh.write(theirs)
                merged.append("%s (new file)" % f)
                continue

            original = open(mine_path, encoding="utf-8").read()
            inner = theirs[theirs.index("<configs>") + len("<configs>"):theirs.rindex("</configs>")]
            cut = original.rindex("</configs>")
            combined = (original[:cut] + inner.strip("\n") + "\n\n</configs>"
                        + original[cut + len("</configs>"):])
            try:
                a = shape(ET.fromstring(original))
                t = shape(ET.fromstring(theirs))
                c = shape(ET.fromstring(combined))
            except ET.ParseError as ex:
                failures.append("%s did not parse after merging %s: %s" % (f, entry, ex))
                continue
            # Every one of our operations still there, in order, unchanged, with theirs after.
            if c[4] != a[4] + t[4]:
                failures.append("%s: merging %s did not produce ours followed by theirs."
                                % (f, entry))
                continue
            with open(mine_path, "w", encoding="utf-8") as fh:
                fh.write(combined)
            merged.append("%s (+%d op)" % (f, len(t[4])))
    return merged, failures


def write_zip(path, root, prefix):
    """Zip the staging tree. prefix="" puts the mod folders at the zip root; prefix="Mods"
    puts them inside one Mods folder. Those are the only two shapes that install correctly."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if os.path.exists(path):
        os.remove(path)
    names = []
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as z:
        for base, dirs, files in os.walk(root):
            dirs.sort()
            for f in sorted(files):
                full = os.path.join(base, f)
                rel = os.path.relpath(full, root).replace("\\", "/")
                arc = "%s/%s" % (prefix, rel) if prefix else rel
                z.write(full, arc)
                names.append(arc)
    return names


def main():
    if not os.path.isdir(MODS):
        raise SystemExit("FAIL no Mods folder at %s" % MODS)
    ver = version()
    staging = os.path.join(DIST, "_staging")
    if os.path.exists(staging):
        shutil.rmtree(staging)
    os.makedirs(staging)

    print("Hospital Challenge v%s" % ver)
    print("  repo    : %s" % REPO)
    print("  copying Mods\\ to staging, this is the slow part on a 950MB tree ...")
    dropped = copy_tree(MODS, staging)

    count, before, after, failures = strip_comments(staging)
    if failures:
        shutil.rmtree(staging)
        for f in failures:
            print("  FAIL " + f)
        raise SystemExit("\nFAIL nothing written. The comment strip is not provably safe.")

    merged, failures = merge_extras(staging)
    if failures:
        shutil.rmtree(staging)
        for f in failures:
            print("  FAIL " + f)
        raise SystemExit("\nFAIL nothing written. The merge is not provably safe.")

    # Last line of defence. If any of these is in the staging tree the release is wrong,
    # and it is cheaper to fail here than to find out from a player.
    leaked = []
    for base, _dirs, files in os.walk(staging):
        for f in files:
            rel = os.path.relpath(os.path.join(base, f), staging)
            if skip_file(f) or any(p in rel.split(os.sep) for p in SKIP_DIRS):
                leaked.append(rel)
    if leaked:
        shutil.rmtree(staging)
        for f in leaked:
            print("  FAIL would have shipped %s" % f)
        raise SystemExit("\nFAIL nothing written.")

    nexus = write_zip(os.path.join(DIST, "nexus", "Mods.zip"), staging, "")
    launcher = write_zip(os.path.join(DIST, "launcher", "Mods.zip"), staging, "Mods")

    folders = sorted(d for d in os.listdir(staging) if os.path.isdir(os.path.join(staging, d)))
    print()
    print("  mod folders   : %d  (%s)" % (len(folders), ", ".join(folders)))
    print("  xml stripped  : %d file(s), %s -> %s bytes (%.0f%% was comment)"
          % (count, format(before, ","), format(after, ","),
             100.0 * (before - after) / before if before else 0))
    print("  verified      : every file re-parsed and compared to its original, shapes identical")
    if merged:
        print("  merged in     : %s" % ", ".join(merged))
    if dropped:
        print("  held back     : %s" % ", ".join(dropped))
    print()
    print("  dist\\nexus\\Mods.zip      %s bytes" % format(
        os.path.getsize(os.path.join(DIST, "nexus", "Mods.zip")), ","))
    for n in nexus[:3]:
        print("      %s" % n)
    print("  dist\\launcher\\Mods.zip   %s bytes" % format(
        os.path.getsize(os.path.join(DIST, "launcher", "Mods.zip")), ","))
    for n in launcher[:3]:
        print("      %s" % n)
    print()
    print("  Check those two lists. Nexus entries must start with a mod folder name,")
    print("  launcher entries must all start with Mods/. Nothing else installs correctly.")
    shutil.rmtree(staging)
    return 0


if __name__ == "__main__":
    sys.exit(main())
