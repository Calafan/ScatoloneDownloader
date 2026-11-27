"""Rating inconsistencies between the cards of the store rated 1 to 5.

    python -I rating_gaps.py BULK_GZ STORE_DIR

Two checks, printed for a human to read:
  reprint  - functional reprints (same cost, type, text with the card's own name blanked,
             power, toughness and loyalty) that have different ratings
  better   - card A is card B plus an optional ability, cheaper or bigger (same type and
             colours, same coloured pips, every ability of B also on A, no added drawback),
             yet A is rated below B. "sure" when every added ability is an activated one or
             a keyword; "check" when an added triggered or static ability needs reading.

Both checks ignore creature subtypes: two cards that differ only in them are the same card
(user ruling 2026-11-27).

Split, flip and double-faced cards take part in the reprint check only.
"""
import gzip
import json
import re
import sys
from collections import defaultdict
from pathlib import Path

TIER_FILES = ("pool.json", "bench.json", "fringe.json")

DRAWBACK = re.compile(
    r"additional cost|enters tapped|^echo|cumulative upkeep|can't block|can't attack|^defender|"
    r"doesn't untap|at the beginning of your upkeep|sacrifice (it|~|this)|discard|lose|"
    r"opponent (draws|gains|creates)|each player|^suspend|^fading|^vanishing|stun counter|"
    r"can't choose|under its owner's control|any player may activate|each opponent may|"
    r"return (it|~|this \w+|an? \w+ you control) to its owner's hand|deals that much damage to you",
    re.IGNORECASE)
DRAWBACK_KEYWORDS = {"echo", "cumulative upkeep", "defender", "fading", "vanishing", "suspend"}


def load_rated(store_dir):
    rated = {}
    for name in TIER_FILES:
        doc = json.load(open(Path(store_dir) / name, encoding="utf-8"))
        for oracle_id, entry in doc["cards"].items():
            if entry["rating"] >= 1:
                rated[oracle_id] = entry
    return rated


def faces(card):
    return card.get("card_faces") or [card]


def oracle_id(card):
    return card.get("oracle_id") or faces(card)[0].get("oracle_id")


def blank_own_name(text, name):
    """Replaces the card's name, and the short name before a comma, with ~."""
    names = {name, name.split(",")[0]}
    for own in sorted(names, key=len, reverse=True):
        text = text.replace(own, "~")
    return text


def without_creature_subtypes(type_line):
    """'Creature — Human Wizard' becomes 'Creature'; any other type line stays as it is."""
    types, dash, _ = type_line.partition(" — ")
    if dash and "Creature" in types:
        return types
    return type_line


def reprint_key(card):
    parts = []
    for face in faces(card):
        parts.append((
            face.get("mana_cost", ""),
            without_creature_subtypes(face.get("type_line", "")),
            blank_own_name(face.get("oracle_text", ""), face.get("name", "")),
            face.get("power"), face.get("toughness"), face.get("loyalty"),
        ))
    return repr(parts)


def abilities(card):
    """One entry per ability: a line of the text, or one keyword of a keyword-only line."""
    keywords = {k.lower() for k in card.get("keywords", [])}
    text = blank_own_name(card.get("oracle_text", ""), card.get("name", ""))
    text = re.sub(r"\([^)]*\)", "", text)
    result = set()
    for line in text.split("\n"):
        line = line.strip()
        if not line:
            continue
        bits = [bit.strip() for bit in line.split(",")]
        if all(bit.lower() in keywords for bit in bits):
            result.update(bit.lower() for bit in bits)
        else:
            result.add(line)
    return result


def is_activated(line):
    """An activated ability is optional, so it is never a drawback: a cost, a colon, an effect.
    One that any player may activate is not optional for its controller, so it does not count."""
    if "any player may activate" in line.lower():
        return False
    head, colon, _ = line.partition(":")
    if not colon or len(head) >= 60 or '"' in head:
        return False
    return not head.lower().startswith(("as ", "when", "at ", "if "))


def is_upside_keyword(line, card):
    keywords = {k.lower() for k in card.get("keywords", [])} - DRAWBACK_KEYWORDS
    return any(line.lower().startswith(keyword) for keyword in keywords)


def split_cost(mana_cost):
    """The generic amount and the sorted coloured (and other) pips."""
    generic = 0
    pips = []
    for symbol in re.findall(r"\{([^}]+)\}", mana_cost):
        if symbol.isdigit():
            generic += int(symbol)
        else:
            pips.append(symbol)
    return generic, tuple(sorted(pips))


def number(value):
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def better_verdict(a, b):
    """'sure' or 'check' when A is B plus something and nothing worse; None otherwise."""
    generic_a, pips_a = split_cost(a.get("mana_cost", ""))
    generic_b, pips_b = split_cost(b.get("mana_cost", ""))
    if pips_a != pips_b or generic_a > generic_b:
        return None
    if not a.get("mana_cost") and "Land" not in a.get("type_line", ""):
        return None

    abilities_a, abilities_b = abilities(a), abilities(b)
    if not abilities_b <= abilities_a:
        return None
    added = abilities_a - abilities_b
    if any(DRAWBACK.search(line) and not is_activated(line) for line in added):
        return None

    power_a, toughness_a = number(a.get("power")), number(a.get("toughness"))
    power_b, toughness_b = number(b.get("power")), number(b.get("toughness"))
    if (power_a is None) != (power_b is None) or (toughness_a is None) != (toughness_b is None):
        return None
    bigger = False
    if power_a is not None:
        if power_a < power_b or toughness_a < toughness_b:
            return None
        bigger = power_a > power_b or toughness_a > toughness_b

    if not (generic_a < generic_b or added or bigger):
        return None
    if all(is_activated(line) or is_upside_keyword(line, a) for line in added):
        return "sure"
    return "check"


def main():
    bulk_path, store_dir = sys.argv[1:3]
    rated = load_rated(store_dir)
    with gzip.open(bulk_path, "rt", encoding="utf-8") as handle:
        cards = [card for card in map(json.loads, handle) if oracle_id(card) in rated]

    def rating(card):
        return rated[oracle_id(card)]["rating"]

    def label(card):
        status = rated[oracle_id(card)].get("status")
        return f"{rating(card)} {card['name']}" + (f" [{status}]" if status else "")

    print(f"rated cards: {len(rated)}, found in the bulk: {len(cards)}")

    groups = defaultdict(list)
    for card in cards:
        groups[reprint_key(card)].append(card)
    reprints = [group for group in groups.values() if len({rating(card) for card in group}) > 1]
    print(f"\n== reprint: {len(reprints)} groups with different ratings")
    for group in reprints:
        print("\t".join(label(card) for card in sorted(group, key=rating, reverse=True)))

    by_shape = defaultdict(list)
    for card in cards:
        if "card_faces" not in card:
            by_shape[(without_creature_subtypes(card.get("type_line", "")), tuple(card.get("colors", [])))].append(card)
    pairs = []
    for group in by_shape.values():
        for a in group:
            for b in group:
                if a is b or rating(a) >= rating(b):
                    continue
                verdict = better_verdict(a, b)
                if verdict:
                    pairs.append((verdict, a, b))
    print(f"\n== better: {len(pairs)} cards rated below a card they improve on")
    for verdict, a, b in sorted(pairs, key=lambda p: (p[0] != "sure", rating(p[1]) - rating(p[2]))):
        added = " | ".join(sorted(abilities(a) - abilities(b)))
        print(f"{verdict}\t{label(a)}  <  {label(b)}\tadded: {added[:120]}")


if __name__ == "__main__":
    main()
