# Tabletop Simulation — 72-hour window (real AI engine, headless)
Generated 2026-10-18 17:28 · seed 42 · step 1 sim-minute · 4 crew

## Scripted events (simultaneous)
- A: Rivera — slow-building depression from hour 2
- B: Okafor vs Tanaka — interpersonal conflict from hour 20
- C: Rivera cortisol patch fails at hour 30 (degraded mode)
- D: CMO Okafor on EVA hours 50–54 (Tier 3 responder unavailable)
- Control: daily exercise sessions for all crew (must NOT cause anxiety alarms)

## Results
- Depression first detected (Tier 1): not within 72 h
- Conflict first detected (Tier 1): Day 2 13:19
- Depression Tier 3 escalation: none (resolved below Tier 3)
- Exercise false-positive anxiety offers: 0 (pass)
- Escalations: 0 · interventions logged: 4 · refusals respected: 0

## What worked
- Conflict between Okafor and Tanaka was detected at Day 2 13:19 from co-location, voice and face signals.
- Daily exercise produced 0 false anxiety offers (exercise/EVA context suppression works).
- No offers were refused in this run (AI crewmates accepted).

## What failed / was weak
- Depression was NOT detected inside 72 h — the 48 h persistence rule is too slow for this onset.
- After the cortisol patch failed, depression confidence dropped and escalation above Tier 1 was capped until enough other evidence accumulated — a real risk of under-reaction.
- No Tier-3 event occurred, so the 15-minute acknowledgement / backup chain was not exercised here — it is tested separately in board scenario Q6.

## Redesign
1. Add a 2-question daily self-check in the Crew Interface as a cortisol fallback (raises confidence without new mass).
2. Fast lane for depression: 12 h of raw signal + low activity starts silent Tier-1 support early; Tier-2 persistence 48 h → 36 h (re-tested below).
3. Present offers as choices between two activities (autonomy-preserving) to raise acceptance.
4. Pre-assign the Commander as standing deputy CMO during any CMO EVA (no 15-min wait).

## Re-test after redesign #2 (same seed, same events)
Redesign #2 as built: if the RAW depression signal stays above threshold for 12 h while activity is clearly low (or sleep AND activity both drop), silent, reversible Tier-1 support starts before the 24 h-smoothed score catches up; Tier 2+ still needs 36 h of persistence.
| Measure | Run A (original 48 h rule) | Run B (redesign #2) |
|---|---|---|
| Depression signal first above threshold | Day 2 23:01 | Day 2 23:42 |
| Depression Tier 1 support starts | not within 72 h | Day 2 16:47 |
| Highest depression tier reached | 0 | 1 |
| Rivera sleep quality / mood when the smoothed signal crossed | 0.93 / 0.10 | 0.93 / 0.16 |
| Exercise false-positive anxiety offers | 0 | 0 |
| Conflict Tier 1 | Day 2 13:19 | Day 2 13:19 |

## Timeline (key events)
- Day 2 12:59  ⚠ Rivera cortisol patch FAILED → degraded mode
- Day 2 13:20  Dr. L. Okafor: T1 Interpersonal conflict: Stagger meal/route timing so the pair is not forced together; warm calm light in commons. Confidence 100 %.
- Day 2 13:20  Dr. L. Okafor: T2 Interpersonal conflict: offer sent — "Would you like a quiet space for a while? I can set up the commons and deploy the acoustic screens in the wardroom."
- Day 2 13:20  Dr. L. Okafor: Dr. L. Okafor accepted the Interpersonal conflict offer.
- Day 2 13:20  Eng. K. Tanaka: T1 Interpersonal conflict: Stagger meal/route timing so the pair is not forced together; warm calm light in commons. Confidence 100 %.
- Day 2 13:20  Eng. K. Tanaka: T2 Interpersonal conflict: offer sent — "Would you like a quiet space for a while? I can set up the commons and deploy the acoustic screens in the wardroom."
- Day 2 13:20  Eng. K. Tanaka: Eng. K. Tanaka accepted the Interpersonal conflict offer.
- Day 2 22:30  Dr. L. Okafor: Interpersonal conflict resolved — environment restored gradually; baseline updated.
- Day 2 22:30  Eng. K. Tanaka: Interpersonal conflict resolved — environment restored gradually; baseline updated.
- Day 3 08:59  CMO Okafor starts 4 h EVA
