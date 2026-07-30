## 1.0.1

* Fixed time trial locations not sending properly.

## 1.0.0

`New:`

\- \[APWorld/Client] Added checks for completing races/cups on 150cc/100cc/50cc. These get added based on your cc requirement setting in the yaml, and send out based on the highest cc completed

\- \[APWorld/Client] Added 3 new fillers: Random Item Box, Start Boost Helper, Inspirational Garfield Quote

\- \[APWorld/Client] Added 5 new traps: Mirror Trap, Sleep Trap, Grayscale Trap, Broken Drift Trap, Bounce Trap

\- \[APWorld/Client] De-coupled the Time Trial locations from the time trial goal, allowing you to randomize time trials without setting time trials as your goal. Time Trials now unlock upon unlocking access to a specific race/cup

\- \[APWorld] Allowed you to set a required % of Puzzle Pieces to goal a Puzzle Piece Hunt slot

\- \[APWorld] Added new options for handling traps and how long they should be active for

\- \[Client] Added DeathLink support

\- \[Client] Added new art for the main menu logo and puzzle piece icons, courtesy of @Flow

\- \[Client] Added mod config options for lapsanity placement requirement, CPU item handling with itemsanity, and toggling on/off item mania mode

\- \[Client] Added extra text displays of the status of each race/cup/time trials checks in the relevant selection menus, as well as lapsanity.

\- \[Client] Revamped the Gallery menu to be used as a tracker for various items you've been sent, as well as a counter for how much puzzle pieces you've gotten. It's now labeled as "Archipelago" with the Archipelago logo.

\- \[Client] Added extra text on the character selections to display if you've sent checks for winning with them or not



`Changes:`

\- \[APWorld/Client] Removed the ability to split up hats/spoilers by tiers or progressively. These are now just randomize on or off, automatically giving you the highest tier hat/spoiler

\- \[APWorld] Simplified item names across the whole APWorld

\- \[Client] Changed the cc\_requirement handling to allow you to play a higher cc than you set (for example, playing 150cc with 100cc requirement set) and still make checks count

\- \[Client] Changed item randomization to give you a 50% chance to select an item you've been given, instead of giving you nothing, if you haven't received every item yet.

\- \[Client] Changed lapsanity to require you to play on your set cc\_requirement or higher for it to count.

\- \[Client] Goal tracking is now handled via a json file storing data, rather than by checking what locations you have sent. This fixes collect causing issues with auto goaling slots, as you will now have to goal all races manually

\- \[Client] Changed the top screen notification display to display colors, and have a drop shadow to be seen easier

\- \[Client] Filtered out warning about compressed websockets and !help message from the notification display



`Fixes:`

\- \[Client] Fixed a bug where the Bombastic Spoiler item gave you the Apprentice Hat

\- \[Client] Fixed the item roulette animation not happening if you picked up and item box and got an item you haven't unlocked

\- \[Client] Fixed bug where you could select characters/karts you don't have unlocked via the Garage menu.

\- \[Client] Fixed the top screen notification display not scaling properly for resolutions

## 0.5.8

* Fixed a bug where you couldn't gain any items after collecting all your itemsanity checks, due to a small code issue

## 0.5.7

* Fixed puzzle piece AP logo texture swaps not working consistently (or really at all)
* Changed the moment you connect to the Archipelago, to avoid issues with going through menus while not connected
* Made various improvements to how the game handles disconnecting mid-game
* Changed the Garage item unlock text for spoilers/hats to appear dependent on your slot data settings
* Changed item boxes to prioritize giving you new itemsanity items first, for checks, before anything else. This makes it more consistent to gain the item box checks
* Changed item boxes to slightly prioritize giving you springs until you've collected all 3 puzzle pieces in the track, if puzzle piece randomization is enabled.

## 0.5.6

* Random thunderstore adjustments

## 0.5.5

* Fixed random lap count triggering on time trials. Time trials are now completely unaffected
* Added a config option to filter to only messages relevant to your slot
* Added a config option to hide messages entirely from Archipelago
* Added a config option to disable stat randomization. This is primarily for if you get screwed over by it when going for Platinum time trial medals.

## 0.5.3

* ACTUALLY FIXED ISSUE WITH LAPSANITY

## 0.5.2

* Fixed issue where the final lapsanity check would not send on races.

## 0.5.1

* Fixed issue where lapsanity checks would not send.

## 0.5.0

* Kart and Character stats randomizer! Randomize the stats of every kart and character, with options to have good, bad, or medium stats on average.
* Lap Count is now split into Lap Count (2-10) and Single Lap Mode (1). Single lap is significantly harder, this should make it harder to accidentally mess yourself up
* Lap sanity! Gives out a check every time you finish a lap in first place.
* Puzzle Pieces are no longer named , now they're all just called "Puzzle Piece".
* New option, Item Mania. CPUs always receive 3 copies of an item if possible. Because we want you to suffer.
* Fixed puzzle pieces being marked as progression items even when they're not required for the goal.
* Fixed grand prix races not sending individual race checks.

## 0.4.4

* Added config option to override slot data lap count and set your own (default 0, if set to 0 it uses slot data)
* Fixed issue where Kart and Character rando would break the character/kart select if set to false.
* Incremented client version to 0.6.6

## 0.4.3

* Fixed issue where cups wouldn't unlock for single race and time trial if you didn't have the cup unlock with cups\_and\_races set for randomize\_races

## 0.4.2

* Fixed issue where cups would be unlocked in grand prix when not all races are unlocked with some yaml settings.
* Fixed issue where the "Races" goal would never successfully complete its goal.
* Fixed issues with error messages relating to filler things

## 0.4.1

**(REQUIRES APWORLD v0.4.0 TO USE)**

* Updated Archipelago client to 0.6.5, hopefully fixing issues with receiving items.

## 0.4.0 (First Beta Release)

**(REQUIRES APWORLD v0.4.0 TO USE)**

* Added Kart randomization, and checks for completing races with a specific kart
* Added Character randomization, and checks for completing races with a specific character
* Added the option to disable CPU using items in the yaml settings
* Added the option to only gain springs from item boxes in the yaml settings (to help with puzzle piece rando)
* Various code cleanup in various places

## 0.3.6

**(REQUIRES APWORLD v0.3.2 TO USE)**

* Fixed issue where Progressive Hats/Spoilers wouldn't be progressive and would give the full tier at once
* Fixed issue where setting Progressive Cups to true and Randomize Races to cups\_and\_races would make menus not function properly
* Other minor fixes

## 0.3.5

**(REQUIRES APWORLD v0.3.2 TO USE)**

* Fixed bug where CPUs wouldn't use items at all
* Added yaml option to set if enemies should use items or not (apworld v0.3.2)
* Lap count yaml option is now supported on apworld v0.3.2
* Revamped notification system to instead just display all messages sent in the archipelago server. This allows checks sent and items received to be shown properly.
* Adjusted the notification display to (hopefully?) resize properly with bigger messages and resize better for other screens

## 0.3.4

* Huge amount of code re-organization
* Added support for lap count via yaml setting (not implemented in apworld as of writing yet)
* Fixed some bugs with progressive cups not correctly loading occasionally
* Fixed some slot data access issues
(Huge thanks to Felucia for working on this update!)

## 0.3.3

* Misc fixes

## 0.3.2

* Fixed an issue where items wouldn't work at all if item randomization was turned off

## 0.3.1

* Removed hot reload plugin, oops

## 0.3.0

* Added Item Randomization
* Added Hat/Spoiler Randomization
* Added Time Trial Goal
* Added the choice to select a specific CC or time trial medal needed for the goals/checks
* The game will now store your last used login and autofill it upon startup.
* Fixed puzzle piece issues

## 0.2.3

* Bumped up the connection client to 0.6.4, which maaaaaybe fixes notification issues?

## 0.2.2

* ACTUALLY fixed issue where unlocking a cup would not unlock all of the single tracks.
* Fixed issue where time trial courses were not selectable ever.

## 0.2.1

* Fixed issue where unlocking a cup would not unlock all of the single tracks.
* Fixed issue where the golden puzzle piece icons in the menu would still display as puzzle pieces.
* Opened back up the time trial button (still no support for this yet, though)

## 0.2

* **Race Randomization**

  * In tandem with the 0.2 apworld, the mod now properly handles randomized races, and handles the goal for these.
* **UI Rebuild**

  * The game now removes or blocks menu boxes and tabs that you cannot use, making it a lot easier to tell what you have vs don't have.
  * The archipelago puzzle piece texture swaps now only occur if you actually have puzzle pieces.
  * The archipelago counter on the track select screen now displays **how many of the checks you have obtained**, rather than displaying **how many of the puzzle pieces you've received as items.**
  * The Gallery menu section now displays art progress based on received puzzle piece items (making it a useful place to see how many you have)
  * Time Trials are now blocked in the menu, as they are not supported yet. This is the same for Splitscreen/Multiplayer.
* **Other Fixes**

  * Fixed a lot of connectivity issues and added more syncing fixes, to hopefully avoid the game failing to give your items.
  * Reduced the font size of the Archipelago notification text.

## 0.1.4

* Fixed the game failing to connect to archipelago.gg.

## 0.1.3

* Added an in game text notifier on the top of the screen when you get sent an item.
* Fixed a bug where locations wouldn't be sent properly unless you restarted the game in some cases.
* Changed the item handling to hopefully be a little better and have less issues.



## 0.1.2

* Changed the puzzle piece icons to add/remove based on your locations sent. Kind of buggy though, sometimes doesn't sync properly until you restart the game.
* Fixed issues where progressive cups wouldn't work for the Burger or Ice Cream Cups.
* Fixed issues with sending puzzle piece checks

## 0.1.1

* Fixed some issues with progressive cups not working properly and locking you out of all cups

## 0.1

* Initial Release



