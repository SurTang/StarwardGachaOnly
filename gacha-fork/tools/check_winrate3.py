import sqlite3
import sys

db = sys.argv[1] if len(sys.argv) > 1 else r"C:\Users\tang\AppData\Local\MihoyoWishBook\StarwardDatabase.db"
c = sqlite3.connect(db)
STANDARD = {"迪卢克", "琴", "莫娜", "七七", "刻晴", "提纳里", "迪希雅"}

rows = c.execute(
    "SELECT Id, Name, GachaType FROM GenshinGachaItem WHERE Uid=110369802 AND RankType=5 AND GachaType IN (301,400) ORDER BY Id;"
).fetchall()

# app-like IsUp: 301 items use the no-up list (standard pool); 400 (集录祈愿) has no entry, so the
# app marks every one of its items as UP
def app_is_up(row):
    return True if row[2] == 400 else (row[1] not in STANDARD)


def rate(seq, label, is_up_fn):
    flips = wins = 0
    prev = None
    for row in seq:
        up = is_up_fn(row)
        if prev is None or prev is True:
            flips += 1
            if up:
                wins += 1
        prev = up
    print(f"{label}: n={len(seq)} flips={flips} wins={wins} rate={(wins / flips) if flips else 0:.1%}")


rate(rows, "merged, app-like IsUp, single state machine", app_is_up)


def rate_per_group(seq, label, is_up_fn):
    flips = wins = 0
    for gtype in sorted({r[2] for r in seq}):
        sub = [r for r in seq if r[2] == gtype]
        prev = None
        for row in sub:
            up = is_up_fn(row)
            if prev is None or prev is True:
                flips += 1
                if up:
                    wins += 1
            prev = up
    print(f"{label}: flips={flips} wins={wins} rate={(wins / flips) if flips else 0:.1%}")


rate_per_group(rows, "merged, app-like IsUp, per gacha type", app_is_up)
rate_per_group(rows, "merged, name-based IsUp, per gacha type", lambda r: r[1] not in STANDARD)
