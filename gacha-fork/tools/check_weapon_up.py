import sqlite3
import sys

db = sys.argv[1] if len(sys.argv) > 1 else r"C:\Users\tang\AppData\Local\MihoyoWishBook\StarwardDatabase.db"
c = sqlite3.connect(db)


def five(uid, gtype):
    return c.execute(
        "SELECT Id, Name FROM GenshinGachaItem WHERE Uid=? AND RankType=5 AND GachaType=? ORDER BY Id;",
        (uid, gtype),
    ).fetchall()


uid = 110369802
perm = five(uid, 200)
char = five(uid, 301)
weapon = five(uid, 302)
standard = {n for _, n in perm}
print("permanent 5★ pool (%d):" % len(standard), sorted(standard))
print()
print("weapon banner 5★ (%d):" % len(weapon))
for _, n in weapon:
    print(f"   {'UP ' if n not in standard else 'off'}  {n}")


def rate(seq, standard_set, label):
    flips = wins = 0
    prev_up = None
    for _, n in seq:
        up = n not in standard_set
        if prev_up is None or prev_up is True:
            flips += 1
            if up:
                wins += 1
        prev_up = up
    print(f"{label}: n={len(seq)} up={sum(1 for _, n in seq if n not in standard_set)} flips={flips} wins={wins} "
          f"rate={(wins / flips) if flips else 0:.1%}")


rate(weapon, standard, "weapon banner (standard pool from permanent)")
rate(char, standard, "character banner (same standard-pool rule, for reference)")
