# HavenDuel: game design (current)

> Pokémon × VALORANT collaboration. A first-person 1v1 duel between Pokémon on Haven.
> Players **aim, dodge in 3D, and choose the right move at the right moment**. Spamming moves loses.
> Licensed Pokémon and VALORANT (Haven) content is used under the collaboration.
> **Platform:** browser, playable on itch.io (Unity WebGL).

**Roster (final, 7):** Pikachu · Greninja · Charizard · Bulbasaur · Eevee · Mimikyu · Ditto

---

## 1. Pillars
1. **Anime-faithful Pokémon.** Each Pokémon moves the way it does in the anime and uses the moves it uses on screen, staged the way the anime stages them.
2. **You dodge by moving.** There's no invulnerable dodge button. How you avoid attacks depends on your Pokémon's own movement (Greninja leaps, Charizard flies, Bulbasaur plods).
3. **Aim matters.** Damaging moves are skillshots with travel time and no aim assist.
4. **Use moves on purpose.** Area moves decide where the opponent can go, and precise moves land the damage. Example: an area move to their left and a shot to their right, so their only safe dodge is into the area.
5. **Spamming loses.** The PP economy (section 5) makes wild spamming run dry, while accurate players keep their moves going.

---

## 2. Match format
```
DRAFT → TEAM PREP → ROUND 1 → ROUND 2 → ROUND 3 (if needed)
```
- **Stage reveal:** the stage for each of the 3 rounds is shown **before the draft**.
- **Draft:** snake order A1, B1, B2, A2, A3, B3. Picks are unique, so 6 of the 7 Pokémon get used.
  - With only 7 Pokémon, the draft is mostly about **denying picks and ordering your lineup**, not about who gets left out.
  - If that feels too fixed in playtests, try one ban each (leaving 5) or mirror picks.
- **Team prep (45s):** for each Pokémon, choose 4 moves from its movepool and 1 held item. You can see the opponent's whole team while doing this.
- **Pairings:**
  - Round 1 is a blind, simultaneous pick.
  - In Rounds 2 and 3 the previous round's winner locks in first and the loser **counterpicks**.
  - Each Pokémon plays only once. Best of 3.
- **Round:** win by KO. Rounds last 120s, and if time runs out the higher HP% wins. Spawns are on Haven's attacker/defender sides and swap every round.
- No levelling or evolution. Each Pokémon is fixed.

---

## 3. Arena: Haven
Each area of Haven favours a different style of fight:

| Area | How it plays |
|---|---|
| A Long, C Long | Long sightlines. Projectiles take longer to arrive, so dodging is easier and setup matters more. Charizard has open sky here. |
| A Short / Sewer, C Lobby | Narrow corridors with little room to dodge sideways. Straight shots are strong. Ceilings take away Charizard's flight. |
| Mid Courtyard | Open, with cover. You need area moves to land anything. |
| Garage, Mid Window | Chokepoints and angles. Good for ambushes (Mimikyu, Greninja). |
| A Heaven, rooftops | High ground, but exposed to attacks from above (Pikachu's Thunder, Charizard dives) |
| Sites A / B / C | Each site has a TM pedestal that unlocks at 30s |

**Flight ceiling:** a soft altitude cap set just above Haven's rooftops, so Charizard can't hover out of reach.

### Stages (one per round)
| Stage | Change | Boosts | Hinders |
|---|---|---|---|
| Sunny Haven | Harsh sun, heat vents | Fire +20%. Bulbasaur's Solar Beam needs no charge. | Water −15% |
| Flooded Haven | Sewer and low ground flood | Water +20% (Greninja swims fast). Electric attacks chain through water. | Fire −20%. Ground-bound Pokémon are slowed in water. |
| Overgrown Haven | Grassy Terrain and tall grass that hides you | Grass +20%. Grounded Bulbasaur heals slowly. | Fire spreads through the grass, which is risky for both players |
| Night Haven | Darkness, fog, shadow zones | Ghost and Dark +15% (Mimikyu, Greninja) | Visibility. Charizard's tail flame gives away its position. |
| Storm Haven | Electric Terrain, lightning pylons, gusting wind | Electric +20%. Grounded Pokémon can't be put to sleep. | Airborne Pokémon get struck, and Charizard's flight is buffeted |

**Day/night:** on every stage except Night Haven, day/night flips at the 60s mark. Day gives Fire and Grass +10%. Night gives Ghost and Dark +10% and lowers visibility.

---

## 4. Movement profiles
Ratings run from 1 (worst) to 5 (best). Low-mobility Pokémon get more HP and ways to defend with moves. Small, agile Pokémon get less HP.

| Pokémon | Ground | Accel | Jump | Air | Water | How it moves | Hitbox / HP |
|---|---|---|---|---|---|---|---|
| Greninja | 5 | 5 | 5 | 4 | 5 | Frog-like ninja: long leaps, clings to and kicks off walls, silent landings, fast swimmer | M / Low |
| Eevee | 4 | 5 | 3 | 3 | 2 | Quick, darting, changes direction sharply | S / Low |
| Pikachu | 4 burst | 4 | 2 | 2 | 2 | Scampers on the ground, hops onto low cover, doesn't climb walls | S / Low-Mid |
| Charizard | 2 | 2 | 2 | **Flight 4** | 1 | **Flies** with big wingbeats. Slow on the ground, and its turns in the air are wide and heavy. A **wing-fatigue** meter makes it land after long flights. | **L** / High |
| Mimikyu | 2 | 3 | 2 | 2 | 2 | Shuffles low in its costume, falls slowly | S / Mid |
| Bulbasaur | 2 | 2 | 2 | 1 | 2 | Sturdy, slow quadruped. Gets extra mobility only through Vine Whip (see section 7). | M / High |
| Ditto | 1 | 2 | 2 | 2 | 3 | Slow blob. Takes on the transformed target's profile, including flight. | S / Mid |

Pokémon only get special movement where their moves give it to them, such as Pikachu's Quick Attack or Bulbasaur vaulting on Vine Whip. Charizard's flight is natural movement, not an ability. There is no shared traversal kit.

---

## 5. PP economy (anti-spam)
- **PP pool:** 100 per Pokémon, regenerating at 2 PP per second.
- **Move costs:** 10–35 PP, scaled to how powerful the move is in the anime.
- **Basic move:** each Pokémon's lightest attack is **free** and gives **+6 PP on hit**.
- **Refunds:**
  - a hit refunds 50% of the cost
  - a hit on a **controlled** target refunds 100% and charges the Signature move. Controlled means slowed, asleep, trapped, wrapped, grabbed, seeded, blinded, or knocked over.
  - a miss refunds nothing
- **Cast commitment comes from the anime staging.** A big move's windup (Pikachu charging Thunderbolt, Bulbasaur gathering light for Solar Beam, Charizard inhaling before Flamethrower) leaves you exposed.
- **Defensive moves cost PP as well.** A deflect or barrier that blocks nothing wastes its PP.
- **Cooldowns** are only short anti-double-tap timers. They are not the anti-spam system.
- **Test gate:** a bot that fires every move the moment it can afford it must lose at least 75% of rounds to a median-skill human.

---

## 6. Strategy and defense
- **Area and control moves** do little damage on their own. They decide where the opponent can dodge, so the follow-up hit lands on a controlled target.
- **Defending with moves, anime-style.** All of these cost PP and need timing or aim:

| Technique | Who |
|---|---|
| **Deflect:** swing a contact move at an incoming projectile | Pikachu (Iron Tail), Greninja (Cut, Aerial Ace), Charizard (Dragon Tail), Mimikyu (Shadow Claw), Bulbasaur (Vine Whip) |
| **Intercept:** your projectile collides with theirs, and type and power decide the clash | Everyone |
| **Counter Shield:** a spinning Thunderbolt dome | Pikachu |
| **Decoys** | Greninja (Double Team) |
| **Disguise:** absorbs the first hit | Mimikyu |
| **Dig:** go underground to escape | Eevee |

- **Movement shapes matchups.**
  - Greninja and Eevee are answered with control (Thunder Wave, Sleep Powder, Leech Seed, Sand Attack).
  - Charizard is answered by pulling it down (Vine Whip, Electroweb, Thunder in the open sky, Storm Haven) or by baiting it into the indoor lanes.
  - Bulbasaur and Ditto are answered with area pressure that drains their PP on defense.
- **Stats and statuses:**
  - Stat stages are ±15% per stage, capped at ±3, and decay after 8s.
  - Statuses (Burn, Paralysis, Sleep, Poison, Confusion, Flinch) have diminishing returns.
  - The type chart is softened: 1.5×, 1×, 0.67×, and 0.25× in place of immunity. STAB is 1.25×. Weak-point hits are 1.5×.

---

## 7. Roster
In team prep you pick 4 moves from each movepool. The free basic move and the Signature (charged by hits on controlled targets) are fixed.
**The licensing team needs to verify each move and how it's depicted against anime episodes.**

### Pikachu (Electric), Ash's Pikachu
- **Basic:** Thunder Shock
- **Movepool:**
  - Thunderbolt
  - Quick Attack: a zig-zag rush and Pikachu's only burst of movement
  - Iron Tail: can deflect
  - Electro Ball: stronger against slower targets
  - Thunder: a strike from the sky onto a marked area. Bonus damage against airborne targets.
  - Electroweb: a net that slows and can tangle a flyer
  - Counter Shield
- **Signature:** 10,000,000 Volt Thunderbolt
- **Passive, Static:** contact may paralyze

### Greninja (Water/Dark), Ash's Greninja
- **Basic:** Water Shuriken
- **Movepool:** Aerial Ace, Cut (deflect), Double Team, Night Slash, Water Pulse, Smokescreen
- **Signature:** Giant Water Shuriken
- **Passive, Protean:** STAB on every move. Its type changes to match its last move.
- Greninja has the smallest PP pool. It defends by moving.

### Charizard (Fire/Flying), Ash's Charizard
- **Basic:** Ember
- **Movepool:**
  - Flamethrower: a sustained stream that you sweep across the target
  - Wing Attack: a diving strike
  - Dragon Tail: a tail swipe that knocks back and can deflect
  - Fire Spin: a vortex that traps, counts as control
  - Dragon Rage: a slow orb that deals fixed damage
  - **Seismic Toss:** grab the target, fly up, and slam them down. Only works on a controlled target.
  - Fly: climb out of sight, then dive onto a marked spot
- **Signature:** Blast Burn
- **Passive, Blaze:** Fire moves get stronger at low HP
- **Charizard's trade-off:** owns the open sky, but has the biggest hitbox and is easy to spot. It's weak indoors and on Storm Haven.

### Bulbasaur (Grass/Poison), Ash's Bulbasaur
- **Basic:** Razor Leaf, spinning leaf blades
- **Movepool:**
  - **Vine Whip.** Lash, grab, or pull, and you can **aim it at terrain** to vault or swing up a ledge, as Bulbasaur does in the anime. A grabbed target counts as controlled. It can also yank Charizard out of the sky.
  - Leech Seed: plants seeds that drain the target to heal Bulbasaur. A seeded target counts as controlled.
  - Sleep Powder: a drifting cloud that causes sleep
  - Solar Beam: a long charge-up, then a big beam. Instant in sun.
  - Seed Bomb
  - Tackle
  - Synthesis: heals, stronger in sun
- **Signature:** a full-sun Solar Beam that you sweep across the arena
- **Passive, Overgrow:** Grass moves get stronger at low HP
- **Bulbasaur's trade-off:** a slow, tough controller. Vine Whip is its only mobility, and it has to land control effects to fight back.

### Eevee (Normal), anime Eevee
- **Basic:** Swift, stars with light homing
- **Movepool:**
  - Quick Attack
  - Take Down: a recoil rush
  - Bite: Dark coverage against Mimikyu, may flinch
  - Shadow Ball: Ghost coverage
  - Sand Attack: blinds, counts as control
  - Baby-Doll Eyes: −Atk
  - Dig
- **Signature:** **Last Resort.** Unlocks only after Eevee has **landed every one of its 4 chosen moves** this round, so it rewards using the whole kit on purpose.
- **Passive, Adaptability:** STAB is 1.4× instead of 1.25×
- **Generalist:** Eevee can use **any TM** from the pedestals, while the others have limited TM lists.

### Mimikyu (Ghost/Fairy), Jessie's Mimikyu
- **Basic:** Shadow Claw (long reach)
- **Movepool:**
  - Shadow Ball
  - Wood Hammer: recoil, coverage against Water
  - Play Rough
  - Shadow Sneak: grabs the target's feet and slows them
  - Will-O-Wisp
  - Swords Dance
- **Signature:** Let's Snuggle Forever, which only works on controlled targets
- **Passive, Disguise:** absorbs the first hit each round

### Ditto (Normal), Duplica's Ditto
- **Basic:** Pound
- **Moves:** Body Slam and Transform
- **Transform:**
  - costs 40 PP and has a long, interruptible windup
  - copies the opponent's moves, type and movement profile, including Charizard's flight
  - Ditto keeps its dot-eyed face, so the opponent can tell
  - Ditto keeps its own PP pool

### Type map and balance watch
- **Starter triangle:** Charizard beats Bulbasaur, Greninja beats Charizard, Bulbasaur beats Greninja. Pikachu hits both Greninja and Charizard hard.
- **Mimikyu vs the Normal types:** Mimikyu is near-immune (0.25×) to Eevee's and Ditto's Normal moves. Eevee's answer is Bite or Shadow Ball. Ditto's answer is Transform.
- **Charizard vs Bulbasaur:** Charizard hits Bulbasaur for 1.5× from both of its types. Bulbasaur has to rely on Sleep Powder, Leech Seed and Vine Whip pull-downs.
- **Charizard's flight** is the biggest balance risk. The knobs are wing fatigue, the flight ceiling, its large hitbox, and the anti-air tools above.
- **Tuning target:** every pairing in the 7×7 matchup matrix wins between 45% and 55% of the time.

---

## 8. Items and moves
- **Moves:** each Pokémon's 4-move set is chosen in team prep.
- **TMs:** site pedestals (A, B, C) unlock at 30s. Grabbing one swaps a move for the rest of the round. Pools lean toward counter moves. Eevee can learn any TM.
- **Held items (1 per Pokémon, chosen in prep):**
  - Leftovers, Life Orb, Focus Sash, Choice Band, Choice Specs, Choice Scarf, Expert Belt, Quick Claw, Wide Lens, Shell Bell
  - Eviolite: +Def and +SpD for Pokémon that aren't fully evolved (Pikachu, Bulbasaur, Eevee)
  - Signature items: Light Ball (Pikachu), Mystic Water (Greninja), Charcoal (Charizard), Miracle Seed (Bulbasaur), Silk Scarf (Eevee), Spell Tag (Mimikyu), Quick Powder (Ditto)
- **Map pickups (current round only):** Ether (+40 PP), Elixir (full PP), Leppa Berry, Potion, Full Heal, X items, Sitrus and Lum Berries

---

## 9. Bot opponent
| Tier | Behaviour |
|---|---|
| Rookie | Aims at your current position, dodges on reaction |
| Trainer | Leads its shots, uses control moves |
| Ace | Predicts your trajectory, especially for flyers and jumpers. Runs two-move combos. |
| Champion | Learns which way you dodge, manages PP and cover, counterdrafts |

The bots respect each Pokémon's movement profile, including Charizard flight paths. A spam bot is kept internally for testing only.

---

## 10. Platform: itch.io web build
- **Engine:** Unity 6.3 LTS (6000.3.28f1) with URP, building to **WebGL 2** for broad browser support. WebGPU can come later.
- **Hosting:** itch.io HTML5 upload, a zip containing `index.html`. Build compression is set to **Gzip with Decompression Fallback**, or turned off, because itch doesn't send Content-Encoding headers.
- **Budgets:**
  - stay well inside itch.io's HTML5 limits on total and per-file size (about 200 MB per file at last check; confirm before shipping)
  - aim for a 60 fps target on a mid-range laptop's integrated GPU
  - stream Haven stage variants as Addressables, not one giant build
- **Browser constraints:**
  - pointer lock and audio both need a user click, so the game needs a click-to-play screen
  - no reliance on multithreading
  - mind memory use, since a WebGL heap of around 2 GB is the practical ceiling
- **Online play (later):** browser clients need WebSocket or WebRTC transport, for example Netcode for GameObjects with the WebSocket transport, plus relay servers. Version 1 is player vs bot.

---

## 11. Roadmap
| Phase | Deliverable |
|---|---|
| 0 | Greninja vs Bulbasaur (high vs low mobility) on a Haven blockout. Movement profiles, projectiles, PP. **Running on itch.io from week 1.** |
| 1 | Charizard flight (the biggest risk), control refunds, defensive techniques, spam-bot test gate |
| 2 | Draft, team prep, pairings, best of 3 |
| 3 | Pikachu, Eevee, Mimikyu, then Ditto last |
| 4 | Stages, items, TMs, bot tiers |

### Built so far (step A: one fight that works)
This covers Greninja and Charizard only. Bulbasaur and the rest of the roster come later.
- **Basic moves.** Both are skillshots with travel time.
  - Greninja: **Water Shuriken**. 7 damage, 42 m/s, no drop, 0.3 s between throws.
  - Charizard: **Ember**. 11 damage, 28 m/s, slight drop, 0.5 s between throws.
  - There's no PP yet; that comes in step B.
- **HP and hurtboxes.**
  - Greninja: 100 HP, 1.6 m tall, 0.38 m hurtbox.
  - Charizard: 150 HP, 2.0 m tall, 0.55 m hurtbox, so it's the big target.
- **Round loop.**
  - A 3 s countdown with movement frozen, then a 120 s fight.
  - A round ends on a KO, or on time with the higher HP% winning (equal HP% is a draw).
  - After a 4.5 s result screen the next round starts. Spawns swap sides every round, and wins are tallied with no match end yet. Best of 3 comes in step D.
- **HUD.** Timer, score, both HP bars with portraits, crosshair, hitmarker (red on a KO), damage numbers, a damage flash, round banners, Charizard's wing stamina, and a click-to-play overlay.
- **Rookie bot.**
  - Aims at your current position without leading, with a little aim wobble and hesitation between throws.
  - Dodges incoming shots after a 0.22–0.4 s reaction.
  - Strafes when it can see you, and otherwise finds you along a NavMesh built from the site when the round loads.
- **Online.** The relay host (the lowest player id) runs the rounds. Each player owns their own HP, and the shooter reports hits.
- All tunables live in `Assets/Data/CombatConfig.asset`, which **HavenDuel → Build Combat Assets** rebuilds. Assets are downloaded from the web, never generated; sources and licenses are in `Assets/Art/SOURCES.md`.

**Risks:**
- Charizard's flight balance
- slow Pokémon may not feel fun to play
- Greninja may be uncatchable
- anime-accuracy sign-off from the licensor
- WebGL performance and build size
