"""Store ratings that the 8 reference cubes contradict, to read again after the review of each year.

    python -I cube_presence.py BULK_GZ STORE_DIR CUBES_DIR

CUBES_DIR holds the lists of fetch_cubes.py. Three lists, printed for a human to read:
  low      - rated 1-2, in at least 3 cubes
  five     - rated 5, in no cube (tokens left out: no cube lists them)
  unrated  - not rated, in at least 3 cubes

Presence in the cubes says "pool or not", not which rating: on 2026-11-25, 83% of the rated
cards in 3 cubes were rated 3 or more, yet the commonest rating for each number of cubes
guessed the exact rating no better than "always 1". The lists are cards to look at, not
proposals.
"""
import sys
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from history_lib import load_oracle, load_store, name_index  # noqa: E402

CUBES = [("modovintage", "Vintage"), ("LSVCube", "LSV"), ("5d617ac6c2a85f3b75fe95a4", "Pauper"),
         ("gianpeasant", "Peasant"), ("2hgcb", "2HG"), ("42a6g", "2HG-BBD"), ("commandercube", "Commander"),
         ("thegiantcube", "Giant")]
MIN_CUBES = 3


def read_mainboard(path):
    names = []
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line == "# mainboard":
            continue
        if line.startswith("#"):
            break
        if line:
            names.append(line)
    return names


def find_oracle_id(name, exact, front):
    """The same lookup as limited_history.py: exact name, front face, first half of a split name."""
    if name in exact:
        return exact[name]
    if name in front:
        return front[name]
    if " // " in name:
        first = name.split(" // ")[0]
        return exact.get(first) or front.get(first)
    return None


def main():
    bulk_path, store_dir, cubes_dir = sys.argv[1:4]
    exact, front = name_index(load_oracle(bulk_path))
    store, _ = load_store(store_dir)

    cubes_of = defaultdict(set)
    for cube_id, label in CUBES:
        missing = []
        for name in read_mainboard(Path(cubes_dir) / f"{cube_id}.txt"):
            oracle_id = find_oracle_id(name, exact, front)
            if oracle_id:
                cubes_of[oracle_id].add(label)
            else:
                missing.append(name)
        if missing:
            print(f"{label}: {len(missing)} names not in the bulk: {', '.join(missing[:5])}")

    def cubes(oracle_id):
        return cubes_of.get(oracle_id, set())

    def name(oracle_id):
        status = store[oracle_id].get("status")
        return store[oracle_id]["name"] + (f" [{status}]" if status else "")

    def most_cubes_first(oracle_id):
        return -len(cubes(oracle_id)), store[oracle_id]["name"]

    rated = [oracle_id for oracle_id, entry in store.items() if entry["rating"] >= 1]
    unrated = [oracle_id for oracle_id, entry in store.items() if entry["rating"] == 0]
    print(f"store {len(store)}: rated {len(rated)}, unrated {len(unrated)}")

    low = sorted((o for o in rated if store[o]["rating"] <= 2 and len(cubes(o)) >= MIN_CUBES), key=most_cubes_first)
    print(f"\n== low: {len(low)} rated 1-2 in at least {MIN_CUBES} cubes")
    for o in low:
        print(f"{store[o]['rating']}\t{len(cubes(o))}\t{name(o)}\t{', '.join(sorted(cubes(o)))}")

    five = sorted((o for o in rated if store[o]["rating"] == 5 and not cubes(o) and store[o].get("status") != "Token"),
                  key=lambda o: (store[o].get("status") or "", store[o]["name"]))
    print(f"\n== five: {len(five)} rated 5 in no cube")
    for o in five:
        print(name(o))

    by_count = Counter(len(cubes(o)) for o in unrated if cubes(o))
    in_cubes = sorted((o for o in unrated if len(cubes(o)) >= MIN_CUBES), key=most_cubes_first)
    print(f"\n== unrated: {len(in_cubes)} not rated, in at least {MIN_CUBES} cubes")
    print("unrated by number of cubes: " + ", ".join(f"{k}: {by_count[k]}" for k in sorted(by_count)))
    for o in in_cubes:
        print(f"{len(cubes(o))}\t{name(o)}\t{', '.join(sorted(cubes(o)))}")


if __name__ == "__main__":
    main()
