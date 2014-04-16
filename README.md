LootWhore
=========

Terraria Plugin

This mod allows you to watch and report on certain items that are picked up as well as a Loot Rolling system instead of "first come first serve" item ninja'ing.

Versions
1.2 - First on github

Commands
/lw
/lootwhore - Gives help on available commands

/watch add XXX - Add item ID to watch list
/watch del|delete|remove XXX - Remote item ID from watch list
/watch save - Save the current list of watched items to config file

/items
/loot - Shows information on you or another player's loot history.
   * Number of times picked up items
   * Number of watched items picked up
   * Number of loot rolls won
   * Last X amount of items picked up
   * Total amount of coins picked up

/roll on|off - enable or disable the loot rolling system; defaulted to off
/roll list - Lists the items in queue to be rolled on, as well as items won waiting for inventory space for you
/roll idle pass|roll - Toggle your choice for action when you idle on a loot roll-off. Auto pass or auto roll on item.

/pass - Pass on an item for loot rolling

TODO List
Store watch list in database and not config file
Add permissions to loot roll enabling
Allow only nearby players to roll on loot or base on Team color
