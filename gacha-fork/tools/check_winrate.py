import sqlite3
import sys
from collections import Counter

db = sys.argv[1] if len(sys.argv) > 1 else r"C:\Users\tang\AppData\Local\MihoyoWishBook\StarwardDatabase.db"
c = sqlite3.connect(db)

STANDARD = {"迪卢克", "琴", "莫娜", "七七", "刻晴", "提纳里", "迪希雅"}

print("gacha types in GenshinGachaItem:", c.execute(
    "SELECT GachaType, COUNT(*) FROM GenshinGachaItem WHERE Uid=110369802 GROUP BY GachaType;").fetchall())

rows = c.execute(
    "SELECT Id, Name, Time, GachaType FROM GenshinGachaItem WHERE Uid=110369802 AND RankType=5 ORDER BY Id;").fetchall()
print("total 5* rows:", len(rows))

for gtype in sorted({r[3] for r in rows}):
    seq = [r for r in rows if r[3] == gtype]
    if len(seq) < 5:
        continue
    up_count = sum(1 for r in seq if r[1] not in STANDARD)
    # model: a 5* is a coin flip unless the previous 5* was NOT up (then it is guaranteed)
    flips = wins = 0
    prev_up = None
    for r in seq:
        is_up = r[1] not in STANDARD
        if prev_up is None or prev_up is True:
            flips += 1
            if is_up:
                wins += 1
        prev_up = is_up
    print(f"gachaType={gtype} n={len(seq)} up={up_count} non_up={len(seq)-up_count} "
          f"flips={flips} wins={wins} rate={wins/flips:.1%}" if flips else f"gachaType={gtype} n={len(seq)} no flips")

# how often does a non-up follow a non-up (impossible under the model)?
seq = [r for r in rows if r[3] == 301]
bad = 0
for a, b in zip(seq, seq[1:]):
    if a[1] in STANDARD and b[1] in STANDARD:
        bad += 1
print("type 301 consecutive non-up pairs (model violations):", bad)
print("names:", Counter(r[1] for r in seq).most_common(12))
