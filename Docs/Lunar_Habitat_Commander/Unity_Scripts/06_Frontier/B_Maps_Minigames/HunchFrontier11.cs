// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/B_Maps_Minigames/HunchFrontier11.cs
//  Section : 06_Frontier/B_Maps_Minigames
//  Folder  : Extra maps and mini-games: solar farm, mining pit, Factory Land, Star Patrol, crew
//            radio + Star Clicker, Science Academy, My Astronaut, model kits, Fun Zone, endless
//            ground, Star Store pages.
//  Purpose : Eleventh part of Moon Frontier: the SCIENCE ACADEMY (map 11) PHYSICS FORCE LAB
//            thirteen set-ups from Physics 1, Physics 2 and Physics C (mechanics, fluids,
//            electricity and magnetism).
//  Contains: HunchFrontier, Quiz, QuizBay
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

/// Eleventh part of Moon Frontier: the SCIENCE ACADEMY (map 11)
///   PHYSICS FORCE LAB   thirteen set-ups from Physics 1, Physics 2 and Physics C (mechanics, fluids, electricity and magnetism).
///                        The known forces are drawn as a free-body diagram; you choose which way the missing force points,
///                        then it is drawn in gold with its equation.
///   SPACE BIOLOGY LAB   ten specimens and questions about how living things change in space (plants, bones, muscles, DNA, cells...).
///   HALL OF SCIENCE     ten famous scientists, the twelve people who have walked on the Moon, and the NASA HUNCH exhibit.
///                        A frame shows a photo when a picture with the right name is in the Pictures folder next to the game.
public partial class HunchFrontier
{
    class Quiz { public string tag, ask, why; public string[] opt; public int right; }
    class QuizBay { public Transform stage, spin; public TextMeshPro head, ask; public TextMeshPro[] optL = new TextMeshPro[3]; public Renderer[] optR = new Renderer[3]; public Quiz[] set; public int at, right, wrong, done, baseIdx; public float nextIn; public bool answered, firstTry; public string name; }
    readonly QuizBay[] bays = new QuizBay[2]; Material quizBtn, quizGood, quizBad; public int quizRight;

    static Quiz Q(string tag, string ask, string a, string b, string c, int right, string why) { return new Quiz { tag = tag, ask = ask, opt = new[] { a, b, c }, right = right, why = why }; }
    static readonly Quiz[] PhysSet =
    {
        Q("Physics 1", "The block is resting on the table. Which way does the NORMAL force from the table push on it?", "Up", "Down", "Sideways", 0, "N = mg. At rest the net force is zero, so the table pushes up as hard as gravity pulls down."),
        Q("Physics 1", "A lamp hangs still on a rope. Which way does the TENSION in the rope pull on the lamp?", "Down", "Up, along the rope", "Sideways", 1, "T = mg. On the Moon g = 1.62 m/s^2, so the rope pulls about six times less than on Earth."),
        Q("Physics 1", "The block sits on a ramp and is NOT sliding. Which way does static FRICTION point?", "Down the slope", "Straight up", "Up the slope", 2, "f = mg sin(angle). Friction balances the part of gravity that pulls along the slope."),
        Q("Physics 1", "A crate is pushed and slides to the right. Which way does kinetic FRICTION act?", "Left, against the motion", "Right, with the motion", "Up", 0, "f = mu N. Kinetic friction always points opposite to the sliding motion."),
        Q("Physics 1", "A ball flies through the airless Moon sky. Which forces act on it in flight?", "Gravity and a forward force", "Only gravity, down", "No force at all", 1, "Nothing pushes it forward: it keeps its sideways speed (Newton's first law). a = g = 1.62 m/s^2 down."),
        Q("Physics 1", "A ball on a string moves in a circle at steady speed. Which way is the NET force on it?", "Outward", "Forward, along the path", "Toward the centre", 2, "F = m v^2 / r, toward the centre. The string's tension keeps turning the velocity."),
        Q("Physics 1", "The spring is stretched to the right. Which way does the SPRING force push the block?", "Left, back to the rest position", "Right", "Down", 0, "Hooke's law: F = -k x. The minus sign means the force points back to the rest position."),
        Q("Physics 2", "A block floats in water. Which way does the BUOYANT force act?", "Down", "Sideways", "Up", 2, "Archimedes: F = rho V g, the weight of the water pushed aside. Floating means it equals the block's weight."),
        Q("Physics 2", "Two POSITIVE charges sit near each other. Which way is the electric force on the RIGHT charge?", "Right, away from the other", "Left, toward the other", "No force", 0, "Coulomb's law: F = k q1 q2 / r^2. Like charges repel, unlike charges attract."),
        Q("Physics 2", "A NEGATIVE charge sits in an electric field E that points right. Which way is the force on it?", "Right, along E", "Left, against E", "Up", 1, "F = qE. For a negative q the force points opposite to the field."),
        Q("Physics C: E&M", "A POSITIVE charge moves right. The magnetic field B points INTO the page (x marks). Which way is the force?", "Up", "Down", "Into the page", 0, "F = q v x B (right-hand rule). The force is always sideways to the motion, so the charge moves in a circle."),
        Q("Physics C: E&M", "A wire carries current UP. The magnetic field B points RIGHT. Which way is the force on the wire?", "Up", "Out of the page", "Into the page", 2, "F = I L x B. Up crossed with right points into the page. This is how electric motors turn."),
        Q("Physics C: E&M", "Two parallel wires carry current in the SAME direction. What do they do?", "Push apart", "Pull toward each other", "Nothing", 1, "Each wire sits in the other's magnetic field: F/L = mu0 I1 I2 / (2 pi d). Same direction attracts.")
    };
    static readonly Quiz[] BioSet =
    {
        Q("Plants", "In orbit a plant cannot feel which way is down. What do its shoots use to decide which way to grow?", "Light: they grow toward it", "Nothing, they stop growing", "The station's hum", 0, "Astronauts have grown lettuce, radishes, zinnias and chile peppers on the space station under LED lamps."),
        Q("Bones", "In weightlessness bones carry no load. What happens to them without exercise?", "They grow thicker", "Nothing changes", "They lose about 1 to 1.5% of their mineral each month", 2, "The Moon has only one sixth of Earth's gravity, so a Moon crew must load their bones with exercise too."),
        Q("Muscles", "About how long do space station astronauts exercise each day to protect muscles and bones?", "10 minutes", "About 2 hours", "8 hours", 1, "They use a treadmill with harness straps, a cycle, and a machine that gives resistance without weights."),
        Q("Body fluids", "Why do astronauts' faces look puffy in orbit?", "Body fluid moves toward the head", "They eat more", "The cabin is hot", 0, "On Earth gravity pulls fluid to the legs. Without it the fluid spreads upward: puffy face, thin legs."),
        Q("DNA", "Away from Earth's magnetic field and air, what can damage the DNA in a crew member's cells?", "Moonlight", "Space radiation: cosmic rays and solar particles", "Low gravity", 1, "That is why a Moon habitat is buried under regolith, and why radiation dose is tracked for every crew member."),
        Q("Cells", "Biomedical engineers grow cells in orbit because, with nothing settling to the bottom, cells can form...", "flat sheets only", "nothing at all", "3-D clumps more like real tissue", 2, "These 3-D cultures help researchers study diseases and test medicines."),
        Q("Tiny animals", "Which tiny animal survived open space in a 2007 experiment?", "The tardigrade (water bear)", "The ant", "The goldfish", 0, "Tardigrades dry out into a resting state and can survive vacuum, cold and strong radiation."),
        Q("Body clock", "A day on the Moon lasts about 29.5 Earth days. How does a habitat keep the crew's body clock healthy?", "It keeps the lights always on", "Its lights change through a 24-hour day", "It keeps the lights always off", 1, "Bright bluish light in the 'morning' and warm dim light in the 'evening' help sleep and mood. This base does exactly that."),
        Q("Microbes", "Why is a space habitat cleaned and checked so carefully?", "Dust looks untidy", "Robots need the practice", "In spaceflight the immune system can weaken and some microbes grow differently", 2, "Crews swab surfaces and test air and water so small problems are caught early."),
        Q("Eyes", "Some astronauts on long flights notice their vision change. What is thought to cause it?", "Fluid shifting toward the head and pressing on the eye", "Bright stars", "Wearing helmets", 0, "Doctors call it SANS. Studying it is part of space medicine.")
    };

    // ------------------------------------------------------------------ building the academy
    void BuildAcademy(Transform r)
    {
        BuildExtraSounds();
        quizBtn = Lit(new Color(0.15f, 0.3f, 0.55f)); quizGood = Glow(new Color(0.3f, 1.6f, 0.5f)); quizBad = Glow(new Color(1.7f, 0.3f, 0.25f));
        bays[0] = BuildBay(r, new Vector3(-16f, 0f, 14f), "PHYSICS FORCE LAB", PhysSet, 1000);
        bays[1] = BuildBay(r, new Vector3(16f, 0f, 14f), "SPACE BIOLOGY LAB", BioSet, 1010);
        BuildHall(r);
        var sign = Label(r, "<b>SCIENCE ACADEMY</b>\n<size=50%>Left: force diagrams.   Right: biology in space.   Walls: scientists, Moon walkers and NASA HUNCH.</size>", 0f, 0f, 7f, 70f, 6f, new Color(1f, 0.84f, 0.3f)); sign.transform.localPosition = new Vector3(0f, 12.5f, 30f); mapLabels.Add(sign);
        A("Force master", "Answer all thirteen force diagrams in the Physics Force Lab.", () => bays[0] != null && bays[0].done == (1 << PhysSet.Length) - 1);
        A("Space biologist", "Answer all ten questions in the Space Biology Lab.", () => bays[1] != null && bays[1].done == (1 << BioSet.Length) - 1);
        A("Hall visitor", "Read about five people in the Hall of Science.", () => Bits(hallSeen) >= 5);
    }

    QuizBay BuildBay(Transform r, Vector3 at, string name, Quiz[] set, int baseIdx)
    {
        var b = new QuizBay { set = set, baseIdx = baseIdx, name = name };
        var root = new GameObject(name).transform; root.SetParent(r, false); root.localPosition = at;
        Prim(PrimitiveType.Cube, root, new Vector3(0f, 4.4f, 0.3f), new Vector3(12.6f, 7.4f, 0.2f), Lit(new Color(0.07f, 0.09f, 0.14f)));
        Prim(PrimitiveType.Cube, root, new Vector3(0f, 8.2f, 0.3f), new Vector3(12.8f, 0.2f, 0.3f), Lit(new Color(0.95f, 0.74f, 0.22f)));
        b.head = Label(root, "", 0f, 0f, 3.4f, 12f, 0.7f, new Color(1f, 0.84f, 0.3f)); b.head.transform.localPosition = new Vector3(0f, 7.7f, 0.15f);
        b.ask = Label(root, "", 0f, 0f, 2.6f, 12f, 1.5f, Color.white); b.ask.transform.localPosition = new Vector3(0f, 1.7f, 0.1f); b.ask.enableAutoSizing = true; b.ask.fontSizeMin = 1.3f; b.ask.fontSizeMax = 2.8f;
        b.stage = new GameObject("Stage").transform; b.stage.SetParent(root, false);
        for (int i = 0; i < 3; i++)
        {
            var g = FacButton(root, new Vector3(-4.1f + i * 4.1f, 0.5f, -1.0f), new Vector3(3.9f, 0.9f, 0.5f), quizBtn, baseIdx + i); b.optR[i] = g.GetComponent<Renderer>();
            b.optL[i] = Label(root, "", 0f, 0f, 1.8f, 3.7f, 0.85f, Color.white); b.optL[i].transform.localPosition = new Vector3(-4.1f + i * 4.1f, 0.5f, -1.27f); b.optL[i].enableAutoSizing = true; b.optL[i].fontSizeMin = 0.9f; b.optL[i].fontSizeMax = 1.9f;
        }
        QuizShow(b, 0); return b;
    }

    void QuizShow(QuizBay b, int k)
    {
        b.at = (k % b.set.Length + b.set.Length) % b.set.Length; b.answered = false; b.firstTry = true; b.nextIn = 0f; var q = b.set[b.at];
        b.head.text = "<b>" + b.name + "</b>   " + q.tag + "   " + (b.at + 1) + " of " + b.set.Length + "   solved " + Bits(b.done);
        b.ask.text = q.ask;
        for (int i = 0; i < 3; i++) { b.optL[i].text = q.opt[i]; b.optR[i].sharedMaterial = quizBtn; }
        QuizDraw(b, false);
    }
    void QuizDraw(QuizBay b, bool reveal)
    {
        for (int i = b.stage.childCount - 1; i >= 0; i--) Destroy(b.stage.GetChild(i).gameObject);
        b.spin = null;
        if (b.baseIdx == 1000) DrawPhys(b.stage, b.at, reveal); else b.spin = DrawBio(b.stage, b.at);
    }
    string QuizText(int i)
    {
        var b = bays[(i - 1000) / 10]; if (b == null) return ""; int o = i % 10; if (o > 2) return "";
        return b.answered ? "next question" : "answer: " + b.set[b.at].opt[o];
    }
    void QuizUse(int i)
    {
        var b = bays[(i - 1000) / 10]; if (b == null) return; int o = i % 10; if (o > 2) return; var q = b.set[b.at];
        if (b.answered) { QuizShow(b, b.at + 1); Play(cPick, 0.5f); return; }
        if (o != q.right) { b.wrong++; b.firstTry = false; b.optR[o].sharedMaterial = quizBad; Play(cBoop, 0.5f); Note("Not quite. Look at the diagram again and try another answer."); return; }
        b.answered = true; b.right++; quizRight++; b.optR[o].sharedMaterial = quizGood; Play(cWin, 0.6f); game.Buzz(0.6f, 0.15f);
        if ((b.done & (1 << b.at)) == 0) { b.done |= 1 << b.at; AddXp(b.firstTry ? 20 : 10); }
        b.ask.text = "<color=#7CFC9A><b>Correct!</b></color>  " + q.why + "  <color=#9AD8FF>(click any answer for the next one)</color>";
        b.head.text = "<b>" + b.name + "</b>   " + q.tag + "   " + (b.at + 1) + " of " + b.set.Length + "   solved " + Bits(b.done);
        QuizDraw(b, true); b.nextIn = 14f;
    }
    void UpdateAcademy(float dt)
    {
        foreach (var b in bays)
        {
            if (b == null) continue;
            if (b.spin) b.spin.Rotate(0f, dt * 40f, 0f, Space.Self);
            if (b.answered && b.nextIn > 0f) { b.nextIn -= dt; if (b.nextIn <= 0f) QuizShow(b, b.at + 1); }
        }
    }
    string AcademyStatus() { return "Force diagrams solved " + (bays[0] != null ? Bits(bays[0].done) : 0) + " of " + PhysSet.Length + "   Biology solved " + (bays[1] != null ? Bits(bays[1].done) : 0) + " of " + BioSet.Length + "   Hall plaques read " + Bits(hallSeen); }

    // ------------------------------------------------------------------ drawing helpers (the diagrams live in the board's flat plane)
    static readonly Color FW = new Color(2.2f, 0.45f, 0.35f), FN = new Color(0.45f, 0.95f, 2.4f), FG = new Color(0.45f, 2.0f, 0.65f), FA = new Color(2.5f, 1.9f, 0.3f), FF = new Color(0.35f, 0.75f, 0.9f);
    GameObject Box(Transform p, Material m, float x, float y, float w, float h, float rot = 0f)
    {
        var g = Prim(PrimitiveType.Cube, p, new Vector3(x, y, -0.3f), new Vector3(w, h, 0.6f), m); if (rot != 0f) g.transform.localRotation = Quaternion.Euler(0f, 0f, rot); return g;
    }
    GameObject Ball(Transform p, Material m, float x, float y, float d) { return Prim(PrimitiveType.Sphere, p, new Vector3(x, y, -0.3f), Vector3.one * d, m); }
    TextMeshPro Txt(Transform p, string text, float x, float y, float size, Color c)
    {
        var l = Label(p, text, 0f, 0f, size, 7f, 1.2f, new Color(Mathf.Min(1f, c.r), Mathf.Min(1f, c.g), Mathf.Min(1f, c.b))); l.transform.localPosition = new Vector3(x, y, -0.85f); return l;
    }
    void Arrow(Transform p, float x, float y, Vector2 d, float len, Color c, string text)
    {
        Vector3 dir = new Vector3(d.x, d.y, 0f).normalized, from = new Vector3(x, y, -0.75f), tip = from + dir * len; var m = Glow(c);
        var sh = Prim(PrimitiveType.Cylinder, p, from + dir * len * 0.5f, new Vector3(0.1f, len * 0.5f, 0.1f), m); sh.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 d2 = Quaternion.AngleAxis(s * 150f, Vector3.forward) * dir; var h = Prim(PrimitiveType.Cube, p, tip + d2 * 0.2f, new Vector3(0.1f, 0.44f, 0.1f), m); h.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d2);
        }
        if (text.Length > 0) { Vector3 at = tip + dir * (0.35f + Mathf.Abs(dir.x) * (0.25f + text.Length * 0.055f)); Txt(p, "<b>" + text + "</b>", at.x, at.y, 1.5f, c); }
    }

    void DrawPhys(Transform s, int scene, bool reveal)
    {
        var obj = Lit(new Color(0.85f, 0.55f, 0.2f)); var surf = Lit(new Color(0.5f, 0.52f, 0.58f)); var rope = Lit(new Color(0.85f, 0.85f, 0.8f)); var pos = Glow(new Color(1.9f, 0.4f, 0.35f)); var neg = Glow(new Color(0.4f, 0.7f, 2.2f));
        Vector2 up = Vector2.up, down = Vector2.down, left = Vector2.left, right = Vector2.right;
        switch (scene)
        {
            case 0:
                Box(s, surf, 0f, 3.5f, 5f, 0.3f); Box(s, obj, 0f, 4.25f, 1.2f, 1.2f); Arrow(s, 0f, 4.25f, down, 1.5f, FW, "W = mg");
                if (reveal) Arrow(s, 0f, 4.25f, up, 1.9f, FA, "N = mg"); break;
            case 1:
                Box(s, surf, 0f, 7.1f, 3f, 0.2f); Box(s, rope, 0f, 6f, 0.06f, 2f); Ball(s, obj, 0f, 4.7f, 1f); Arrow(s, 0f, 4.7f, down, 1.5f, FW, "W = mg");
                if (reveal) Arrow(s, 0.25f, 4.7f, up, 1.5f, FA, "T = mg"); break;
            case 2:
                {
                    float a = 25f * Mathf.Deg2Rad; Vector2 u = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), n = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)); Vector2 c = new Vector2(0f, 4.2f) + n * 0.75f;
                    Box(s, surf, 0f, 4.2f, 7f, 0.3f, 25f); Box(s, obj, c.x, c.y, 1.2f, 1.2f, 25f); Txt(s, "angle = 25 deg", -2.2f, 2.95f, 1.3f, Color.white);
                    Arrow(s, c.x, c.y, down, 1.6f, FW, "W = mg"); Arrow(s, c.x, c.y, n, 1.5f, FN, "N");
                    if (reveal) Arrow(s, c.x, c.y, u, 1.5f, FA, "f = mg sin(angle)"); break;
                }
            case 3:
                Box(s, surf, 0f, 3.5f, 9f, 0.3f); Box(s, obj, 0f, 4.25f, 1.2f, 1.2f); Arrow(s, -0.5f, 5.4f, right, 1.2f, FG, "v");
                Arrow(s, 0f, 4.25f, down, 1.1f, FW, "W"); Arrow(s, 0f, 4.25f, up, 1.1f, FN, "N"); Arrow(s, 0f, 4.25f, right, 1.9f, FG, "F push");
                if (reveal) Arrow(s, 0f, 4.25f, left, 1.4f, FA, "f = mu N"); break;
            case 4:
                Box(s, surf, 0f, 3.2f, 10f, 0.2f);
                for (int i = 0; i <= 12; i++) { float x = -4.5f + i * 0.75f; Ball(s, rope, x, 3.5f + 2.9f * (1f - (x / 4.5f) * (x / 4.5f)), 0.12f); }
                Ball(s, obj, 0f, 6.4f, 0.7f); Arrow(s, 0f, 6.4f, right, 1.5f, FG, "v");
                if (reveal) Arrow(s, 0f, 6.4f, down, 1.7f, FA, "W = mg (the only force)"); break;
            case 5:
                Ball(s, surf, 0f, 4.9f, 0.3f); for (int i = 0; i < 24; i++) { float a = i * Mathf.PI / 12f; Ball(s, rope, Mathf.Cos(a) * 2.2f, 4.9f + Mathf.Sin(a) * 2.2f, 0.1f); }
                Box(s, rope, 1.1f, 4.9f, 2.2f, 0.05f); Ball(s, obj, 2.2f, 4.9f, 0.7f); Arrow(s, 2.2f, 4.9f, up, 1.4f, FG, "v");
                if (reveal) Arrow(s, 2.2f, 4.9f, left, 1.5f, FA, "F = m v^2 / r"); break;
            case 6:
                Box(s, surf, -4.6f, 4.4f, 0.3f, 2.4f); Box(s, surf, 0f, 3.5f, 9.5f, 0.3f);
                for (int i = 0; i < 10; i++) Box(s, rope, -4.2f + i * 0.5f, 4.25f, 0.62f, 0.06f, i % 2 == 0 ? 55f : -55f);
                Box(s, obj, 1.35f, 4.25f, 1.2f, 1.2f); Box(s, FlatMat(FF), -0.6f, 4.6f, 0.05f, 1.9f); Txt(s, "rest position", -0.6f, 5.8f, 1.2f, FF); Arrow(s, -0.6f, 6.4f, right, 1.6f, FG, "stretch x");
                if (reveal) Arrow(s, 1.35f, 4.25f, left, 1.6f, FA, "F = -k x"); break;
            case 7:
                Box(s, Clear(new Color(0.2f, 0.5f, 1f, 0.35f)), 0f, 4.0f, 6f, 2.4f); Box(s, obj, 0f, 5.0f, 1.3f, 1.3f); Arrow(s, 0f, 5.0f, down, 1.5f, FW, "W = mg");
                if (reveal) Arrow(s, 0.3f, 5.0f, up, 1.5f, FA, "F = rho V g"); break;
            case 8:
                Ball(s, pos, -2.2f, 4.9f, 0.9f); Txt(s, "<b>+</b>", -2.2f, 4.9f, 3f, Color.white); Ball(s, pos, 1f, 4.9f, 0.9f); Txt(s, "<b>+</b>", 1f, 4.9f, 3f, Color.white); Txt(s, "distance r", -0.6f, 4.1f, 1.2f, Color.white);
                if (reveal) Arrow(s, 1f, 4.9f, right, 1.7f, FA, "F = k q1 q2 / r^2"); break;
            case 9:
                for (int j = 0; j < 3; j++) { Arrow(s, -5f, 3.6f + j * 1.3f, right, 3.6f, FF, ""); Arrow(s, 1f, 3.6f + j * 1.3f, right, 3.6f, FF, j == 2 ? "E" : ""); }
                Ball(s, neg, 0f, 4.9f, 0.9f); Txt(s, "<b>-</b>", 0f, 4.95f, 3.4f, Color.white);
                if (reveal) Arrow(s, 0f, 4.9f, left, 1.7f, FA, "F = qE"); break;
            case 10:
                for (int j = 0; j < 3; j++) for (int i = 0; i < 6; i++) Txt(s, "x", -4.4f + i * 1.75f, 3.5f + j * 1.4f, 2.2f, FF); Txt(s, "B into the page", 3.6f, 7.1f, 1.3f, FF);
                Ball(s, pos, -1f, 4.9f, 0.8f); Txt(s, "<b>+</b>", -1f, 4.9f, 2.8f, Color.white); Arrow(s, -1f, 4.9f, right, 1.6f, FG, "v");
                if (reveal) Arrow(s, -1f, 4.9f, up, 1.6f, FA, "F = q v x B"); break;
            case 11:
                for (int j = 0; j < 3; j++) { Arrow(s, -5f, 3.6f + j * 1.3f, right, 3.4f, FF, ""); Arrow(s, 1.4f, 3.6f + j * 1.3f, right, 3.4f, FF, j == 2 ? "B" : ""); }
                Box(s, Lit(new Color(0.95f, 0.74f, 0.22f)), 0f, 4.9f, 0.16f, 3.6f); Arrow(s, -0.5f, 4.2f, up, 1.4f, FG, "I");
                if (reveal) Txt(s, "<b>(X)  F = I L x B, into the page</b>", 0.2f, 2.95f, 1.6f, FA); break;
            default:
                Box(s, Lit(new Color(0.95f, 0.74f, 0.22f)), -1.3f, 4.9f, 0.16f, 3.6f); Box(s, Lit(new Color(0.95f, 0.74f, 0.22f)), 1.3f, 4.9f, 0.16f, 3.6f);
                Arrow(s, -1.8f, 3.6f, up, 1.3f, FG, "I1"); Arrow(s, 1.8f, 3.6f, up, 1.3f, FG, "I2"); Txt(s, "distance d", 0f, 3.0f, 1.2f, Color.white);
                if (reveal) { Arrow(s, 1.3f, 5.6f, left, 0.9f, FA, ""); Arrow(s, -1.3f, 5.6f, right, 0.9f, FA, ""); Txt(s, "<b>F/L = mu0 I1 I2 / (2 pi d)</b>", 0f, 6.9f, 1.5f, FA); }
                break;
        }
    }
    Material FlatMat(Color c) { return Glow(c); }

    /// returns the part that should slowly turn, if any
    Transform DrawBio(Transform s, int scene)
    {
        var white = Lit(new Color(0.92f, 0.92f, 0.9f)); var green = Lit(new Color(0.25f, 0.7f, 0.3f)); var pink = Lit(new Color(0.95f, 0.55f, 0.6f)); var red = Lit(new Color(0.8f, 0.2f, 0.2f)); var tan = Lit(new Color(0.8f, 0.66f, 0.45f)); var dish = Clear(new Color(0.7f, 0.9f, 1f, 0.3f));
        var spin = new GameObject("spin").transform; spin.SetParent(s, false); spin.localPosition = new Vector3(0f, 4.9f, -0.9f);
        switch (scene)
        {
            case 0:
                Box(s, Lit(new Color(0.5f, 0.3f, 0.18f)), -1f, 3.4f, 1.4f, 0.9f); Box(s, green, -0.6f, 4.9f, 0.14f, 2.3f, -20f);
                Prim(PrimitiveType.Sphere, s, new Vector3(0.1f, 5.6f, -0.3f), new Vector3(0.9f, 0.3f, 0.5f), green); Prim(PrimitiveType.Sphere, s, new Vector3(-0.9f, 4.9f, -0.3f), new Vector3(0.8f, 0.28f, 0.5f), green);
                Ball(s, Glow(new Color(2.4f, 2.1f, 1.2f)), 3.6f, 6.6f, 0.9f); Txt(s, "LED lamp", 3.6f, 5.8f, 1.2f, Color.white); Arrow(s, 3f, 6.4f, new Vector2(-1f, -0.35f), 2f, new Color(2.2f, 1.9f, 0.6f), ""); return null;
            case 1:
                Box(s, white, 0f, 4.9f, 4f, 0.6f, 15f); foreach (float sx in new[] { -1f, 1f }) { Ball(s, white, sx * 2.0f, 4.9f + sx * 0.75f, 0.9f); Ball(s, white, sx * 2.0f + 0.3f, 4.9f + sx * 0.3f, 0.8f); }
                Txt(s, "thigh bone", 0f, 3.3f, 1.3f, Color.white); return null;
            case 2:
                Box(s, pink, -1.2f, 5.2f, 2.6f, 0.6f, 30f); Box(s, pink, 1f, 5.3f, 2.4f, 0.55f, -35f); Prim(PrimitiveType.Sphere, s, new Vector3(-1.1f, 5.6f, -0.5f), new Vector3(1.6f, 0.8f, 0.6f), red); Ball(s, pink, 2.1f, 4.6f, 0.7f);
                Txt(s, "muscles need work every day", 0f, 3.3f, 1.3f, Color.white); return null;
            case 3:
                Prim(PrimitiveType.Capsule, s, new Vector3(0f, 4.3f, -0.3f), new Vector3(1.1f, 1.1f, 0.8f), white); Ball(s, pink, 0f, 6.2f, 1.25f);
                Arrow(s, -1.2f, 3.6f, Vector2.up, 1.8f, FN, ""); Arrow(s, 1.2f, 3.6f, Vector2.up, 1.8f, FN, "fluid"); return null;
            case 4:
                for (int i = 0; i < 14; i++)
                {
                    float a = i * 0.55f, y = (i - 6.5f) * 0.3f; Vector3 p1 = new Vector3(Mathf.Cos(a) * 0.9f, y, Mathf.Sin(a) * 0.9f);
                    Prim(PrimitiveType.Sphere, spin, p1, Vector3.one * 0.3f, Glow(new Color(0.4f, 0.9f, 2.2f))); Prim(PrimitiveType.Sphere, spin, new Vector3(-p1.x, y, -p1.z), Vector3.one * 0.3f, Glow(new Color(2.2f, 0.6f, 0.9f)));
                    var rung = Prim(PrimitiveType.Cylinder, spin, new Vector3(0f, y, 0f), new Vector3(0.07f, 0.9f, 0.07f), white); rung.transform.localRotation = Quaternion.FromToRotation(Vector3.up, p1.normalized);
                }
                Txt(s, "DNA double helix", 0f, 2.75f, 1.3f, Color.white); return spin;
            case 5:
                Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 4.3f, -0.4f), new Vector3(4f, 0.25f, 1.6f), dish);
                for (int i = 0; i < 11; i++) Prim(PrimitiveType.Sphere, spin, new Vector3(Mathf.Cos(i * 2.4f) * 0.25f * (i % 4), Mathf.Sin(i * 1.7f) * 0.5f + 0.2f, Mathf.Sin(i * 2.4f) * 0.25f * (i % 4)), Vector3.one * (0.45f + (i % 3) * 0.08f), pink);
                Txt(s, "a 3-D clump of cells", 0f, 3.3f, 1.3f, Color.white); return spin;
            case 6:
                for (int i = 0; i < 4; i++) { Prim(PrimitiveType.Sphere, spin, new Vector3((i - 1.5f) * 0.75f, 0f, 0f), new Vector3(1f, 0.95f - i * 0.05f, 0.9f), tan); foreach (float z in new[] { -0.4f, 0.4f }) Prim(PrimitiveType.Capsule, spin, new Vector3((i - 1.5f) * 0.75f, -0.5f, z), new Vector3(0.2f, 0.25f, 0.2f), tan); }
                Prim(PrimitiveType.Sphere, spin, new Vector3(-1.7f, 0.1f, 0f), Vector3.one * 0.5f, tan); Txt(s, "tardigrade, half a millimetre long", 0f, 3.1f, 1.3f, Color.white); return spin;
            case 7:
                {
                    var c = Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 4.9f, -0.2f), new Vector3(3.4f, 0.08f, 3.4f), white); c.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Box(s, Lit(new Color(0.1f, 0.1f, 0.12f)), 0f, 5.5f, 0.12f, 1.3f).transform.localPosition += new Vector3(0f, 0f, -0.1f); Box(s, Lit(new Color(0.1f, 0.1f, 0.12f)), 0.45f, 4.9f, 0.9f, 0.12f).transform.localPosition += new Vector3(0f, 0f, -0.1f);
                    Ball(s, Glow(new Color(0.6f, 1.3f, 2.6f)), -3.6f, 6.4f, 0.8f); Txt(s, "morning light", -3.6f, 5.6f, 1.1f, Color.white); Ball(s, Glow(new Color(2.4f, 1.2f, 0.4f)), 3.6f, 6.4f, 0.8f); Txt(s, "evening light", 3.6f, 5.6f, 1.1f, Color.white); return null;
                }
            case 8:
                Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 4.6f, -0.4f), new Vector3(4.4f, 0.2f, 1.8f), dish);
                for (int i = 0; i < 16; i++) Prim(PrimitiveType.Capsule, s, new Vector3(Mathf.Cos(i * 2.1f) * (0.4f + (i % 5) * 0.35f), 4.95f + (i % 3) * 0.12f, -0.4f + Mathf.Sin(i * 2.1f) * 0.5f), new Vector3(0.14f, 0.2f, 0.14f), i % 2 == 0 ? Glow(new Color(0.4f, 1.8f, 0.6f)) : Glow(new Color(0.4f, 0.9f, 2f))).transform.localRotation = Quaternion.Euler(0f, 0f, i * 37f);
                Txt(s, "microbes on a test dish", 0f, 3.5f, 1.3f, Color.white); return null;
            default:
                Ball(s, white, 0f, 4.9f, 2.6f); Prim(PrimitiveType.Sphere, s, new Vector3(0f, 4.9f, -1.3f), new Vector3(1.2f, 1.2f, 0.5f), Lit(new Color(0.2f, 0.45f, 0.8f))); Prim(PrimitiveType.Sphere, s, new Vector3(0f, 4.9f, -1.5f), new Vector3(0.55f, 0.55f, 0.3f), Lit(new Color(0.03f, 0.03f, 0.04f)));
                Arrow(s, 3.4f, 4.9f, Vector2.left, 1.6f, FN, ""); Txt(s, "pressure", 3.9f, 5.5f, 1.2f, Color.white); return null;
        }
    }

    // ------------------------------------------------------------------ hall of science
    static readonly string[] SciKey = { "newton", "galileo", "faraday", "maxwell", "curie", "einstein", "franklin", "johnson", "hawking", "jemison" };
    static readonly string[] SciName = { "Isaac Newton", "Galileo Galilei", "Michael Faraday", "James Clerk Maxwell", "Marie Curie", "Albert Einstein", "Rosalind Franklin", "Katherine Johnson", "Stephen Hawking", "Mae Jemison" };
    static readonly string[] SciSub = { "1643 - 1727", "1564 - 1642", "1791 - 1867", "1831 - 1879", "1867 - 1934", "1879 - 1955", "1920 - 1958", "1918 - 2020", "1942 - 2018", "born 1956" };
    static readonly string[] SciMark = { "F = ma", "telescope\n1609", "induction\n1831", "light is an\nEM wave", "Ra   Po", "E = mc^2", "DNA\nPhoto 51", "orbit\nmath", "black\nholes", "doctor\nin space" };
    static readonly string[] SciBody =
    {
        "Isaac Newton wrote down the three laws of motion and the law of universal gravitation in his book Principia (1687). The same rules explain a falling apple, the orbit of the Moon and the path of every rocket.\n\nHe also helped invent calculus, the mathematics of change, and showed that white light is made of all the colours.\n\nIn this game: every force diagram in the Physics Force Lab uses Newton's laws, and the reference sheet's calculus is his too.",
        "Galileo Galilei turned a telescope to the sky in 1609 and 1610. He saw mountains and craters on the Moon, four moons circling Jupiter, and the phases of Venus: evidence that the planets go around the Sun.\n\nHe also showed by experiment that, without air, heavy and light things fall at the same rate.\n\nIn 1971 astronaut David Scott dropped a hammer and a feather on the Moon. They landed together. Galileo was right.",
        "Michael Faraday had almost no formal schooling; he taught himself while working as a bookbinder's apprentice. In 1831 he discovered electromagnetic induction: a changing magnetic field makes an electric current.\n\nEvery generator, transformer and electric motor is built on that idea, including the ones that would power a Moon base.",
        "James Clerk Maxwell gathered everything known about electricity and magnetism into four equations. They predicted waves of electric and magnetic field that travel at the speed of light, and so showed that light itself is an electromagnetic wave.\n\nRadio, radar, Wi-Fi and the link between a Moon base and Earth all follow from his work.",
        "Marie Curie studied the rays given off by uranium and named the effect radioactivity. With Pierre Curie she discovered two new elements, polonium and radium.\n\nShe was the first woman to win a Nobel Prize and is the only person to win Nobel Prizes in two different sciences: physics (1903) and chemistry (1911).\n\nRadiation safety for space crews starts with what she discovered.",
        "Albert Einstein published special relativity in 1905 (including E = mc^2) and general relativity in 1915, which describes gravity as the bending of space and time. He won the Nobel Prize in 1921 for explaining the photoelectric effect: light arrives in packets of energy.\n\nSolar panels work because of the photoelectric effect, and satellite navigation must correct for relativity to stay accurate.",
        "Rosalind Franklin was an expert in X-ray diffraction. In 1952 her laboratory took 'Photo 51', an X-ray picture of DNA whose X-shaped pattern revealed that DNA is a helix. It was key evidence for the double-helix model of DNA.\n\nShe went on to do important work on the structure of viruses.\n\nSpace biology studies how radiation and weightlessness affect the same DNA she helped reveal.",
        "Katherine Johnson was a NASA mathematician. She calculated the path of Alan Shepard's 1961 flight, the first American in space. In 1962 John Glenn asked that she personally check the computer's numbers before his orbital flight. She also worked on the trajectories that took Apollo 11 to the Moon in 1969.\n\nShe received the Presidential Medal of Freedom in 2015.",
        "Stephen Hawking studied black holes and the beginning of the universe. In 1974 he showed that black holes are not completely black: they slowly give off energy, now called Hawking radiation.\n\nHe lived for more than fifty years with motor neurone disease, spoke through a computer, and wrote 'A Brief History of Time' so that everyone could share the wonder of the universe.",
        "Mae Jemison is a physician and an engineer. In September 1992 she flew on Space Shuttle Endeavour and became the first African American woman in space.\n\nOn that mission she worked on life-science experiments, including bone cell research and motion sickness studies: exactly the kind of biomedical science a Moon base needs."
    };
    static readonly string[] MoonKey = { "armstrong", "aldrin", "conrad", "bean", "shepard", "mitchell", "scott", "irwin", "young", "duke", "cernan", "schmitt" };
    static readonly string[] MoonName = { "Neil Armstrong", "Buzz Aldrin", "Pete Conrad", "Alan Bean", "Alan Shepard", "Edgar Mitchell", "David Scott", "James Irwin", "John Young", "Charles Duke", "Eugene Cernan", "Harrison Schmitt" };
    static readonly string[] MoonSub = { "Apollo 11, 1969", "Apollo 11, 1969", "Apollo 12, 1969", "Apollo 12, 1969", "Apollo 14, 1971", "Apollo 14, 1971", "Apollo 15, 1971", "Apollo 15, 1971", "Apollo 16, 1972", "Apollo 16, 1972", "Apollo 17, 1972", "Apollo 17, 1972" };
    static readonly string[] MoonBody =
    {
        "Commander of Apollo 11. On 20 July 1969 he became the first person to walk on the Moon, in the Sea of Tranquility.",
        "Lunar module pilot of Apollo 11 and the second person on the Moon. He earned a doctorate from MIT for his work on how spacecraft meet in orbit.",
        "Commander of Apollo 12. He made a pinpoint landing within walking distance of Surveyor 3, a robot lander that had arrived two years earlier.",
        "Lunar module pilot of Apollo 12. After leaving NASA he became an artist and painted scenes of the Moon from his own memory.",
        "Commander of Apollo 14. In 1961 he had been the first American in space. On the Moon he hit two golf balls.",
        "Lunar module pilot of Apollo 14. He and Alan Shepard pulled a tool cart across the Fra Mauro highlands to collect rock samples.",
        "Commander of Apollo 15, the first mission with the Lunar Roving Vehicle. He dropped a hammer and a feather together to show that Galileo was right.",
        "Lunar module pilot of Apollo 15. He and David Scott found the 'Genesis Rock', a piece of the Moon's early crust about four billion years old.",
        "Commander of Apollo 16. He flew six space missions and in 1981 commanded the very first Space Shuttle flight.",
        "Lunar module pilot of Apollo 16. At 36 he is the youngest person to have walked on the Moon. He left a photo of his family on the surface.",
        "Commander of Apollo 17. In December 1972 he was the last person, so far, to leave footprints on the Moon.",
        "Lunar module pilot of Apollo 17 and a geologist: the first professional scientist on the Moon. He discovered orange soil made of tiny beads of volcanic glass."
    };
    const string MoonAll = "During NASA's Apollo programme, from 1968 to 1972, twenty-four people flew from Earth to the Moon, and twelve of them walked on the surface.\n\nApollo 8 (1968) and Apollo 10 (1969) flew around the Moon without landing. Apollo 13 (1970) had to swing around the Moon and come home after an oxygen tank failed; the crew returned safely.\n\nThe six landings:\n  Apollo 11 (1969)   Neil Armstrong, Buzz Aldrin\n  Apollo 12 (1969)   Pete Conrad, Alan Bean\n  Apollo 14 (1971)   Alan Shepard, Edgar Mitchell\n  Apollo 15 (1971)   David Scott, James Irwin\n  Apollo 16 (1972)   John Young, Charles Duke\n  Apollo 17 (1972)   Eugene Cernan, Harrison Schmitt\n\nOn each landing a third astronaut stayed in orbit around the Moon to fly the command module.\n\nNASA's Artemis programme aims to take people back to the Moon and to build a base where crews can stay: the kind of habitat this game is about.";
    const string HunchAll = "HUNCH stands for High school students United with NASA to Create Hardware.\n\nThe programme began in 2003 at NASA's Johnson Space Center. High school students design and build real things for NASA and for the crews of the International Space Station: hardware, software, sewn items, food for astronauts, and design prototypes. NASA engineers review the students' work the way they review their own.\n\nTHIS PROJECT\nLunar Smart Adaptive Habitat Design with AI Psychological Monitoring.\n\nA Moon base that looks after its crew. Sensors watch each person's heart rate, breathing, sleep and mood. When the AI sees stress, loneliness or tiredness building, it changes the habitat to help: the colour and brightness of the lights, the sounds, the air flow and temperature, the schedule, and it calls the crew together.\n\nIn this game you can see the idea working: the Crew Health wall, the team meetings, the crew radio and the lights that follow a 24-hour day are all parts of that design.";
    int hallFirst = -1; public int hallSeen;

    void BuildHallReads()
    {
        hallFirst = reads.Count;
        for (int i = 0; i < SciName.Length; i++) { int ii = i; reads.Add(new ReadItem { title = SciName[i] + " (" + SciSub[i] + ")", zone = "Hall of Science", got = true, body = () => SciBody[ii] }); }
        for (int i = 0; i < MoonName.Length; i++) { int ii = i; reads.Add(new ReadItem { title = MoonName[i] + " (" + MoonSub[i] + ")", zone = "Hall of Science", got = true, body = () => MoonBody[ii] }); }
        reads.Add(new ReadItem { title = "Apollo: everyone who flew to the Moon", zone = "Hall of Science", got = true, body = () => MoonAll });
        reads.Add(new ReadItem { title = "NASA HUNCH and this project", zone = "Hall of Science", got = true, body = () => HunchAll });
    }

    string PictureDir() { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Pictures"); }
    Texture2D LoadPicture(string key)
    {
        try
        {
            foreach (string ext in new[] { ".jpg", ".jpeg", ".png" })
            {
                string p = Path.Combine(PictureDir(), key + ext); if (!File.Exists(p)) continue;
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, true); if (t.LoadImage(File.ReadAllBytes(p))) { t.wrapMode = TextureWrapMode.Clamp; return t; }
            }
        }
        catch { }
        return null;
    }
    int hallPictures;
    void Frame(Transform wall, float x, float y, int idx, string key, string name, string sub, string mark, Color card)
    {
        Prim(PrimitiveType.Cube, wall, new Vector3(x, y, 0.12f), new Vector3(3.5f, 4.4f, 0.2f), Lit(new Color(0.95f, 0.74f, 0.22f)));
        var tex = LoadPicture(key); Material m;
        if (tex) { m = new Material(solidSrc); if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); m.mainTexture = tex; m.SetColor("_BaseColor", Color.white); hallPictures++; }
        else m = Lit(card);
        float w = 3.1f, h = 3.1f; if (tex) { float asp = tex.width / (float)Mathf.Max(1, tex.height); if (asp > 1f) h = w / asp; else w = h * asp; }
        Prim(PrimitiveType.Quad, wall, new Vector3(x, y + 0.45f, 0f), new Vector3(w, h, 1f), m);
        if (!tex) { var e = Label(wall, "<b>" + mark + "</b>", 0f, 0f, 3f, 2.9f, 2.9f, Color.white); e.transform.localPosition = new Vector3(x, y + 0.45f, -0.03f); e.enableAutoSizing = true; e.fontSizeMin = 1.2f; e.fontSizeMax = 4f; }
        var n = Label(wall, "<b>" + name + "</b>\n<size=70%>" + sub + "</size>", 0f, 0f, 1.5f, 3.3f, 0.9f, new Color(0.08f, 0.08f, 0.1f)); n.transform.localPosition = new Vector3(x, y - 1.62f, -0.03f); n.enableAutoSizing = true; n.fontSizeMin = 0.8f; n.fontSizeMax = 1.6f;
        FacButton(wall, new Vector3(x, y, -0.12f), new Vector3(3.5f, 4.4f, 0.1f), Clear(new Color(1f, 1f, 1f, 0.02f)), idx);
    }
    void BuildHall(Transform r)
    {
        var wallMat = Lit(new Color(0.2f, 0.22f, 0.28f)); var titleCol = new Color(1f, 0.84f, 0.3f);
        var back = new GameObject("Hall: scientists").transform; back.SetParent(r, false); back.localPosition = new Vector3(0f, 0f, 44f);
        Prim(PrimitiveType.Cube, back, new Vector3(0f, 4f, 0.5f), new Vector3(56f, 8f, 0.5f), wallMat, true);
        for (int i = 0; i < SciName.Length; i++) Frame(back, (i - 4.5f) * 5.2f, 3.3f, 1200 + i, SciKey[i], SciName[i], SciSub[i], SciMark[i], new Color(0.14f + (i % 3) * 0.08f, 0.2f + (i % 2) * 0.1f, 0.42f));
        var t1 = Label(back, "<b>FAMOUS SCIENTISTS</b>   <size=60%>click a frame to read</size>", 0f, 0f, 4.5f, 50f, 1.5f, titleCol); t1.transform.localPosition = new Vector3(0f, 6.9f, 0.2f);
        var left = new GameObject("Hall: Moon walkers").transform; left.SetParent(r, false); left.localPosition = new Vector3(-46f, 0f, 6f); left.localRotation = Quaternion.Euler(0f, -90f, 0f);
        Prim(PrimitiveType.Cube, left, new Vector3(0f, 4f, 0.5f), new Vector3(62f, 8f, 0.5f), wallMat, true);
        for (int i = 0; i < MoonName.Length; i++) Frame(left, (i - 6f) * 4.4f, 3.3f, 1220 + i, MoonKey[i], MoonName[i], MoonSub[i], "Moon\nwalker\n" + (i + 1), new Color(0.3f, 0.3f, 0.34f));
        Frame(left, 6f * 4.4f + 0.6f, 3.3f, 1232, "apollo", "Apollo: all who flew", "24 people, 1968 - 1972", "24\nflew to\nthe Moon", new Color(0.1f, 0.12f, 0.3f));
        var t2 = Label(left, "<b>THE TWELVE WHO WALKED ON THE MOON</b>   <size=60%>click a frame to read</size>", 0f, 0f, 4.5f, 56f, 1.5f, titleCol); t2.transform.localPosition = new Vector3(0f, 6.9f, 0.2f);
        var right = new GameObject("Hall: NASA HUNCH").transform; right.SetParent(r, false); right.localPosition = new Vector3(46f, 0f, 6f); right.localRotation = Quaternion.Euler(0f, 90f, 0f);
        Prim(PrimitiveType.Cube, right, new Vector3(0f, 4f, 0.5f), new Vector3(30f, 8f, 0.5f), wallMat, true);
        Frame(right, -4f, 3.3f, 1240, "hunch_project", "NASA HUNCH", "and this project", "Lunar Smart\nAdaptive\nHabitat", new Color(0.1f, 0.25f, 0.5f));
        var hb = Label(right, "<b>NASA HUNCH</b>\n<size=62%>High school students United with NASA to Create Hardware\n\n<b>This project:</b> Lunar Smart Adaptive Habitat Design\nwith AI Psychological Monitoring\n\nA Moon base that watches over its crew's health and mood,\nand changes light, sound, air and schedule to help them.</size>", 0f, 0f, 3.2f, 14f, 7f, Color.white); hb.transform.localPosition = new Vector3(6.5f, 4f, 0.2f);
        try { string d = PictureDir(); if (!Directory.Exists(d)) Directory.CreateDirectory(d); string f = Path.Combine(d, "READ_ME.txt"); if (!File.Exists(f)) File.WriteAllText(f, "Pictures for the Hall of Science (Science Academy map).\r\nPut a .jpg or .png with one of these names in this folder and it appears in that person's frame the next time the game starts.\r\nUse pictures you are allowed to use: NASA photographs are public domain, and so are old portraits of Newton, Galileo, Faraday and Maxwell.\r\n\r\nScientists: " + string.Join(", ", SciKey) + "\r\nMoon walkers: " + string.Join(", ", MoonKey) + "\r\nOthers: apollo, hunch_project (a picture of your own project)\r\n"); } catch { }
    }
    string HallText(int i)
    {
        if (i >= 1200 && i < 1200 + SciName.Length) return "read about " + SciName[i - 1200];
        if (i >= 1220 && i < 1220 + MoonName.Length) return "read about " + MoonName[i - 1220];
        if (i == 1232) return "read: everyone who flew to the Moon on Apollo";
        if (i == 1240) return "read about NASA HUNCH and this project";
        return "";
    }
    void HallUse(int i)
    {
        int k = i >= 1200 && i < 1200 + SciName.Length ? i - 1200 : (i >= 1220 && i < 1220 + MoonName.Length ? SciName.Length + i - 1220 : (i == 1232 ? SciName.Length + MoonName.Length : (i == 1240 ? SciName.Length + MoonName.Length + 1 : -1)));
        if (k < 0 || hallFirst < 0) return; hallSeen |= 1 << k; OpenRead(hallFirst + k);
    }

    IEnumerator AutoAcademy()
    {
        AutoSay("Science Academy. Reading a force diagram: which way does the missing force point?");
        for (int bay = 0; bay < 2 && mapNow == 11 && autoOn; bay++)
        {
            var b = bays[bay]; TeleportTo(mapC[11] + new Vector3(bay == 0 ? -16f : 16f, 0f, 3f), 0f); yield return new WaitForSeconds(1.2f);
            if (bay == 1) AutoSay("Space Biology Lab. How do living things change away from Earth?");
            for (int n = 0; n < 3 && mapNow == 11 && autoOn; n++)
            {
                if (b.answered) QuizUse(b.baseIdx);
                yield return new WaitForSeconds(2.2f); QuizUse(b.baseIdx + b.set[b.at].right); AutoSay(b.set[b.at].why); yield return new WaitForSeconds(4.5f);
            }
        }
    }
    public string ProbeQuiz() { var b = bays[Mathf.Clamp(probeInt, 0, 1)]; if (b.answered) QuizUse(b.baseIdx); QuizUse(b.baseIdx + (b.set[b.at].right + 1) % 3); QuizUse(b.baseIdx + b.set[b.at].right); return b.name + " at " + b.at + " right " + b.right + " wrong " + b.wrong + " | " + AcademyStatus() + " pictures " + hallPictures; }
    public string ProbeQuizNext() { var b = bays[Mathf.Clamp(probeInt, 0, 1)]; QuizShow(b, b.at + 1); QuizUse(b.baseIdx + b.set[b.at].right); return b.name + " scene " + b.at; }
    public string ProbeQuizAll() { int n = 0; foreach (var b in bays) for (int k = 0; k < b.set.Length; k++) { QuizShow(b, k); QuizUse(b.baseIdx + b.set[k].right); n++; } return "answered " + n + " | " + AcademyStatus(); }
    public string ProbeHall() { HallUse(probeInt); return "reading " + readOpen + " seen " + Bits(hallSeen); }
}
