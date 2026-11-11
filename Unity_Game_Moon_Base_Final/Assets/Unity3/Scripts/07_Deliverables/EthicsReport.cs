// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/07_Deliverables/EthicsReport.cs
//  Section : 07_Deliverables
//  Folder  : Project ARES D1–D6 inside the game: design pages, D4 ethics report, dimensioned floor
//            plan.
//  Purpose : D4 written ethics report (800–1,200 words). Shown in the game over three pages and
//            saved to Reports/D4_Ethics_Report.md.
//  Why     : Rubric D4 written report (1,155 words).
//  Contains: EthicsReport
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Linq;
using System.Text.RegularExpressions;

namespace Hunch3
{
    /// D4 written ethics report (800–1,200 words). Shown in the game over three pages and saved to Reports/D4_Ethics_Report.md.
    public static class EthicsReport
    {
        public const string Title = "D4 Ethics Report: An AI That Watches Over People Who Can't Leave";
        public static readonly string Text = "Our habitat AI is built to notice when a crew member is struggling and to change their surroundings before a small problem becomes a big one. That only works if the crew trusts it. On an 18-month mission nobody can walk away from the system, so a careless design would turn a support tool into surveillance. This report explains the four ethical tensions we ran into, the hard limits we set, how a human can always override the AI, and how these choices changed the technical design.\n\nTension 1: surveillance vs. privacy\n\nTo detect anxiety or depression early, the AI needs a lot of data. The more it senses, the better it works, but also the more it watches. We decided that monitoring stops at the pod door. Private pods, hygiene and medical have no cameras and no microphones, only a door sensor that says someone is inside. Voice and face analysis only happen in shared rooms, and the audio and video are processed on the device inside the room and then thrown away. Only numbers, like \"pitch variance down 30 %\", ever leave the room. Wearables go wherever the person goes, so private rooms don't need any room sensors at all.\n\nConsent is hard when you can't really say no to the mission. Our answer is that every psychological sensor can be switched off by the crew member at any time, with no explanation needed. The only things that stay on are heart rate and CO2, because those are about physical safety, not mental health. We think that is the fair line: the AI can't help you if it can't see anything, but you decide how much it sees.\n\nTension 2: autonomy vs. intervention\n\nThe AI can make a room calmer without asking. That is helpful, but it can also feel like being managed. So we split interventions into three tiers. Tier 1 changes are small, can be undone, and are limited to three per person per day. Tier 2 is always an offer in plain words, like \"want to take a 20 minute break?\", and the person can say no. A \"no\" stops the support immediately, gets logged, and the AI waits six hours before it offers anything again. After a refusal, only a human may follow up. We also treat loss of autonomy as one of the six conditions the AI looks for, so the system has to check whether its own action gives the person more control or less. Supporting someone means giving them options. Nudging them without telling them would be manipulation, and we don't allow it.\n\nTension 3: transparency vs. distraction\n\nIf the AI announced every small light change, people would get annoyed and stop paying attention. If it hid them, it would be secretive. Our rule is that silent does not mean secret. Tier 1 changes aren't announced, but every one of them is written in the person's own log, which they can read at any time. Anyone can also switch on \"tell me about every silent change\". Board question Q3 tests this: if someone finds out about a change they didn't know about, they can see exactly what happened and why, the AI apologises, and they can turn silent changes off for themselves.\n\nTension 4: the individual vs. the crew\n\nSometimes one person's state affects everyone, for example a conflict between two people at dinner. We decided the AI acts on the environment and the schedule, never on information. It can stagger meal times or set up a second dining area, but it never tells anyone else why. Team suggestions at the daily meeting are always about the whole team and never name anyone. A crisis only becomes a crew matter at Tier 3, and then a human doctor (the CMO) makes that decision, not the AI.\n\nHard limits\n\nThese are enforced in the code, so the AI cannot break them even if its scores say it should:\n1. It never shares one person's mental health data with another crew member unless they agree.\n2. It never overrides a refusal. It stops, logs it, and only a human (the CMO) may follow up.\n3. It never touches medication, medical protocols or life-safety systems like air, hatches or power.\n4. It never alerts mission control about someone before that person has been told.\n5. It only makes changes that can be undone and that the crew can override (light, sound, temperature within 2 °C, scent, schedule offers).\n6. There are no cameras or microphones in pods, bathrooms, the VR retreat or the family-call booth.\n7. A human has to review the system at least every 7 days. If the review is overdue, the AI drops to Tier 1 and notifications only.\n8. Public indicators, like the light on someone's beacon or pod door, never show their mood unless they choose to share it.\nEvery attempt to break a limit is blocked, counted and shown on the dashboard. You can try this live in both games.\n\nHuman override\n\nEvery crew member can override the AI at any time. They can stop one support, pause everything for 24 or 72 hours, restore their room to default, mark a reading as wrong so the AI learns from it, or turn the AI off completely with one button. Turning it off undoes everything it changed, and only a human can turn it back on. We also added a night watch because the AI is the only one \"awake\" at night. If anything goes wrong while the crew sleeps (an alarm, the AI breaking a bedtime rule, a Tier 3 emergency or a failed sensor), the AI turns itself off and wakes a human, who is in charge until they let it back on. Overrides are never blocked, even during a Tier 3, because the person always has the final say over their own space.\n\nHow ethics shaped the technical design\n\nOur ethics decisions changed the engineering in real ways. Because there are no sensors in pods, we needed wearables and door sensors, which made the system lighter (about 13 kg and 110 W in total). Because refusals must be respected, the engine has a cooldown and a refusal flag for each condition. Because we would rather miss something than raise a false alarm, the AI is limited to Tier 1 whenever its confidence is below 50 %, and every condition needs more than one sensor to agree. Because people must be told first, the escalation chain goes person, then CMO, then Commander, then mission control, and the code checks that order. We ran a 72-hour tabletop simulation and 70 test missions to make sure these rules hold up when things go wrong.\n\nIn the end, we think the AI is only worth having if the crew would choose to keep it switched on. Every design choice above is meant to earn that.";
        public static int Words => Regex.Matches(Title + " " + Text, @"[A-Za-z0-9][A-Za-z0-9'’\-%.]*").Count;
        static string[] parts;
        /// the report split into 3 readable pages at section headings
        public static string Part(int i)
        {
            if (parts == null)
            {
                var paras = Text.Split(new[] { "\n\n" }, System.StringSplitOptions.None);
                int a = System.Array.FindIndex(paras, p => p.StartsWith("Tension 3")), b = System.Array.FindIndex(paras, p => p.StartsWith("Human override"));
                string Fmt(System.Collections.Generic.IEnumerable<string> ps) => string.Join("\n\n", ps.Select(p => p.Length < 60 && !p.EndsWith(".") ? "<b>" + p + "</b>" : p));
                parts = new[] { Fmt(paras.Take(a)), Fmt(paras.Skip(a).Take(b - a)), Fmt(paras.Skip(b)) };
            }
            return parts[i];
        }
        public static string Save(string dir)
        {
            string path = System.IO.Path.Combine(dir, "D4_Ethics_Report.md");
            try { System.IO.Directory.CreateDirectory(dir); System.IO.File.WriteAllText(path, "# " + Title + "\n\n" + Text + "\n\n(" + Words + " words)\n"); } catch { }
            return path;
        }
    }
}
