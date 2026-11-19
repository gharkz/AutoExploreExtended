# Changelog

## 1.2.0
- Lawnmower path (multi-tool, on by default): explores and works the map in back-and-forth lanes as wide as your
  sight, chunk by chunk, then nearest-first for stragglers. Covers the playable area only.
- Lawnmower path for AutoExplore's own modes (experimental, off by default).
- Fight in place (optional): never chase enemies; fight with what's in reach, then resume the route or the job.
- Tool in Hand: the held tool also does its usual jobs on top of the preset.
- Pickaxe & Axe Work: objects (trees, rocks), walls, or both; tool choice follows the game's own harvest rules.
- Only seen tiles are worked; seen animals within a few tiles are sheared first.
- Fixes: interrupted jobs retry instead of stalling the run; no more "nothing to do" with work left.

## 1.1.1
The starter all_tools preset starts out of the cycle: multi-tool mode never appears until you turn a preset on.

## 1.1.0
Multi-tool mode: named presets of tools; one pass over the map, swapping to the best carried tool for each job.
Sickle first on reapable plants; lockpick before crowbar. Presets join AutoExplore's mode key after Combined or use
their own key; reorder with arrows. New Multi-tool settings tab.

## 1.0.4
New setting, on by default: shovel mode skips platforms and bridges (and the floor beneath them).
Digging a raised platform out from under you can leave you stuck.

## 1.0.3
Key settings now ask you to press a key, like the game's own key binding (Esc cancels, Del clears),
instead of opening a list of every key.

## 1.0.2
Settings say "locked containers" instead of "locked chests" (clam shells and the like count too);
practice chests keep their name. Tool labels read "when shovel/shears tool selected". No behaviour change.

## 1.0.1
Internal rename: the DLL, namespace and mod ID now say AutoExploreExtended (they still said
Lockpicking from development). Your settings and lists are carried over automatically.

## 1.0.0
First public release: crowbar/lockpick chests, shearing, shovel floors and tracks, tracks while
mining, the settings window, named restriction lists, material filters, Instant Actions
compatibility, and English/Japanese/Simplified Chinese UI.
