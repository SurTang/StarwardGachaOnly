import sqlite3
import sys

db = sys.argv[1] if len(sys.argv) > 1 else r"C:\Users\tang\AppData\Local\MihoyoWishBook\StarwardDatabase.db"
c = sqlite3.connect(db)

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
STANDARD = {"迪卢克", "琴", "莫娜", "七七", "刻晴", "提纳里", "迪希雅", "梦见月瑞希"}


def dict_up(item_id, t):
    ranges = NOUP.get(item_id)
    if not ranges:
        return True
    for start, end in ranges:
        if t >= start and (end is None or t <= end):
            return False
    return True


rows = c.execute(
    "SELECT Id, ItemId, Name, Time, GachaType FROM GenshinGachaItem WHERE Uid=110369802 AND RankType=5 AND GachaType IN (301,400) ORDER BY Id;"
).fetchall()
p_dict = "".join("U" if dict_up(r[1], r[3]) else "-" for r in rows)
p_name = "".join("U" if r[2] not in STANDARD else "-" for r in rows)
print("dict:", p_dict)
print("name:", p_name)
diff = [i for i, (a, b) in enumerate(zip(p_dict, p_name)) if a != b]
print("differing positions:", len(diff))

# names that the dict marks as UP but the name rule marks non-UP, and vice versa
for i in diff[:20]:
    r = rows[i]
    print(f"  pos {i}: id={r[1]} name={r[2]!r} time={r[3]} type={r[4]} dict={p_dict[i]} name={p_name[i]}")


def flips_wins(pattern, label):
    flips = wins = 0
    prev = False
    for ch in pattern:
        up = ch == "U"
        if not prev:
            flips += 1
            if up:
                wins += 1
        prev = up
    print(f"{label}: flips={flips} wins={wins} rate={wins / flips:.1%}" if flips else label)


flips_wins(p_dict, "dict rule")
flips_wins(p_name, "name rule")
