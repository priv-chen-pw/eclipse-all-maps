# Eclipse All Maps

> **Early version.** This mod is new and not thoroughly tested yet. Please report bugs and balance issues on [GitHub](https://github.com/priv-chen-pw/eclipse-all-maps).

Adds the four portal-only maps to the stage rotation of Eclipse runs and keeps the run on that rotation. Made mainly for no-loop Eclipse climbs.

| Stage | Added map | Preview | Changes |
| --- | --- | --- | --- |
| 2 | Reformed Altar | ![Reformed Altar](https://raw.githubusercontent.com/priv-chen-pw/eclipse-all-maps/main/docs/images/reformed-altar.jpg) | • Stage 2 chest and monster credits<br>• No green portal |
| 3 | Treeborn Colony | ![Treeborn Colony](https://raw.githubusercontent.com/priv-chen-pw/eclipse-all-maps/main/docs/images/treeborn-colony.jpg) | • No green portal<br>• Golden Dieback after looping |
| 4 | Conduit Canyon | ![Conduit Canyon](https://raw.githubusercontent.com/priv-chen-pw/eclipse-all-maps/main/docs/images/conduit-canyon.jpg) | • Teleporter leads to stage 5, not the Alloyed path<br>• One boss fight: Solus Control Unit, Alloy Worship Unit, Solus Amalgamator or Alloy Hunter, equally likely<br>• Stage 4 monster credits |
| 5 | Void Locus | ![Void Locus](https://raw.githubusercontent.com/priv-chen-pw/eclipse-all-maps/main/docs/images/void-locus.jpg) | • Primordial Teleporter instead of Deep Void Signals, so no void fog<br>• Sky Meadow's chests, shrines, printers and drones next to its void interactables<br>• First loop: half Sky Meadow's monsters, half its own, no Void Jailers<br>• Stage 5 chest credits (220 → 545) |

Each added map is as likely as any other map of its stage. Everything can be switched off in **Settings → Mod Options**. Only the host needs the mod.

## Other changes

* **No portals off the rotation.** No green portals open: not from teleporters, Halcyon Shrines, Gilded Coast or the Bazaar's geodes. Solved access codes and Siren's Call no longer open a portal to Solutional Haunt. You still get the access codes' extra boss and item, and on stage 3 they still open their portal to Conduit Canyon. Gold portals to Gilded Coast are unchanged.

### Halcyon Shrine

![Halcyon Shrine](https://raw.githubusercontent.com/priv-chen-pw/eclipse-all-maps/main/docs/images/halcyon-shrine.jpg)

Can spawn on every stage, with its usual cost and odds. From stage 4 it is weaker: its monster waves get half the credits, and its Halcyonite gets half the bonus from the gold (at most ×5.2 health and ×2.75 damage instead of ×10 and ×4.75).

### Prime Meridian and Neural Sanctum

![The frog on Commencement](https://raw.githubusercontent.com/priv-chen-pw/eclipse-all-maps/main/docs/images/frog.jpg)

After Mithrix, pet the frog 10 times. Besides the portal to the Planetarium (Voidling), two more open: Prime Meridian (False Son) and Neural Sanctum (Solus Heart). Rebirth and accepting Solus Heart's offering both count as Eclipse wins.

### Alloy Hunter

![Alloy Hunter](https://raw.githubusercontent.com/priv-chen-pw/eclipse-all-maps/main/docs/images/alloy-hunter.jpg)

From stage 4 it can be the teleporter boss on any map, as likely as any of the map's own bosses. It comes alone, plus one more per Shrine of the Mountain. Each one gets the health of the extra hunters the boss credits would have bought, so the total health is the same as any other boss fight. The Alloy Worship Unit on Conduit Canyon works the same way.

## Settings

In game under **Settings → Mod Options**, or in r2modman's config editor. Changes apply from the next stage. Everything is on by default unless marked (off).

* **General:** Add all maps to Eclipse (the whole mod) · Also in normal runs (off) · Added map chance (1×, from 0.1× to 5×) · Balance chest and monster credits
* **Stage 1-5:** a checkbox for every map of the stage, vanilla or added. A map switched off doesn't roll and isn't offered by Lunar Seers.
* **Void Locus:** Primordial Teleporter (off: Deep Void Signals, so a run that rolls Void Locus ends with Voidling) · Stage 5 monsters · Stage 5 interactables
* **Conduit Canyon:** Teleporter to stage 5 · Robot bosses (off: Solus Amalgamator, or Alloy Hunter)
* **Colossus Path:** No green portals · Frog portal to Prime Meridian · Halcyon Shrine on every stage · Weaker Halcyon Shrines from stage 4
* **Alloyed Path:** No Solutional Haunt portals · Frog portal to Neural Sanctum
* **Bosses:** Alloy Hunter as a normal boss (off, together with Robot bosses: vanilla's scripted Conduit Canyon fight)
* **Debug:** Only added maps (off): only the added maps roll on stages 2-5. For testing.

## Requirements

Anything that needs a DLC that is off for the run is skipped.

* **Seekers of the Storm:** Reformed Altar, Treeborn Colony, Halcyon Shrine, Prime Meridian portal.
* **Alloyed Collective:** Conduit Canyon, Alloy Hunter, Neural Sanctum portal.
* **Survivors of the Void:** Void Locus.

Risk Of Options shows the settings in game; without it they are still in the config file.

## Credits

Images from the [Risk of Rain 2 Wiki](https://riskofrain2.wiki.gg/).
