playtest/ is NOT a mod folder. Nothing in here is installed as-is.

tools/pack.py folds every Config/*.xml found under playtest/<anything>/Config/ into
Hospital/Config/ when it builds a release, appended at the end of the matching file,
or created new if Hospital has no such file.

zzzz_FrilBlocks is Fril's own "Area 7 Creative Block". It sets CreativeMode to None on
every block, item and item modifier, so a player cannot open the creative menu and take
what the challenge is supposed to make them earn.

WHY IT LIVES HERE AND NOT IN Mods\

Because merging it at build time costs nothing, and having it in Mods\ costs two things.

Its own ModInfo warns: "Remove this mod when updating items.xml or blocks.xml". With the
folder in Mods\ you have to remember that every single time you work on the mod, and your
own install has no creative menu until you do. Merged at build time instead, your install
keeps its creative menu, the repo stays clean, and players still get one folder fewer.

THE LOAD-ORDER POINT, because the zzzz_ prefix was the mechanism and not decoration

7 Days to Die applies mod config in folder order, so zzzz_FrilBlocks was named to load
after everything it overrides. Inside Hospital\Config those rules run at Hospital's
position instead.

For this pack that changes nothing: "Hospital" already sorts after all eight other folders.
If a tenth folder is ever added whose name sorts AFTER "Hospital", and it adds items or
blocks, those items will appear in the creative menu and this is why. The fix then is to
go back to a separate zzzz_ folder.
