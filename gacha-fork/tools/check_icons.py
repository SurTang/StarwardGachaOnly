import sqlite3
import sys

db = sys.argv[1] if len(sys.argv) > 1 else r"C:\Users\tang\AppData\Local\MihoyoWishBook\StarwardDatabase.db"
c = sqlite3.connect(db)

for t in ("GenshinGachaInfo", "StarRailGachaInfo", "ZZZGachaInfo"):
    try:
        cols = [r[1] for r in c.execute(f"PRAGMA table_info({t});")]
        total = c.execute(f"SELECT COUNT(*) FROM {t};").fetchone()[0]
        empty = c.execute(f"SELECT COUNT(*) FROM {t} WHERE Icon IS NULL OR Icon = '';").fetchone()[0] if "Icon" in cols else "n/a"
        print(f"{t}: cols={cols} total={total} empty_icon={empty}")
    except sqlite3.Error as ex:
        print(f"{t}: {ex}")

print("--- sample icons ---")
for row in c.execute("SELECT Id, Name, Icon FROM GenshinGachaInfo WHERE Icon IS NOT NULL AND Icon <> '' LIMIT 3;"):
    print(row)
for row in c.execute("SELECT Id, Name, Icon FROM StarRailGachaInfo WHERE Icon IS NOT NULL AND Icon <> '' LIMIT 2;"):
    print(row)
for row in c.execute("SELECT Id, Name, Icon FROM ZZZGachaInfo WHERE Icon IS NOT NULL AND Icon <> '' LIMIT 2;"):
    print(row)
