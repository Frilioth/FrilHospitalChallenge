names.txt is NOT shipped with this mod, on purpose.

It is the server owner's own file — the list of who has completed the challenge
and which zombie type carries their name. Shipping a copy would overwrite yours
every time you updated the mod, which is exactly what happened once.

To use it, create Config/names.txt yourself:

    PlayerName=zombieClassName

USE THE CLASS NAME, NOT THE DISPLAY NAME.

    Devilskut=zombieYo          correct
    Devilskut=Yo                works on English clients ONLY

Display names are translated, so a file written against them matches nothing for
a French, German or Japanese player. Class names are the same in every language.
The class list is the Key column of the game's own Data/Config/Localization.csv.

Variants are separate classes, so add a line for each tier you want covered:

    Devilskut=zombieYo
    Devilskut=zombieYoFeral
    Devilskut=zombieYoRadiated

A Feral entry renders as "Feral PlayerName" in the player's own language.

Irregular class names that catch people out:
    Lab Worker      zombieLab
    Big Mama        zombieFemaleFat
    Hazmat          zombieMaleHazmat
    Businessman     zombieBusinessMan     (capital M)
    Utility Worker  zombieUtilityWorker
    Party Girl      zombiePartyGirl

HOW MANY GET RENAMED. With one name on a type, about 1 in 3 of that type carry
it and the rest keep their normal name. Two names is 1 in 2. Ten names is 5 in 6.
Some always stay unnamed so the type itself is still recognisable.

CHECKING A LINE TOOK. A typo fails silently — the only symptom is that the name
never appears. The log confirms it:

    [Hospital] Loaded 4 named zombie entries for 2 zombie types.
    [Hospital] Named zombie: id=963 Yo -> Devilskut

The second line only prints when you LOOK AT a renamed zombie, because it hangs
off the target bar. No lines means nobody looked, not that nothing was named.
