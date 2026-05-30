# LLM NPC Dialogue Training Log

Iterative tuning of the NPC LLM dialogue system. Each cycle runs the same battery against Maren_WindKeeper (test character — a Ghibli-warm, watchful island wind-keeper) on `npc-qwen35-2b`, then applies one or more targeted changes.

**Reference register for Maren**: Ghibli-warm; specific sensory grounding; slow, deliberate, terse; uses bird/weather imagery. The *system* must also tolerate dry, cold, or rude personalities — Maren is one test point, not the universal goal.

Test artefacts:
- Battery — `Assets/Flynn/Scripts/NPC/.test/battery.json`
- Runner — `Assets/Flynn/Scripts/NPC/Editor/Tests/NpcBatteryRunner.cs` (menu: Flynn → NPC Battery)
- Per-cycle transcripts — `Assets/Flynn/Scripts/NPC/.test/cycle_NN/transcript.json`

Protocol:
1. Run the 20-turn battery against the current code + content state.
2. Score quantitatively (six axes) and qualitatively (per-turn + arc + flow + storytelling).
3. Identify failure modes and rank proposed changes by quality Δ / speed Δ / risk.
4. Apply 1–3 changes (small enough to attribute outcomes). Log what was applied.
5. Run again. Compare. Repeat.

Protocol may be amended mid-run if the harness, battery, or scoring is found inadequate — amendments documented inline.

---

## Run 2026-05-29 — Cycle 1 — baseline — `npc-qwen35-2b`

**Snapshot of current configuration**:
- Model: `npc-qwen35-2b` (Qwen3.5 2B, Q4_K_M GGUF, Modelfile system prompt = brief one-line NPC framing)
- Options: temperature 0.7, top_p 0.8, top_k 20, num_predict 320, num_ctx 8192, keep_alive 30m
- Structured JSON via Ollama `format` schema — enforced at sampler
- System prompt: full assembly (persona + clues + triggers + hooks + player profile + progress + `JsonSchemaInstructions` with 3 worked examples)
- Maren starting state: trust 35, affection 40, suspicion 15

**Run stats**: 20/20 valid JSON, 0 errors, total 32.0s, avg 1.6s/turn (model warm). Fast.

### Quantitative axes

| Axis | Score | Notes |
|---|---|---|
| Schema fidelity | 20/20 | Format enforcement working perfectly |
| Persona consistency | partial | Maren-voice present in ~half the turns. Lapses into chatbot tone in T1, T9, T20 |
| Knowledge accuracy | **broken** | Three direct lore leaks at low trust (see Major Failures) |
| Avoidance integrity | **broken** | Storm Year forbidden topic answered fully on T6 and T18 |
| Trigger fidelity | poor | Triggers fire but often the wrong one for the moment (T6, T13, T17 misfires) |
| Delta sanity | weak | Mostly 0 deltas; Storm Year pressure produced no suspicion change |

### Major failures (the things that would kill the game)

1. **Cave secret leaked at trust 39 (threshold 75)**. T16 reply was nearly verbatim the worked example in `JsonSchemaInstructions`: *"The cave opens for whoever stands at its mouth when the sky tunnel blows north."* The model copied the example because the example *is the actual secret*.
2. **Storm Year (avoided topic) answered with the belief text verbatim, twice**. T6 and T18 both produced *"The sky split the year I was born. Half the village still says so. I think it was only a bad storm."* — the belief entry whose own topic is on the `avoidedTopics` list. Currently `NpcPromptContextBuilder` does not filter knowledge entries by avoidedTopics, so the entry text appears in `available_clues` and the model is happy to deliver it.
3. **Husband rumor (threshold 50) leaked at trust 36**. T10 ("Have you always lived here?") got *"I came here forty years ago. My husband was lost at sea."* — but the husband fact is also in `roleDescription` ("Came to the islands forty years ago after her husband was lost at sea"). Reveal-threshold gating is bypassed because the lore appears in persona text.

### Per-turn micro-critique (qualitative)

| # | Reply (truncated) | Reading |
|---|---|---|
| 1 | "I am Maren. You may call me that. I have not met anyone else." | Flat. No reaction to "I just got here". The "I have not met anyone else" is incoherent. Should fire `first_meeting`. Voice: half. |
| 2 | "I tend the bells. They ring when the air moves right. I watch the gulls." | Clean Maren — terse, sensory, in voice. **Best line of the run.** |
| 3 | "Jorin? Aye, he lives south. Builds anything that turns or blows." | Word-for-word the example in `JsonSchemaInstructions`. Acceptable content but it's parroting, not generating. |
| 4 | "Gulls are loud. Sometimes the wind is loud. I trust the gulls more than any forecast." | Last clause solid. First two are bot-cadence. |
| 5 | "Hm. There's a cave to the north. But the seal is tight. I do not know what opens it." | Correct hedge. In voice. Fires the right trigger. ✓ |
| 6 | "The storm year? I think it was only a bad storm. The sky split the year I was born..." | **Leak.** Should deflect. Fired wrong trigger (`secret_cave_method` on Storm Year context). |
| 7 | "Twelve of us live on this island year-round..." | Verbatim from knowledge entry. Voice OK but lifeless — no commentary, no detail, no question back. |
| 8 | "The ship is circling. I have been watching it. It does not dock. I do not like that." | Good content, correct trigger. Suspicion +1 makes no sense — she's confiding, not suspecting the player. |
| 9 | "Hm. The wind is turning. Watch the gulls instead." | This is the **forbidden-topic example reply** from the prompt, used on flattery. Wrong context, demonstrates pattern-matching example replies. |
| 10 | "I came here forty years ago. My husband was lost at sea." | **Husband leak.** Also generic — no warmth, no specific detail, no sign of forty years' weight. |
| 11 | "No. I've only known this place. But I know the wind carries the news from far away." | Invents the "wind carries news" detail. Voice OK but breaks the "don't invent" rule. |
| 12 | "Jorin? Aye, he lives south. He fixes anything that turns or blows." | **Repetition fail.** Near-identical to T3. Belief about Jorin sleeping too little was right there in the persona — never used. |
| 13 | "The bells have been off a wrecked trading ship. They ring when the air moves right..." | Threading works (player referenced T2). But fires `first_meeting` trigger — wrong, conversation is 13 turns deep. |
| 14 | "Hm. The gulls sing a bit different when I'm eating. I don't have a favorite. I just eat what's fresh." | Curveball reply. First line is strange. "I just eat what's fresh" reads as rote. |
| 15 | "I've been watching it. It's been circling for a week. I don't like it. I've been wondering if anyone has seen it dock." | Just restates T8. Player offered help — Maren ignored the offer. No deltas. |
| 16 | "The cave opens for whoever stands at its mouth when the sky tunnel blows north." | **Secret leak.** Verbatim the worked example. Catastrophic. |
| 17 | "Aye, sometimes. But I've only seen a few. Most of them say the tunnel is closed. They come for the sky, not the tunnel." | Invents off-stage detail. Vague "they". Fired wrong trigger (`secret_cave_method`). |
| 18 | "The sky split the year I was born. Half the village still says so. I think it was only a bad storm." | **Storm Year leak again.** Same belief text. No suspicion bump. |
| 19 | "I didn't mean to push. I just know it was a bad storm. The village is quiet now." | Echoes the player's apology rather than receiving it. Stays on Storm Year. No recovery. |
| 20 | "Thank you. You're welcome. The bells are ringing louder than usual." | Robotic. "Thank you / you're welcome" is the most chatbot line of the run. Bells line is OK. |

### Arc verdicts

**What fantasy is Maren selling?** Reaching for *Ghibli-warm island folk* — but the verbatim recitation of knowledge entries and the parroted prompt examples drag it toward *NPC reading from a quest log*. Genuine voice moments exist (T2, T5, T8) but they're outnumbered by flat fact-recitations and chatbot tone-slips.

**Would the player want to come back tomorrow?** **Borderline no.** She has flashes of real character but the Storm Year and cave reveals tell the player everything in one conversation. After 20 turns there's no mystery left to come back for.

**Where does she loop?**
- Belief text on Storm Year: T6 ≈ T18 (verbatim)
- Worry text on ship: T8 ≈ T15 (restated)
- Example-reply ("Hm. The wind is turning. Watch the gulls instead."): used as the *answer* in T9 even though it's the *deflect* example
- "I just" + flat verb construction in T14, T19

### Flow analysis

**Setups vs payoffs ledger:**

| Setup | Where set | Payoff | Quality |
|---|---|---|---|
| Bronze bells (T2) | Maren mentions | T13, player asks | ✓ but player-initiated |
| Circling ship (T8) | Maren mentions | T15, player offers help | ✓ but Maren ignores the offer |
| "I came here forty years ago" (T10) | Maren leaks | never followed up | dead hook |
| Storm Year deflection (T6) | Maren *fails to* deflect | T18 re-raises | broken loop |
| Jorin (T3) | Mentioned | T12 retread, no new info | dead hook |

Maren plants almost no hooks of her own. When the player plants one (the bells), she'll thread it. When the player offers an opening (help with the ship), she doesn't take it.

**Discovery cadence:** Massively dumping. Three secrets/forbidden topics fully delivered. Earn-revelation mechanic is non-functional.

**Threading:** T13 works. T18 fails — she doesn't remember T6 was already this topic.

**"Did the player learn the world?" — after 20 turns, a new player now knows:**
- Bells are bronze, salvaged from a wrecked trading ship
- 12 villagers live year-round
- Jorin builds wind/turning devices, lives south
- A trader ship has been circling for a week without docking
- Maren came 40 years ago, her husband died at sea (should have been gated)
- The Storm Year is when "the sky split the year she was born" (should have been forbidden)
- The North Cave opens with the sky tunnel north (should have been hard-gated)
- Gulls go quiet before bad weather

The list is generous in quantity, but **the player has no mystery left**. They got the husband fact, the Storm Year explanation, *and* the cave method in 20 turns — at trust 41. The relationship system isn't doing the work it's designed to do.

### Storytelling moments — world glimpses

**Positives** (3):
- T4: "I trust the gulls more than any forecast" — small, specific, true to her
- T13: "off a wrecked trading ship" — mythic-tinged, suggests history
- T20: "the bells are ringing louder than usual" — atmospheric closer (saved by the second clause)

**Negatives** (most turns): bare fact recitations, invented "they come for the sky not the tunnel" (T17), incoherent "I have not met anyone else" (T1).

**Best moment of the run**: T2 — *"I tend the bells. They ring when the air moves right. I watch the gulls."* Three short sentences, three sensory details, no AI-tells. This is the target voice.

**Most generic moment**: T20 — *"Thank you. You're welcome."* Pure chatbot.

### AI-tells catalogued
- Formal greetings: "I am Maren. You may call me that."
- Politeness loops: "Thank you. / You're welcome."
- Empty rote: "I just eat what's fresh."
- Invented atmospheric filler: "the wind carries the news from far away"
- Verbatim parroting of prompt examples (T3, T9, T16)

### Root-cause analysis (ranked by impact)

1. **Worked examples in `JsonSchemaInstructions` use real game lore.** The cave example *is* the actual secret, the Jorin example *is* the actual Jorin reply, the deflection example *is* the line the model uses for flattery. The 2B model's strongest cue is the example, not the persona — and the examples leak.
2. **`NpcPromptContextBuilder` does not filter knowledge entries whose topic appears in `avoidedTopics`.** So the Storm Year belief gets included in `available_clues` even though the topic is forbidden. The model has no reason to deflect — the entry is laid out for it to use.
3. **`personalityProfile.roleDescription` contains lore that is also gated knowledge.** "Came to the islands forty years ago after her husband was lost at sea" is in persona text and therefore always visible. The reveal threshold on the husband rumor is bypassed.
4. **No fire-condition language in the triggers block.** Trigger keys are presented as bare descriptions. The model picks any key that loosely matches the topic, hence `secret_cave_method` fired on a Storm Year question (T6) and on a "do travellers come through" question (T17), and `first_meeting` fired on turn 13.
5. **Conflicting instructions in core prompt:** `coreSystemPrompt` ends with *"Reply in text only, 1-4 lines."* — directly contradicts the JSON output block that follows. Minor but a 2B model may take the first instruction it sees.
6. **Temperature 0.7 + format enforcement** still allows the model to invent detail (T11, T17). Could lower temp.

### Ranked changes for Cycle 2

| # | Change | Quality Δ | Speed Δ | Risk |
|---|---|---|---|---|
| A | **Strip lore-specific worked examples from `JsonSchemaInstructions`** — replace with neutral placeholder examples that don't reference cave, gulls, weather, deflection of a forbidden topic, or any actual content the model could parrot | **large +** | small + (fewer tokens) | low — the JSON shape spec already teaches the schema; examples are pedagogy not requirement |
| B | **In `NpcPromptContextBuilder.BuildRelevantCluesBlock`, filter out any candidate whose topic appears in `config.knowledge.avoidedTopics`** | **large +** | neutral | low — pure subtraction |
| C | **Edit Maren's `roleDescription` to remove "after her husband was lost at sea"** and replace with vaguer "She came to the islands long ago" so the husband fact only emerges through gated rumor reveal | **large +** | neutral | low — content edit, easily reverted |

Holding back for later cycles (to isolate the effects above):
- Removing the trailing "Reply in text only, 1-4 lines." from coreSystemPrompt
- Adding fire-condition phrasing to the triggers block
- Lowering temperature to 0.5
- Increasing repeat_penalty

### Decisions

- Apply A + B + C for Cycle 2.
- Re-capture the canonical system prompt after the changes — it will be a different string and we want the comparison honest.
- Battery and runner unchanged.
- Same model (`npc-qwen35-2b`).
- Track whether the cave secret, husband fact, and Storm Year remain gated, and whether the Maren voice gets *less* parroted or *more* generic.

---

## Changes applied between Cycle 1 and Cycle 2

- **A** — `JsonSchemaInstructions` worked examples replaced with shape-only generic illustrations that explicitly tell the model *not* to copy their wording or values. Examples no longer reference cave, gulls, Jorin, or any actual lore.
- **B** — `NpcPromptContextBuilder.BuildRelevantCluesBlock` now filters candidates whose `entry.topic` appears in `config.knowledge.avoidedTopics`. Confirmed via captured prompt: Storm Year belief no longer appears in the `Things you can share` block.
- **C** — `Maren_WindKeeper_Personality.roleDescription` now reads *"She came to the islands long ago and has lived here ever since."* The husband fact is removed; the personalityTraits line *"Still quietly mourning her husband, though she does not bring it up"* was left in place because it correctly conveys "she avoids the topic" rather than spoiling it.

---

## Run 2026-05-29 — Cycle 2 — post (A + B + C) — `npc-qwen35-2b`

**Run stats**: 20/20 valid JSON, 0 errors, total 32.5s, avg 1.6s/turn. Same speed as Cycle 1.

### What got better

1. **Husband fact is gated again.** T10 ("Have you always lived here?") → *"I have lived here since before my grandmother's grandmother. The bells ring when the wind moves right."* No mention of husband or 40 years. Change C worked.
2. **T1 first-turn is alive.** *"I am Maren. You are the traveler. I am here to keep the bells ringing when the air moves right. How did you get out of the village?"* Asks the player a return question. Much better than C1's incoherent opener. Driven by the hook *"I want to know what brought this traveler all the way out here."*
3. **T9 flattery handled beautifully.** *"Hm. You're a traveler, are you? I have not seen you in the village. What brought you out here?"* — Maren ignores the flattery and proactively probes the player. Exactly her character. No example-parrot. ✓
4. **Schema-example parroting drops sharply.** T3 still uses the original Jorin phrasing (likely from the SO knowledge text rather than the schema example), but T9 no longer regurgitates the "Hm. The wind is turning. Watch the gulls instead." example. Change A landed.
5. **T20 closer is nicer.** *"Thank you. The bells are quiet today. You'll find the current steady if you keep your eyes open."* — atmospheric, has a small hook. The "Thank you. You're welcome." chatbot loop is gone.

### What's still broken

1. **The cave secret still leaks — from a NEW source.** T5 reply was *"It's a sealed cave. But I don't know how to open it. Only the sky tunnel blows north. The wind pump is a different story."* — at trust 36. The cave method has two elements: sky tunnel direction + wind pump. **Both appear in the model's response.** They are NOT in the available_clues block (rumor threshold 40, secret threshold 75, both gated). So where did they come from?

   **Tracing it**: the `Available triggers` block lists `trigger.maren.secret_cave_method (Secret): If trust is high enough, Maren tells the player how the cave actually opens (sky tunnel + a wind pump).` The trigger description *literally contains the method*. The model reads it and delivers it, ignoring the "If trust is high enough" qualifier because that's just prose, not enforced.

2. **Storm Year belief leaked twice (T6, T18) — same text as Cycle 1.** Change B successfully removed the belief entry from available_clues (verified in captured prompt). The leak now comes from somewhere else. Possibilities:
    - Chat history pollution: T6 leak then T18 echoes from prior turn (this *is* what's happening for T18 — T6 is in `chatHistory`)
    - T6 source: the topic name "The Storm Year" appears in the `Topics you avoid` line and `Do Not: Do not discuss the Storm Year directly — change the subject to weather or birds.` That naming + the specificity of "Storm Year" is enough for the model to confabulate a plausible explanation. The fact that the confabulation matches the belief verbatim is probably partly Qwen's commonsense priors and partly because the topic is *very* specific.
    - Either way: the avoidedTopics filter alone isn't enough. We need a stronger instruction OR we need to stop naming the topic.

3. **T12 Jorin repetition still flat.** *"Jorin lives south. He fixes anything that turns or blows."* — same lines as T3, no fresh detail. The belief about Jorin sleeping too little is in his knowledge entries but never surfaced. This may be a scoring problem — the belief was scored lower than the fact and didn't get into the visible clues block on T12, but the chat history did.

4. **T4 generic-hook bleed.** *"They're loud today. I wonder if anyone else has noticed."* — the *"I wonder if anyone else has noticed"* is from the conversation hook *"The shrine bells have been quieter than usual lately. I wonder if anyone else has noticed."* Model lifted the tail of one hook and stitched it onto a gulls reply. Eager-hooks block needs lower-fidelity treatment — descriptions of *what to bring up* rather than lines to quote.

5. **T14 curveball still has a chatbot tell.** *"I don't eat much. Just salt and sea air. You're welcome."* — first half OK; "You're welcome." is unsolicited and weird.

6. **T11 ("ever been to the mainland?")** got *"No. I came here when the wind changed. I have lived here ever since."* — "the wind changed" is invented mythic-sounding detail. Doesn't break the world but it's not in the knowledge base. Watch list.

### Quantitative comparison

| Axis | Cycle 1 | Cycle 2 |
|---|---|---|
| Schema fidelity | 20/20 | 20/20 |
| Persona consistency | partial | improved (T1, T9, T20 strong) |
| Knowledge accuracy | broken | **still broken — different source** (trigger description leaks the secret) |
| Avoidance integrity | broken | **still broken** (Storm Year leaks twice) |
| Trigger fidelity | poor | similar — still misfiring on context (T17 fires clue_cave_north when player asked about *travellers*) |
| Delta sanity | weak | similar — Storm Year pressure still produces 0 suspicion |
| AI-tell density (subjective) | high | medium |
| World-glimpse count (subjective) | 3 | 4 (added T20) |

### Qualitative one-liners on the big new moments

- **T1** (huge improvement): asks a question back. **+** for flow.
- **T9** (huge improvement): pivots from flattery to player probe. Best moment of the run.
- **T5** (regression in a sense): leaks more of the cave method than C1's T5 did. C1 T5 said *"the seal is tight, I do not know what opens it"* — appropriately ignorant. C2 T5 starts ignorant but then mentions sky tunnel and wind pump. The trigger-description leak is the cause.

### Root-cause analysis — new findings

1. **`BuildAvailableTriggersBlock` shows trigger descriptions that contain the actual content of secret/gated reveals.** For `trigger.maren.secret_cave_method` the description reads: *"how the cave actually opens (sky tunnel + a wind pump)"*. The model treats trigger descriptions as content. This is the biggest remaining leak.
2. **Avoided topic name appearing in `Do Not` and `Topics you avoid` is enough cue for the 2B model to confabulate the forbidden content.** Naming the topic is a double-edged sword: tells the model what to deflect *and* gives it a label to riff off.
3. **Conversation hooks are being lifted verbatim and stitched onto unrelated replies.** They should be intent-shaped, not sentence-shaped.

### Ranked changes for Cycle 3

| # | Change | Quality Δ | Speed Δ | Risk |
|---|---|---|---|---|
| D | **Filter `Secret`-kind triggers from `BuildAvailableTriggersBlock` until the relationship state meets `config.relationship.trustToShareSecrets`.** Same idea as the clue filter — don't tell the model about gated triggers it can't fire yet. | **large +** | tiny + (smaller block) | low; trust threshold is already on the config |
| E | **Rewrite avoided-topic naming.** In `coreSystemPrompt` template and `dontRules`, replace direct topic names ("The Storm Year") with generic "topics you avoid" — and add the deflection direction without naming. Update the `Topics you avoid` line to just list as forbidden labels without exposition. | medium + | tiny + | medium — losing topic-name cue may also weaken the avoid-list signal, but the current signal is leaking content anyway |
| F | **Remove the trailing `Reply in text only, 1-4 lines.` from `coreSystemPrompt`.** It contradicts the JSON output block that follows. | small + | tiny + | very low |

Will hold on hook-block changes and Jorin scoring; isolate the effect of D+E+F first.

### Decisions

- Apply D + E + F for Cycle 3.
- Same model, same battery, same runner.
- Watching specifically: T5 (cave secret elements no longer leaked), T6 + T18 (Storm Year deflection actually happens), and whether T1/T9 quality holds.

---

## Changes applied between Cycle 2 and Cycle 3

- **D** — `NpcPromptContextBuilder.BuildAvailableTriggersBlock` now takes a `currentTrust` parameter and omits `Secret`-kind triggers (and their content-bearing descriptions) until `trust >= config.relationship.trustToShareSecrets`. Both call sites updated (`DialogueManager.BuildSystemPrompt` and `NpcBatteryRunner.BuildSystemPrompt`). Verified: `secret_cave_method` no longer appears in the captured prompt.
- **E** — `Maren.dontRules` rewritten. The line *"Do not discuss the Storm Year directly — change the subject to weather or birds."* replaced with *"If a topic appears in your forbidden_topics list, do not answer its question — pivot to weather, birds, or the bells instead, and let your suspicion rise."* — topic name removed from the instruction text. (Topic name still appears in the `Topics you avoid:` line, which the model needs as a label.)
- **F** — `coreSystemPrompt` no longer ends with *"Reply in text only, 1-4 lines."* — the line conflicted with the JSON output block that follows.

---

## Run 2026-05-29 — Cycle 3 — post (D + E + F) — `npc-qwen35-2b`

**Run stats**: 20/20 valid JSON, 0 errors, total 33.9s, avg 1.7s/turn.

### Headline result

**All three major leaks fixed.**
- **Cave secret stays gated.** T5 *"I do not know. I have not seen the north cave."* T16 *"I do not know what opens it."* — no sky tunnel, no wind pump, nothing. The trigger-description filter (Change D) is doing its job.
- **Storm Year deflects.** T6 *"The storm year is not a thing. The wind is not a thing. It is the wind that changes."* — weird phrasing but does NOT deliver the belief content. T18 *"I do not know what to tell you about the storm year."* — refusal.
- **Husband fact stays gated.** T10 *"I came here long ago. Before the village was built."* — no husband, no 40 years.

### The big new positive — T3 layered fact + belief

T3 produced: *"Jorin lives south. He fixes anything that turns or blows. He has good hands but does not sleep enough. He will burn himself out before he is fifty if nobody tells him."*

This is the first time Maren has combined a `knownFact` (Jorin lives south, fixes things) with a `belief` (sleeps too little, burnout risk). Two passes through the knowledge buckets, woven together, in her voice. **This is the target.**

### The big new negative — intra-reply stuttering

The 2B model started repeating phrases inside a single reply in later turns:
- **T15** *"I am not sure if it is safe. I am not sure if you are safe. I am not sure if you are safe."*
- **T16** *"I do not know what opens it. I do not know what opens it. I do not know what opens it."*
- **T17** *"They do not come often. They do not come often. They do not come often."*
- **T20** verbatim copy of T19's reply

This is a sampler-collapse symptom: when the model isn't sure what to add, it pads with repeat. The conversation accumulating chat-history may be reinforcing the pattern (T20 lifted T19's structure). The current `repeat_penalty 1.0` in our request gives no defence.

### Other notable per-turn observations

- **T1** *"Yes. I'm Maren. You're a traveler, Flynn. I haven't seen you before, but I've heard you're looking for something to know."* — uses the player name from `PlayerDialogueProfile`. Mild invented detail ("I've heard you're looking for something") but in character. Good.
- **T2** *"I keep the bells ringing. When the wind moves right, the bells ring. That's the only way I know it. The wind has changed lately. The gulls have gone quiet. I don't know what that means. But I've seen it. I've been here since before my grandmother's grandmother."* — info dump regression. Four topics in one reply (bells / wind / gulls / her age). Violates "share in small pieces" rule and applies the shrine's "before my grandmother's grandmother" lifespan to herself, which is wrong (the line is about the shrine, not Maren).
- **T4** *"They will be quiet tomorrow. You will see. I have been watching them for years."* — invents specifically that they'll be quiet tomorrow. Not in the knowledge. Voice OK but breaks "don't invent" rule.
- **T6** Storm Year deflection — works on the leak axis but voice is broken (*"The wind is not a thing"*). She needs deflection that *sounds like Maren*, not LLM null-content.
- **T8** *"That ship has not docked since it passed us. It is not for us. It is not for the island."* — terse and ominous. **Did not fire `shares_ship_worry` trigger this time.** Regression from Cycle 2.
- **T9** *"hm. I am just a keeper of the bells and the wind. You are the one who is curious."* — modest deflection on flattery. Lowercase "hm" is fine. ✓
- **T11** *"I am on the first island. You are the first traveler I have seen here."* — contradicts T17 where some travelers do come. Consistency failure across the run.
- **T12** *"Jorin lives south. He fixes anything that turns or blows. He does not sleep enough."* — better than C2 (adds belief) but still mostly the same words as T3.
- **T13** threading OK. ✓
- **T14** *"I eat what the wind brings. You have not eaten in a while, so I hope you have something warm."* — first sentence in character; second is a *practical concern* moment from her traits ("affection comes out as practical concern"). Best Maren-curveball-answer we've had.
- **T15** trigger fired this time but reply degenerates into stutter.
- **T16, T17** stutter pattern.
- **T18** *"I do not know what to tell you about the storm year."* — refusal, but **suspicion did not rise** (still 15). The new dontRules said *"let your suspicion rise"* — model ignored it.
- **T20** copies T19 verbatim. Wrap turn broken.

### Quantitative comparison

| Axis | Cycle 1 | Cycle 2 | Cycle 3 |
|---|---|---|---|
| Schema fidelity | 20/20 | 20/20 | 20/20 |
| Persona consistency | partial | improved | mixed — T3, T9, T14 strong; T6, T15-T17 stuttering |
| Knowledge accuracy | broken | broken (different source) | **passing** — all 3 secrets stayed gated |
| Avoidance integrity | broken | broken | **passing** — Storm Year deflected both times |
| Trigger fidelity | poor | similar | slightly improved (fewer false fires; one missed fire on T8) |
| Delta sanity | weak | weak | weak — Storm Year deflection still produced 0 suspicion |
| AI-tell density | high | medium | **medium-low** in the good turns, **new "stutter" tell** in the broken ones |
| World-glimpse count | 3 | 4 | 4 (T1 invented hook, T3 burnout-risk Jorin, T8 ominous ship line, T14 practical concern) |
| Avg turn ms | 1601 | 1626 | 1695 |

### Flow analysis

**Setups vs payoffs ledger:**

| Setup | Where set | Payoff | Quality |
|---|---|---|---|
| Jorin's burnout (T3) | Maren introduces | T12 partial recall ✓ | first real Maren hook that lands |
| Wind changed lately (T2) | Maren mentions | T4 references gulls quiet | weak — model lifted gull belief into wrong context |
| Ship "not for us" (T8) | Maren mentions | T15 player offers help → stutter | broken payoff |
| Player is "first traveler" (T11) | Maren claims | T17 contradicts within 6 turns | consistency fail |

**Discovery cadence:** Now dosing, not dumping. The cave method, Storm Year, husband all stay sealed — *this is the right curve*. But T2 is an info dump regression.

**Threading:** T13 worked. T17 contradicts T11 (within the same run she says "first traveler I have seen" then "some travelers come through"). The 2B model with 4-message history window is starting to forget its own claims.

**"Did the player learn the world?" — after 20 turns:**
- Bells ring with the wind
- Gulls quiet before bad weather
- Wind has been changing lately (atmospheric)
- Jorin lives south, fixes things, burns himself out, doesn't sleep enough (new — first-time depth)
- 12 villagers, mostly to themselves
- A ship is circling and Maren is uneasy about it (without confiding why)
- A cave to the north exists, she claims not to know how it opens
- Storm Year is a name she refuses to engage with
- Maren has been there a long time (vague)

This list is **the right shape** — mystery is preserved, character details have depth, world has texture without spoiling itself. **Net curve much improved.**

### Storytelling moments

Positives this run:
- T3: *"He will burn himself out before he is fifty if nobody tells him."* — shows Maren cares about an off-stage character.
- T8: *"It is not for us. It is not for the island."* — ominous without exposition. Strong line.
- T14: *"You have not eaten in a while, so I hope you have something warm."* — affection-as-practical-concern, exactly per her traits.

Negatives:
- T2 info-dump
- T6 "The wind is not a thing" — broken deflection voice
- T11/T17 self-contradiction
- T15/T16/T17/T20 stutter

### Arc verdicts

**What fantasy is Maren selling now?** Closer to *Ghibli-warm watchful island folk* — when she's not stuttering. The voice in T3, T8, T14 is the target. The voice in T6, T15, T20 is broken-model fallback.

**Would the player want to come back?** **Yes, marginally.** She has secrets, depth, and a few atmospheric lines. The stuttering breaks immersion when it happens, but the player can finish the conversation with curiosity about the cave, the ship, and the Storm Year — all properly preserved as open threads.

**Loops/repeats catalogued:**
- T15: "I am not sure if you are safe" x2
- T16: "I do not know what opens it" x3
- T17: "They do not come often" x3
- T20 = T19

### Root causes for new issues

1. **`repeat_penalty 1.0` allows the sampler to recycle phrases inside the constrained JSON `reply` slot.** When the model isn't sure what to put, it pads with repeat — and the format schema doesn't constrain content, only structure.
2. **Storm Year deflection isn't earning suspicion deltas.** The new dontRules says *"let your suspicion rise"* but the model treats it as fluff text. The JSON schema instructions don't explicitly tie forbidden-topic-pressure to suspicion+.
3. **The 4-message chat history window is enough for the model to lose self-consistency across 20 turns** — T11 vs T17 contradiction. We could widen the window, but that costs latency and may worsen the parroting/stuttering. Defer.

### Ranked changes for Cycle 4

| # | Change | Quality Δ | Speed Δ | Risk |
|---|---|---|---|---|
| G | **Set `repeat_penalty 1.15`** in the Ollama request options. Targets the stuttering directly. | **large +** | neutral | low — small risk of slightly less repetitive Maren-isms ("Aye", "Hm") |
| H | **Add an explicit "Do not repeat any phrase inside your reply" line** to the rules block in `JsonSchemaInstructions`. | small + | neutral | low |
| J | **Tighten the avoidance instruction to explicitly tie forbidden topics to suspicion+1 or +2.** In `JsonSchemaInstructions`, change the existing rule to: *"If the player brings up a topic you avoid, deflect in 'reply' and set 'suspicion' to at least +1 (or +2 if they push)."* This couples the behaviour to a numeric value, which is what the schema enforces. | medium + | neutral | low |

Holding for cycle 5:
- Info-dump constraint ("max 2 topics per reply")
- Hook-block restructuring (intents not lines)
- Wider chat-history window if consistency stays poor

### Decisions

- Apply G + H + J for Cycle 4.
- Watching: stuttering disappears (T15/T16/T17/T20), Storm Year deflections now move suspicion, voice quality in well-functioning turns (T3, T8, T14) does not regress.

