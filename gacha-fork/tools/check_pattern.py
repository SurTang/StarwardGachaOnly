import sqlite3
import sys

db = sys.argv[1] if len(sys.argv) > 1 else r"C:\Users\tang\AppData\Local\MihoyoWishBook\StarwardDatabase.db"
c = sqlite3.connect(db)

# exact replica of the app rule for hk4e301 (GachaNoUp)
NOUP = {
    10000003: [("2020-09-01", None)],
    10000016: [("2020-09-01", None)],
    10000035: [("2020-09-01", None)],
    10000041: [("2020-09-01", None)],
    10000042: [("2020-09-01", "2021-02-17 17:59:59"), ("2021-03-02 16:00:00", None)],
    10000069: [("2022-09-27 18:00:00", None)],
    10000079: [("2023-04-11 18:00:00", None)],
    10000109: [("2025-03-25 18:00:00", None)],
}


def is_up(item_id, time_str):
    ranges = NOUP.get(item_id)
    if not ranges:
        return True
    for start, end in ranges:
        if time_str >= start and (end is None or time_str <= end):
            return False
    return True


rows = c.execute(
    "SELECT Id, ItemId, Time, GachaType FROM GenshinGachaItem WHERE Uid=110369802 AND RankType=5 AND GachaType IN (301,400) ORDER BY Time;"
).fetchall()
pattern = "".join("U" if is_up(r[1], r[2]) else "-" for r in rows)
print("app-rule pattern (by Time):", pattern)
print("up:", pattern.count("U"), "non-up:", pattern.count("-"))


def flips_wins(pairs):
    flips = wins = 0
    prev = False
    for up in pairs:
        if not prev:
            flips += 1
            if up:
                wins += 1
        prev = up
    return flips, wins


ups = [ch == "U" for ch in pattern]
print("single state machine:", flips_wins(ups))
by_type = {}
for r, u in zip(rows, ups):
    by_type.setdefault(r[3], []).append(u)
total = [0, 0]
for t, seq in by_type.items():
    f, w = flips_wins(seq)
    print(f"type {t}: n={len(seq)} flips={f} wins={w}")
    total[0] += f
    total[1] += w
print("per type total:", total, f"{total[1]/total[0]:.1%}" if total[0] else "")
