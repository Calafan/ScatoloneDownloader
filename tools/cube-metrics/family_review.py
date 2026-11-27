"""Families of cards to rate together, listed by library year, to read before the review of each year.

    python -I family_review.py BULK_GZ STORE_DIR LIBRARY_DIR OUT_MD

LIBRARY_DIR is the library's Source folder (<year>/<set>/<card>.png): a card's year and set are
those of its image. Two kinds of family, nonland cards only, creature subtypes ignored (user
ruling 2026-11-27):
  text   - the same text once colours, mana symbols and basic land types are blanked: the same
           card in another colour (Wrath of God and Damnation, River Boa and Mire Boa)
  cycle  - a design cycle of one set: 3+ cards of the same library set, front type, mana value
           and number of colours, with at least 3 different colours and a common part of the
           name ("Dictate of ...", "... Charm"), not already one text family

The colour counts in a rating (Stone Rain 4, Ice Storm 5: user ruling 2026-11-25), so the list
proposes no rating. Each family with an unrated card is listed under the library year of that
card, next to the ratings its other cards already have; a year rated changes those ratings, so
run it again before the next year.
"""
import gzip
import json
import re
import sys
import unicodedata
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from history_lib import load_store  # noqa: E402
from rating_gaps import blank_own_name, faces, oracle_id, without_creature_subtypes  # noqa: E402

SKIP_LAYOUTS = {"art_series", "token", "double_faced_token", "emblem"}
BASIC = re.compile(r"\b(Plains|Island|Swamp|Mountain|Forest)s?\b")
LANDWALK = re.compile(r"\b(plains|island|swamp|mountain|forest)(walk|cycling)\b", re.IGNORECASE)
COLOUR = re.compile(r"\b(non-?)?(white|blue|black|red|green)\b", re.IGNORECASE)
MANA = re.compile(r"\{([WUBRG])(/[WUBRGP])?\}")
ARTICLES = {"The", "A", "An"}


def collapse(name):
    """The key of ScatoloneDownloader's CardNameKey.Collapse: lowercase ASCII letters and digits only,
    so "Summon: Choco/Mog" meets its file "Summon ChocoMog.png" and "A // B" meets "A_B.png"."""
    decomposed = unicodedata.normalize("NFD", name).lower()
    return "".join(ch for ch in decomposed if "a" <= ch <= "z" or "0" <= ch <= "9")


def library_index(library_dir):
    """collapsed card name -> (year, set); a key shared by two files is left out."""
    index, shared = {}, set()
    for image in Path(library_dir).glob("*/*/*.png"):
        key = collapse(image.stem)
        if key in index:
            shared.add(key)
        index[key] = (image.parts[-3], image.parts[-2])
    for key in shared:
        del index[key]
    return index


def blank_colours(text, name):
    text = blank_own_name(text, name)
    text = re.sub(r"\([^)]*\)", "", text)
    text = LANDWALK.sub(r"BASIC\2", text)
    text = BASIC.sub("BASIC", text).replace("an BASIC", "a BASIC")
    text = COLOUR.sub(lambda m: (m.group(1) or "") + "COLOUR", text)
    return MANA.sub("{M}", text).strip()


def text_key(card):
    parts = []
    for face in faces(card):
        parts.append((MANA.sub("{M}", face.get("mana_cost", "")),
                      BASIC.sub("BASIC", without_creature_subtypes(face.get("type_line", ""))),
                      blank_colours(face.get("oracle_text", ""), face.get("name", "")),
                      face.get("power"), face.get("toughness"), face.get("loyalty")))
    return repr(parts)


def colour_signal(card):
    """The card's colours; for a colourless card, ~ and the colours of the mana it makes."""
    colours = card.get("colors")
    if colours is None:
        colours = sorted({c for face in faces(card) for c in face.get("colors", [])})
    if colours:
        return "".join(colours)
    return "~" + "".join(sorted(set(card.get("produced_mana", [])) - {"C"}))


def name_parts(name):
    """The parts of a name a cycle can share: before " of ", the last word, the first word."""
    base = name.split(" // ")[0].split(",")[0]
    words = base.split()
    parts = []
    if " of " in base:
        parts.append(base.split(" of ")[0] + " of ...")
    if len(words) >= 2:
        parts.append("... " + words[-1])
        if words[0] not in ARTICLES:
            parts.append(words[0] + " ...")
    return parts


def is_cycle(members):
    colours = {colour_signal(card) for card in members}
    return len(members) >= 3 and len(colours) >= 3 and len(colours) >= 0.6 * len(members)


def main():
    bulk_path, store_dir, library_dir, out_path = sys.argv[1:5]
    store, _ = load_store(store_dir)
    place = library_index(library_dir)

    def is_family_card(card):
        entry = store.get(oracle_id(card))
        if entry is None or entry.get("status") == "Token" or card.get("layout") in SKIP_LAYOUTS:
            return False
        return "Land" not in faces(card)[0].get("type_line", "")

    with gzip.open(bulk_path, "rt", encoding="utf-8") as handle:
        cards = [card for card in map(json.loads, handle) if is_family_card(card)]

    def rating(card):
        return store[oracle_id(card)]["rating"]

    def year_and_set(card):
        return place.get(collapse(store[oracle_id(card)]["name"]), ("no library file", None))

    homeless = [card["name"] for card in cards if year_and_set(card)[1] is None]
    print(f"nonland store cards: {len(cards)}, without a library file: {len(homeless)} {homeless[:5]}")

    by_text = defaultdict(list)
    for card in cards:
        by_text[text_key(card)].append(card)
    families = [("text", None, members) for members in by_text.values() if len(members) >= 2]

    by_name = defaultdict(list)
    for card in cards:
        library_set = year_and_set(card)[1]
        if library_set is None:
            continue
        shape = (library_set, faces(card)[0].get("type_line", "").partition(" — ")[0], card.get("cmc", 0),
                 len(colour_signal(card).lstrip("~")))
        for part in name_parts(card["name"]):
            by_name[(part,) + shape].append(card)
    seen = set()
    for (part, library_set, _, mana_value, _), members in sorted(by_name.items(), key=lambda item: item[0][0]):
        ids = frozenset(oracle_id(card) for card in members)
        if ids in seen or not is_cycle(members) or len({text_key(card) for card in members}) == 1:
            continue
        seen.add(ids)
        families.append(("cycle", f"{part} ({library_set}, mana value {mana_value:g})", members))

    rows = defaultdict(list)  # year -> (kind, label, the unrated cards of that year, the other cards)
    for kind, label, members in families:
        unrated_by_year = defaultdict(list)
        for card in members:
            if rating(card) == 0:
                unrated_by_year[year_and_set(card)[0]].append(card)
        for year, unrated in unrated_by_year.items():
            others = [card for card in members if card not in unrated]
            rows[year].append((kind, label, unrated, others))

    def other_card(card):
        return f"[{rating(card)}] {card['name']} ({year_and_set(card)[0]})"

    def row_order(row):
        kind, _, unrated, others = row
        return kind != "text", not any(rating(card) for card in others), sorted(card["name"] for card in unrated)

    lines = ["# Families to rate together, by library year", "",
             "Written by `tools/cube-metrics/family_review.py`. `[n]` is the rating today, the year is the library year.",
             "`text`: the same text in other colours; `cycle`: a design cycle of one set."]
    print("year             text  cycle  cards to rate")
    for year in sorted(rows, reverse=True):
        year_rows = sorted(rows[year], key=row_order)
        to_rate = sum(len(unrated) for _, _, unrated, _ in year_rows)
        kinds = Counter(kind for kind, *_ in year_rows)
        print(f"{year:15}  {kinds['text']:4}  {kinds['cycle']:5}  {to_rate:5}")
        lines += ["", f"## {year}: {len(year_rows)} families, {to_rate} cards to rate", ""]
        for kind, label, unrated, others in year_rows:
            head = f"- {kind}" + (f" {label}" if label else "") + ": " + ", ".join(sorted(card["name"] for card in unrated))
            if others:
                others = sorted(others, key=lambda card: (-rating(card), card["name"]))
                head += " — with " + "; ".join(other_card(card) for card in others)
            lines.append(head)
    Path(out_path).write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"wrote {out_path}")


if __name__ == "__main__":
    main()
