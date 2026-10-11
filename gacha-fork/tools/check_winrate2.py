import sqlite3
import sys

db = sys.argv[1] if len(sys.argv) > 1 else r"C:\Users\tang\AppData\Local\MihoyoWishBook\StarwardDatabase.db"
c = sqlite3.connect(db)
STANDARD = {"迪卢克", "琴", "莫娜", "七七", "刻晴", "提纳里", "迪希雅"}


def rate(seq, label):
    flips = wins = 0
    prev = None
    for row in seq:
        up = row[1] not in STANDARD
        if prev is None or prev is True:
            flips += 1
            if up:
                wins += 1
        prev = up
    print(f"{label}: n={len(seq)} flips={flips} wins={wins} rate={(wins / flips) if flips else 0:.1%}")


rows = c.execute(
    "SELECT Id, Name, GachaType FROM GenshinGachaItem WHERE Uid=110369802 AND RankType=5 AND GachaType IN (301,400) ORDER BY Id;"
).fetchall()
rate(rows, "merged 301+400 ordered by Id")
rate([r for r in rows if r[2] == 301], "301 only")
rate([r for r in rows if r[2] == 400], "400 only")
rows_t = c.execute(
    "SELECT Id, Name, GachaType FROM GenshinGachaItem WHERE Uid=110369802 AND RankType=5 AND GachaType IN (301,400) ORDER BY Time;"
).fetchall()
rate(rows_t, "merged 301+400 ordered by Time")
print("first 16 merged types:", [r[2] for r in rows][:16])
print("types interleaved:", len({r[2] for r in rows[:40]}) , "distinct in first 40")
