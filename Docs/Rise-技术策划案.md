# Rise — Technical Design Document

## Scope
This document focuses on the shipping implementation for climbing input, momentum, tool states, rest points, scene composition, UI, and audio hooks.

---

## 1. Project Goal
Deliver a playable mountain-climbing experience in which the player controls both hands with the mouse, uses leg kicks and inertia to gain height, and manages a small set of tool and rest decisions.

- Core loop: observe holds, grab, swing, kick, relocate, and recover at rest points.
- The game must remain understandable from the first minute and still have tension near the summit.
- All systems should support the feeling of climbing, fatigue, and risk instead of competing with it.

---

## 2. System Overview
| System | Responsibility | Notes |
|--------|---------------|-------|
| Input | Mouse hand control, Q/E leg kicks, W tool equip, scroll tool switching | All input must be readable and low friction |
| Movement | Body physics, hand grabs, swing momentum, piton rescue | Inertia must matter |
| Tools | Anchor and rope | Both modify route safety |
| Rest | Short rest and long rest at designated points | Some points only allow short rest |
| UI | Stamina, health, sanity, hunger, cold, tool state, prompts | Minimal and persistent |
| Audio | Climb foley, swing, tool feedback, ambience, summit/fail stings | Primary feedback channel |

---

## 3. Player Rules
| Item | Rule |
|------|------|
| Base stamina | 100 |
| Grab cost | 2 stamina per successful grab |
| Hold drain | 1 stamina per second per hand while holding |
| Leg kick | Q and E consume a small amount of stamina and add momentum |
| Anchor | Next grab after equip places an anchor; grabbing and holding the anchor costs 0 stamina |
| Rope | Equip with W and mouse wheel; choose a landing point in range, fire rope, and then grab rope points for 0 stamina |

- Two hands can be active independently.
- Momentum should carry the body when the player swings a stable arm or uses both legs.
- Low stamina should make the character less stable but not unplayable.

---

## 4. Rest Point Rules
The map contains several fixed rest locations. When the player enters the area, a prompt appears and the rest type is determined by the point definition.

| Type | Prompt | Effect | Restriction |
|------|--------|--------|-------------|
| Short rest point | Can short rest | Recover stamina and reduce pressure | No save and no major recovery |
| Long rest point | Can long rest | Save, cook, and recover major states | Must be a safe camp or designated long-rest node |
| Short-only point | Short rest only | Recover stamina with limited relief | No long rest |

---

## 5. Level Design Requirements
- Stone blocks are the basic map kit and must support repeated modular assembly.
- Include platform pieces, ice pieces, anchor-ready surfaces, camp bases, summit marker, sky backdrop, and weather layers.
- Rest points, resource points, and the summit must be visually distinct from generic climbing stones.

---

## 6. UI and Interaction
| UI item | Purpose |
|---------|---------|
| Stamina bar | Show climb pressure |
| Status icons | Hunger, cold, sanity, health |
| Tool state | Anchor / rope / empty |
| Interaction prompt | Rest, supply, camp, or special point entry |
| Key prompts | Basic tutorial readability |

---

## 7. Technical Constraints
- Use one fixed camera-facing climbing plane and a simple body-rig approach that can be tuned quickly.
- Do not build a deep survival system. Only keep states that affect movement decisions and rest timing.
- Keep the route linearly readable with a few branch points for resources or recovery.
- Anchor and rope should be implemented as state switches, not separate complex subsystems.
- All prompts, tool changes, and rest states must be deterministic and easy to debug.

---

## 8. Audio Implementation Notes
- Grab, hold, slip, kick, swing, rope fire, rope settle, anchor place, short rest, long rest, wind, breathing, fire, fail sting, summit sting.
- Audio should be tied to state changes, not only decorative ambience.
- If time is short, prioritize grab, slip, breathing, wind, and tool confirm sounds first.

---

## 9. Asset Delivery Rules
The asset list is intended for direct production handoff. Every row in the CSV should map to one deliverable item or one clearly named variant.

- Map pieces should be modular and snap-friendly.
- Icons should be readable at HUD size.
- Backgrounds should support height and weather readability, not compete with the player silhouette.
- Audio entries should be one-shot or loop assets, clearly named by state and trigger.