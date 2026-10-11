import sqlite3
import sys

db = sys.argv[1] if len(sys.argv) > 1 else r"C:\Users\tang\AppData\Local\MihoyoWishBook\StarwardDatabase.db"
out = sys.argv[2] if len(sys.argv) > 2 else "weapon-check.txt"
c = sqlite3.connect(db)

STANDARD_WEAPONS = {
    "天空之刃", "天空之傲", "天空之翼", "天空之卷", "天空之脊",
    "狼的末路", "四风原典", "阿莫斯之弓", "和璞鸢", "风鹰剑",
}
STANDARD_CHARS = {"迪卢克", "琴", "莫娜", "七七", "刻晴", "提纳里", "迪希雅", "梦见月瑞希"}
STANDARD = STANDARD_WEAPONS | STANDARD_CHARS

lines = []


def five(uid, gtype):
    return c.execute(
        "SELECT Id, Name FROM GenshinGachaItem WHERE Uid=? AND RankType=5 AND GachaType=? ORDER BY Id;",
        (uid, gtype),
    ).fetchall()


def report(seq, standard, label):
    off = [n for _, n in seq if n in standard]
    flips = wins = 0
    prev_up = None
    for _, n in seq:
        up = n not in standard
        if prev_up is None or prev_up is True:
            flips += 1
            if up:
                wins += 1
        prev_up = up
    lines.append(f"{label}: n={len(seq)} up={len(seq) - len(off)} off={len(off)} "
                 f"flips={flips} wins={wins} rate={(wins / flips if flips else 0):.1%}")
    lines.append(f"    off-banner items: {off}")


uid = 110369802
perm = five(uid, 200)
derived = {n for _, n in perm}
lines.append(f"permanent 5★ distinct ({len(derived)}): {sorted(derived)}")
lines.append(f"standard weapons missing from the permanent pull list: {sorted(STANDARD_WEAPONS - derived)}")
lines.append("")

weapon = five(uid, 302)
report(weapon, STANDARD, "weapon banner / hardcoded standard pool")
report(weapon, derived, "weapon banner / pool derived from permanent pulls")
lines.append("")
chars = five(uid, 301)
report(chars, STANDARD_CHARS, "character banner / hardcoded standard characters")
report(chars, derived, "character banner / pool derived from permanent pulls")

with open(out, "w", encoding="utf-8") as fh:
    fh.write("\n".join(lines) + "\n")
print("written", out)
