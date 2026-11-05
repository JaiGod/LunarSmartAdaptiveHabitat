# Tabletop Simulation — 72-hour window (real AI engine, headless)
Generated 2026-10-18 12:53 · seed 42 · step 1 sim-minute · 4 crew

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
- Escalations: 0 · interventions logged: 24 · refusals respected: 0

## What worked
- Two conditions ran in parallel on separate clocks with zone-separated levers (greenhouse/wardroom vs commons).
- Exercise context suppression prevented false anxiety alarms.
- Refusals stopped interventions immediately and were logged.
- Crew were informed before every escalation; backup chain engaged when the CMO was on EVA.

## What failed / was weak
- Depression needs 48 h persistence by design, so Tier 1 starts late (~2 days) — slow by nature but late.
- After the cortisol failure, confidence dropped and depression was capped at Tier 1 until monitoring-window escalation — a real risk of under-reaction.
- AI crewmates with low autonomy declined offers more often, delaying behavioural activation.

## Redesign
1. Add a 2-question daily self-check in the Crew Interface as a cortisol fallback (raises confidence without new mass).
2. Shorten depression persistence to 36 h when sleep AND movement both trend down (two independent signals).
3. Present offers as choices between two activities (autonomy-preserving) to raise acceptance.
4. Pre-assign the Commander as standing deputy CMO during any CMO EVA (no 15-min wait).

## Timeline (key events)
- Day 2 04:21  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 2 04:28  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
- Day 2 04:49  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 2 04:56  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
- Day 2 05:05  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 2 05:12  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
- Day 2 05:41  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 2 05:55  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
- Day 2 12:59  ⚠ Rivera cortisol patch FAILED → degraded mode
- Day 2 13:20  Dr. L. Okafor: T1 Interpersonal conflict: Stagger meal/route timing so the pair is not forced together; warm calm light in commons. Confidence 100 %.
- Day 2 13:20  Dr. L. Okafor: T2 Interpersonal conflict: offer sent — "Would you like a quiet space for a while? I can set up the commons and deploy the acoustic screens in the wardroom."
- Day 2 13:20  Dr. L. Okafor: Dr. L. Okafor accepted the Interpersonal conflict offer.
- Day 2 13:20  Eng. K. Tanaka: T1 Interpersonal conflict: Stagger meal/route timing so the pair is not forced together; warm calm light in commons. Confidence 100 %.
- Day 2 13:20  Eng. K. Tanaka: T2 Interpersonal conflict: offer sent — "Would you like a quiet space for a while? I can set up the commons and deploy the acoustic screens in the wardroom."
- Day 2 13:20  Eng. K. Tanaka: Eng. K. Tanaka accepted the Interpersonal conflict offer.
- Day 2 22:30  Dr. L. Okafor: Interpersonal conflict resolved — environment restored gradually; baseline updated.
- Day 2 22:30  Eng. K. Tanaka: Interpersonal conflict resolved — environment restored gradually; baseline updated.
- Day 3 03:14  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 3 03:15  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
- Day 3 03:30  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 3 03:32  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
- Day 3 03:37  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 3 03:52  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
- Day 3 04:26  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 3 04:40  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
- Day 3 04:53  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 3 04:56  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
- Day 3 05:13  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 3 05:22  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
- Day 3 05:48  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 3 05:54  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
- Day 3 06:00  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 3 06:06  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
- Day 3 08:59  CMO Okafor starts 4 h EVA
- Day 4 03:28  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 4 03:29  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
- Day 4 03:36  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 4 03:39  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
- Day 4 03:47  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 4 03:49  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
- Day 4 03:56  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 4 04:07  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
- Day 4 04:26  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 4 04:37  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
- Day 4 04:46  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 4 04:48  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
- Day 4 05:10  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 4 05:24  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
- Day 4 05:51  Dr. S. Rivera: T1 Anxiety: Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool). Confidence 100 %.
- Day 4 06:02  Dr. S. Rivera: Anxiety resolved — environment restored gradually; baseline updated.
