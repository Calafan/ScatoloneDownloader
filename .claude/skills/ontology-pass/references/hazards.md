# Hazards

Each of these has actually happened during a pass. They are listed by the symptom
that identifies them, because that is how they present.

## A regex that never matches, and looks correct on screen

**Symptom:** a rule plainly should fire and does not. `Why` shows the field exists,
is non-null, and reports `match=False` against text that obviously contains the
pattern.

**Cause:** the source file holds a literal **control character** where an escape
was meant. Writing C# through a bash heredoc running Python collapses a doubled
escape one level too far — `"\\b"` reaches Python as `"\b"`, and a non-raw string
renders it as **BACKSPACE (U+0008)** in the file. The terminal swallows the byte,
so the diff looks perfect.

This shipped twice in one afternoon: once in `SpendsItselfWithoutACostLine`, which
had never fired at all, and once in the ontology sentence *describing* that first
bug.

**Fix:** use the Edit/Write tools for anything containing a regex escape — never a
Python heredoc. `SourceHygieneTests.EverySourceFile_IsFreeOfControlCharacters`
fails the build if it happens again; when that test goes red, look for the escape
that was meant to be `\b`, `\w` or `\d`.

## A pattern array built from nulls

**Symptom:** `NullReferenceException` from the classifier, or `Why` reporting
`!! NULL AT READ TIME` for a field, right after adding a shared pattern to an
array.

**Cause:** `EffectClassifier` is a partial class split across six files. Field
initialisers run in **textual order within a file**, and in **unspecified order
across the files** of a partial class. So an array that reads a field declared
lower in the same file is built before that field exists.

**Fix:** declare the shared pattern **above** the array that consumes it, with a
comment saying the placement is load-bearing — the next person will otherwise
tidy it back. The rule table itself avoids this with a static constructor, which
is the one place the order is guaranteed, and that is why it exists.

## The guard was asked of the whole card

**Symptom:** a card loses a tag it clearly earns, and `Why` shows the veto firing
on text from a completely different ability.

**Cause:** a guard asked of the whole card bills a cost to whichever line happens
to carry the words. Cryogen Relic draws when it enters and separately sacrifices
itself to stun a creature; the self-sacrifice veto read the stun line and took the
draw's tag with it.

**Fix:** ask repeatability and cost **per ability**, not per card. `Abilities(text)`
splits on newlines and stitches modal bullets back onto the trigger that introduced
them, which is needed because a bullet three lines down belongs to the trigger
above it.

## Reminder text answering for the card

**Symptom:** a tag fires on a card whose only matching words are inside brackets.

**Cause:** keyword reminder text restates an effect the card does not have — Primal
Clay explains defender with "(A creature with defender can't attack.)", and encore
spells its cost as "Exile this card from your graveyard:" which reads as a real
activated ability.

**Fix:** strip `Parenthetical` and re-ask the tag's own patterns, so a card that
really does the thing on another line keeps what it earned. Do not simply delete
the match.

## The score moved but the tag did not

**Symptom:** the per-tag line is unchanged while `exact tag-set match` moved.

**Cause:** the change landed on a different tag. The tags interact — Pacify and
Protection share prevention wording, Filter and CardAdvantage share every loot.

**Fix:** always diff the whole per-effect table, not just the tag being worked:

```powershell
diff <(sed -n '9,40p' acc-BEFORE.txt) <(sed -n '9,40p' acc-summary.txt)
```

## A test pinning an overturned ruling

**Symptom:** a test fails on a card the human has just re-ruled.

**Cause:** the tests pin rulings, and a ruling can be overturned. That is normal.

**Fix:** update the test and say in its comment that the ruling was overturned and
when — the test file is a record of decisions, so a silent edit loses the history.
Never weaken an assertion to make a test pass.

## Case-sensitive patterns written lower-case

**Symptom:** a tribal or named-card pattern matches nothing.

**Cause:** most patterns run case-insensitively, but a few are deliberately
case-sensitive. A lower-case alternative added to one of those silently never
matches.

**Fix:** spell both cases (`[Cc]reatures`) and check with `Why` that it fires.

## Numbers that drift out of a report

**Symptom:** a commit message or an answer quotes a figure that no longer holds.

**Cause:** the reviewed set grows continuously — the human tags cards between
passes — so every figure is relative to a run. A number carried over from an
earlier message is stale the moment they tag another card.

**Fix:** re-run `Score` before writing any number into a commit or a reply. This is
AGENTS.md §3: a number that ends in a commit or a report is measured, never
estimated.
