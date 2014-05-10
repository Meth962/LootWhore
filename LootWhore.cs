using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TerrariaApi.Server;
using Terraria;
using TerrariaApi;
using TShockAPI;
using System.Reflection;
using System.IO;
using System.Timers;

namespace LootWhore
{
    [ApiVersion(1, 16)]
    public class LootWhore : TerrariaPlugin
    {
        byte rollTimeout = 60;
        Timer watcher = new Timer(1000);
        List<Player> players = new List<Player>();
        List<WinQueue> winQueue = new List<WinQueue>();
        string itemConfigFile = "ItemsToWatch.config.txt";

        #region itemsToWatch
        List<int> itemsToWatch = new List<int>() { 
            160,
            256,
            257,
            258,
            367,
            426,
            434,
            489,
            490,
            491,
            514,
            547,
            548,
            549,
            672,
            758,
            788,
            842,
            843,
            844,
            854,
            855,
            899,
            900,
            905,
            994,
            1121,
            1122,
            1123,
            1129,
            1132,
            1141,
            1155,
            1157,
            1169, //Bone Key
            1170,
            1178,
            1182,
            1225,
            1248,
            1255,
            1258,
            1259,
            1273,
            1281,
            1294,
            1295,
            1296,
            1297,
            1299,
            1305,
            1311,
            1313, //Book of Skulls
            1327,
            1360,
            1361,
            1362,
            1363,
            1364,
            1365,
            1366,
            1367,
            1368,
            1369,
            1370,
            1371,
            1520,
            1570,
            1782,
            1784,
            1798,
            1801,
            1802,
            1803,
            1804,
            1805,
            1806,
            1807,
            1826,
            1829,
            1835,
            1837,
            1845,
            1855,
            1856,
            1871,
            1873,
            1910,
            1916,
            1929,
            1930,
            1931,
            1946,
            1947,
            1959,
            1960,
            1961,
            1962,
            2104,
            2105,
            2106,
            2107,
            2108,
            2109,
            2110,
            2111,
            2112,
            2113,
            2218 };
        #endregion

        Boolean rollOnLoot = true;
        Boolean forcePassOnIdle = true;
        List<Item> itemsToRoll = new List<Item>();

        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public override string Name
        {
            get { return "Loot Whore"; }
        }

        public override string Author
        {
            get { return "Meth"; }
        }

        public override string Description
        {
            get { return "Exposes the loot whores for who they are! Able to use loot rolling on precious item drops."; }
        }

        public LootWhore(Main game)
            : base(game)
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ServerJoin.Deregister(this, OnServerJoin);
                ServerApi.Hooks.NpcLootDrop.Deregister(this, OnNpcLootDrop);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
                watcher.Stop();
            }
        }

        public override void Initialize()
        {
            ReadConfig();
            ServerApi.Hooks.ServerJoin.Register(this, OnServerJoin);
            ServerApi.Hooks.NpcLootDrop.Register(this, OnNpcLootDrop);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.ServerChat.Register(this, OnChat);
            watcher.Elapsed += new ElapsedEventHandler(watcher_Tick);
            watcher.Start();
        }

        bool SaveConfig()
        {
            try
            {
                string contents = string.Join("\n", itemsToWatch);
                File.WriteAllText(itemConfigFile, contents);
                return true;
            }
            catch
            {
                return false;
            }
        }

        void ReadConfig()
        {
            if (File.Exists(itemConfigFile))
            {
                string[] lines = File.ReadAllLines(itemConfigFile);
                if (lines.Length > 0)
                    itemsToWatch = new List<int>();

                foreach (string line in lines)
                {
                    try
                    {
                        itemsToWatch.Add(Int32.Parse(line.Trim()));
                    }
                    catch
                    {
                        //ignore bad entry
                    }
                }
            }
        }

        void watcher_Tick(object sender, ElapsedEventArgs e)
        {
            // Continually check progress of rolls
            if (itemsToRoll.Count > 0)
            {
                // Need to check if one person has rolled, essentially starting the countdown for a roll
                int hasRolled = players.Where(p => p.Roll != 0).Count();
                if (hasRolled > 0)
                {
                    rollTimeout--;
                    // Roll timeout expired, force pass or roll
                    if (rollTimeout <= 0)
                    {
                        // Get players who haven't rolled
                        var notRolled = players.Where(p => p.Roll == 0).ToList();
                        foreach (var player in notRolled)
                        {
                            // Force pass or auto roll for idle player
                            if (forcePassOnIdle || player.PassOnIdle)
                                player.Roll = -1;
                            else
                                player.Roll = (sbyte)(new Random().Next(1, 101));
                            TSPlayer.All.SendMessage(string.Format("{0} is idle and {1}.", player.Name, forcePassOnIdle ? "passes" : "rolled a " + player.Roll), Color.Yellow);
                        }

                        // Determine winner
                        CheckLootRoll();
                    }

                    // Give warning of roll timeout last 30 seconds, every 10 seconds.
                    if (rollTimeout <= 30 && rollTimeout % 10 == 0)
                        TSPlayer.All.SendInfoMessage(string.Format("Loot roll ending in {0} seconds.", rollTimeout));
                }
            }

            // Continually check items that players have won, but do not have inventory space for
            if (winQueue.Count > 0)
            {
                List<int> successful = new List<int>();
                for (int q = 0; q < winQueue.Count; q++)
                {
                    var queue = winQueue[q];
                    // When they have room, give them their prize and remove from queue. Look for stackable items.
                    // Since this processes before inventory can be checked, only give on player an item at a time in case they are full after first prize
                    if (TShock.Players[queue.PlayerID].InventorySlotAvailable && !successful.Contains(queue.PlayerID))
                    {
                        successful.Add(queue.PlayerID);
                        TShock.Players[queue.PlayerID].GiveItemCheck(queue.Item.type, queue.Item.name, queue.Item.width, queue.Item.height, queue.Item.stack, queue.Item.prefix);
                        TShock.Players[queue.PlayerID].SendMessage(string.Format("Prize delivered: {0} ({1})!", queue.Item.AffixName(),queue.Item.stack), Color.SeaGreen);
                        winQueue.RemoveAt(0);
                        q--;
                    }
                }
            }
        }

        bool CanStackItem(int playerID, Item item)
        {
            var sameItems = Main.player[playerID].inventory.Where(i => i.netID == item.netID).ToList();
            if (sameItems.Count > 0)
            {
                foreach (var sameItem in sameItems)
                {
                    if (sameItem.maxStack <= sameItem.stack + item.stack)
                        return true;
                }

                return false;
            }
            else
                return false;
        }

        void OnNpcLootDrop(NpcLootDropEventArgs e)
        {
            Item item = TShock.Utils.GetItemById(e.ItemId);
            bool precious = itemsToWatch.Contains(e.ItemId);

            // If new item is watched and roll-off is enabled
            if (rollOnLoot && precious)
            {
                // Build item manually since we will not let the game process the drop
                item.SetDefaults(e.ItemId, false);
                item.Prefix(e.Prefix);
                item.stack = e.Stack;

                // Add to roll off items
                itemsToRoll.Add(item);
                if (itemsToRoll.Count == 1)
                    TSPlayer.All.SendMessage(string.Format("Loot: {0} ({1}). Type /roll for this item", item.AffixName(),item.stack), Color.Pink);
                else
                    TSPlayer.All.SendMessage(string.Format("{0} ({1}) added to loot roll queue.", item.AffixName(),item.stack), Color.LightPink);

                e.Handled = true;
            }

        }

        void OnServerJoin(JoinEventArgs e)
        {
            Player player = players.Where(p => p.Name == Main.player[e.Who].name).FirstOrDefault();
            if (player == null)
            {
                players.Add(new Player(e.Who, Main.player[e.Who].name));
            }
            else
            {
                //Log.ConsoleInfo("Player exists.");
                player.Index = e.Who;
            }
        }

        void OnChat(ServerChatEventArgs e)
        {
            string text = e.Text;
            if (e.Text.StartsWith("/"))
            {
                var sender = TShock.Players[e.Who];
                string[] arr = e.Text.Split(' ');
                switch (arr[0])
                {
                    case "/lw":
                    case "/lootwhore":
                        sender.SendInfoMessage("Available commands for LootWhore: /watch,/items,/loot,/roll,/pass");
                        sender.SendInfoMessage("Use /command ? or /command help for info. E.I. /watch help");
                        break;
                    case "/watch":
                        WatchCommand(sender, arr);
                        e.Handled = true;
                        break;
                    case "/items":
                    case "/loot":
                        LootCommand(sender, arr);
                        e.Handled = true;
                        break;
                    case "/roll":
                        RollCommand(sender, arr);
                        e.Handled = true;
                        break;
                    case "/pass":
                        PassCommand(sender);
                        e.Handled = true;
                        break;
                }
            }
        }

        private void PassCommand(TSPlayer sender)
        {
            if (itemsToRoll.Count <= 0)
            {
                sender.SendWarningMessage("No items to roll on.");
            }
            else
            {
                Player player = players.Where(p => p.Index == sender.Index).FirstOrDefault();
                if (player.Roll != 0)
                    sender.SendWarningMessage(string.Format("You already rolled a {0} on this item! Stop rolling!", player.Roll));
                else
                {
                    player.Roll = -1;
                    TSPlayer.All.SendInfoMessage("{0} passes.",player.Name);
                    CheckLootRoll();
                }
            }
        }

        private void RollCommand(TSPlayer sender, string[] arr)
        {
            if (arr.Length > 1)
            {
                switch (arr[1].ToLower())
                {
                    case "on":
                        rollOnLoot = true;
                        TSPlayer.All.SendMessage(string.Format("{0} set loot rules to roll-off.", sender.Name), Color.SeaGreen);
                        break;
                    case "off":
                        rollOnLoot = false;
                        TSPlayer.All.SendMessage(string.Format("{0} set loot rules to free-for-all.", sender.Name), Color.SeaGreen);
                        break;
                    case "list":
                        StringBuilder sb = new StringBuilder();
                        sb.Append("Loot list: ");
                        foreach (var item in itemsToRoll)
                        {
                            sb.Append(string.Format("{0} ({1}),", item.AffixName(),item.stack));
                        }
                        sb.Remove(sb.Length - 1, 1);
                        sender.SendMessage(sb.ToString(), Color.GreenYellow);
                        sb = new StringBuilder();
                        var inQueue = winQueue.Where(q => q.PlayerID == sender.Index).ToList();
                        if (inQueue.Count > 0)
                        {
                            sb = new StringBuilder();
                            sb.Append("Items Won Awaiting space: ");
                            foreach (var item in inQueue)
                            {
                                sb.Append(string.Format("{0} ({1}),",item.Item.AffixName(),item.Item.stack));
                            }
                            sb.Remove(sb.Length - 1, 1);
                            sender.SendMessage(sb.ToString(), Color.Teal);
                        }
                        break;
                    case "idle":
                        if (arr.Length < 3)
                            sender.SendInfoMessage("Correct use is /roll idle pass|roll.");
                        else
                        {
                            Player player = players.Where(p => p.Index == sender.Index).FirstOrDefault();
                            switch (arr[2].ToLower())
                            {
                                case "pass":
                                    player.PassOnIdle = true;
                                    sender.SendInfoMessage("You have chosen to automatically pass on loot if you do not respond.");
                                    break;
                                case "roll":
                                    player.PassOnIdle = false;
                                    sender.SendInfoMessage("You have chosen to automatically roll on loot if you do not respond.");
                                    break;
                                default:
                                    sender.SendErrorMessage("Proper use is /roll idle pass|roll.");
                                    break;
                            }
                        }
                        break;
                    case "?":
                    case "help":
                        sender.SendInfoMessage("/roll on|off - Toggles loot rolling on item drops.");
                        sender.SendInfoMessage("/roll list - Shows list of items to roll on, and any items awaiting for space in your inventory.");
                        sender.SendInfoMessage("/roll idle pass|roll - Set whether to roll or pass if you're idle on loot rolls.");
                        break;
                    default:
                        sender.SendInfoMessage("Invalid use. Proper use is /roll on|off|list|help|?");
                        break;
                }
            }
            else
            {
                if (itemsToRoll.Count <= 0)
                {
                    sender.SendWarningMessage("No items to roll on. Type /roll ? or /roll help for more info.");
                }
                else
                {
                    Player player = players.Where(p => p.Index == sender.Index).FirstOrDefault();
                    if (player.Roll == -1)
                        sender.SendWarningMessage("You already passed on this item!");
                    else if (player.Roll != 0)
                        sender.SendWarningMessage(string.Format("You already rolled a {0} on this item! Stop rolling!", player.Roll));
                    else
                    {
                        Random rnd = new Random();
                        player.Roll = (sbyte)rnd.Next(1, 101);
                        TSPlayer.All.SendInfoMessage(string.Format("{0} rolled a {1}.", player.Name, player.Roll));
                        CheckLootRoll();
                    }
                }
            }
        }

        private void LootCommand(TSPlayer sender, string[] arr)
        {
            if (arr.Length > 1)
            {
                switch (arr[1].ToLower())
                {
                    case "help":
                    case "?":
                        sender.SendInfoMessage("/items|loot - Shows your loot information");
                        sender.SendInfoMessage("/items|loot playerName - Show another player's loot info.");
                        break;
                    default:
                        Player player = players.Where(p => p.Name.ToLower().Contains(arr[1].ToLower())).FirstOrDefault();
                        if (player != null)
                        {
                            sender.SendMessage(string.Format("{0}'s loots: {1:n0} Precious: {2:n0} Rolls Won: {3:n0}", player.Name, player.ItemsCollected, player.PreciousItemsLooted, player.RollsWon), Color.Blue);
                            sender.SendMessage(player.LootHistory, Color.Blue);
                            sender.SendMessage(string.Format("Coins Looted: {0}", FormatCoins(player.Money)), Color.LightGreen);
                        }
                        else
                            sender.SendInfoMessage("No player with name containing " + arr[1]);
                        break;
                }
            }
            else
            {
                Player player = players.Where(p => p.Index == sender.Index).FirstOrDefault();
                if (player != null)
                {
                    sender.SendMessage(string.Format("Your loots: {0:n0} Precious: {1:n0} Rolls Won: {2:n0}", player.ItemsCollected,player.PreciousItemsLooted,player.RollsWon), Color.Blue);
                    sender.SendMessage(player.LootHistory, Color.Blue);
                    sender.SendMessage(string.Format("Coins Looted: {0}", FormatCoins(player.Money)), Color.LightGreen);
                }
            }
        }

        private void WatchCommand(TSPlayer sender, string[] arr)
        {
            if (arr.Length > 2)
            {
                switch (arr[1].ToLower())
                {
                    case "add":
                        int newID = 0;
                        if (Int32.TryParse(arr[2], out newID))
                        {
                            if (!itemsToWatch.Contains(newID))
                            {
                                itemsToWatch.Add(newID);
                                sender.SendMessage(string.Format("{0} added to watch list.", Main.itemName[newID]), Color.Orange);
                            }
                            else
                                sender.SendErrorMessage(string.Format("{0} is already on the list.", Main.itemName[newID]));
                        }
                        else
                            sender.SendInfoMessage("Invalid use. Proper use is: /watch add itemNumber");
                        break;
                    case "del":
                    case "delete":
                    case "remove":
                        int delID = 0;
                        if (Int32.TryParse(arr[2], out delID))
                        {
                            if (itemsToWatch.Contains(delID))
                            {
                                itemsToWatch.Remove(delID);
                                sender.SendMessage(string.Format("{0} removed from watch list.", Main.itemName[delID]), Color.Orange);
                            }
                            else
                                sender.SendErrorMessage(string.Format("{0} is not on the list.", Main.itemName[delID]));
                        }
                        else
                            sender.SendInfoMessage("Invalid use. Proper use is: /watch del|delete|remove itemNumber");
                        break;
                    default:
                        sender.SendInfoMessage("The proper use of watch is /watch add|remove itemNumber");
                        break;
                }
            }
            else
            {
                if (arr.Length == 2)
                {
                    switch (arr[1].ToLower())
                    {
                        case "save":
                            if (SaveConfig())
                                sender.SendMessage("Saved Item Watch List successfully.", Color.Orange);
                            else
                                sender.SendErrorMessage("Failed to save Item Watch List");
                            break;
                        case "help":
                        case "?":
                            sender.SendInfoMessage("/watch itemNumber - Toggles an item to be watched or not.");
                            sender.SendInfoMessage("/watch add|remove itemNumber - Add or Remove an item to be watched.");
                            sender.SendInfoMessage("/watch save - Force save watch list to file.");
                            break;
                        default:
                            // User passed a number. We don't know if to add or remove, so check it!
                            int theID = 0;
                            if (Int32.TryParse(arr[1], out theID))
                            {
                                if (!itemsToWatch.Contains(theID))
                                {
                                    itemsToWatch.Add(theID);
                                    sender.SendMessage(string.Format("{0} added to watch list.", Main.itemName[theID]), Color.Orange);
                                }
                                else
                                {
                                    itemsToWatch.Remove(theID);
                                    sender.SendMessage(string.Format("{0} removed from watch list.", Main.itemName[theID]), Color.Orange);
                                }
                            }
                            else
                                sender.SendInfoMessage("Invalid use. Use /watch help or /watch ? for more info.");
                            break;
                    }
                }
                else
                {
                    sender.SendMessage("Proper use of /watch is as follows: /watch save /watch [add|remove] itemID", Color.LightGreen);
                }
            }
        }

        void OnGetData(GetDataEventArgs e)
        {
            if (!e.Handled)
            {
                int plr = e.Msg.whoAmI;
                using (var reader = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                {
                    switch (e.MsgID)
                    {
                        case PacketTypes.ItemDrop:
                            Int16 itemID = reader.ReadInt16();
                            float posX = reader.ReadSingle();
                            float posY = reader.ReadSingle();
                            float velX = reader.ReadSingle();
                            float velY = reader.ReadSingle();
                            Int16 stacks = reader.ReadInt16();
                            byte prefix = reader.ReadByte();
                            bool noDelay = reader.ReadBoolean();
                            Int16 netID = reader.ReadInt16();

                            Item item = Main.item[itemID];
                            bool precious = itemsToWatch.Contains(item.netID);
                            // Check that item is on ground. Item slots < 400 are in the world already; 401 is new drop
                            if (itemID < 400)
                            {
                                // Item picked up/purged if the netID is set to 0
                                if (netID == 0)
                                {
                                    Player player = players.Where(p => p.Index == item.owner).FirstOrDefault();
                                    if (player != null)
                                    {
                                        // If item is coins, add up worth to player coin loot stats
                                        if (item.netID >= 71 && item.netID <= 74)
                                        {
                                            switch (item.netID)
                                            {
                                                case 71: // Copper
                                                    player.Money += (UInt32)item.stack;
                                                    break;
                                                case 72: // Silver
                                                    player.Money += (UInt32)item.stack * 100;
                                                    break;
                                                case 73: // Gold
                                                    player.Money += (UInt32)item.stack * 10000;
                                                    break;
                                                case 74: // Platinum
                                                    player.Money += (UInt32)item.stack * 1000000;
                                                    break;
                                            }
                                        }

                                        // Add loot to player's history
                                        player.AddLoot(item.name, item.stack, precious);
                                    }

                                    // Tattle on players picking up watched items to server
                                    if (precious)
                                        TSPlayer.All.SendMessage(string.Format("{0} picked up: {1} ({2})", Main.player[item.owner].name, item.AffixName(), item.stack), Color.Teal);
                                }
                            }
                            else
                            {
                                // OnNpcDropLoot hook is better for this
                                /*precious = itemsToWatch.Contains(netID);

                                // New Item dropped
                                if (rollOnLoot && precious)
                                {
                                    item = new Item();
                                    item.SetDefaults(netID, false);
                                    item.type = netID;
                                    item.prefix = prefix;
                                    item.stack = stacks;

                                    itemsToRoll.Add(item);
                                    if (itemsToRoll.Count == 1)
                                        TSPlayer.All.SendMessage(string.Format("Drop: {0}. Type /roll for this item", item.AffixName()), Color.Pink);
                                    else
                                        TSPlayer.All.SendMessage(string.Format("{0} added to loot roll queue.", item.AffixName()), Color.LightPink);
                                }*/
                            }
                            break;
                    }
                }
            }
        }

        void CheckLootRoll()
        {
            bool rollingDone = true;

            // Get List of players who didn't roll
            var playersToRoll = players.Where(p => p.Roll == 0);
            foreach (var player in playersToRoll)
            {
                // Check if they are active
                if (TShock.Players[player.Index] != null && TShock.Players[player.Index].Active)
                    rollingDone = false;
            }

            if (rollingDone)
            {
                EndRolling();
            }
        }

        private void EndRolling()
        {
            // Get first item
            Item item = itemsToRoll[0];
            // Get any players with the highest roll
            sbyte max = players.Max(m => m.Roll);
            var winners = players.Where(p => p.Roll == max).ToList();
            // Roll-off any ties until solved.
            while (winners.Count > 1)
            {
                TSPlayer.All.SendInfoMessage("Tie! Automatic roll off.");
                Random rnd = new Random();
                foreach (var winner in winners)
                {
                    winner.Roll = (SByte)rnd.Next(1, 101);
                    TSPlayer.All.SendInfoMessage(string.Format("{0} rolled a {1}.", winner.Name, winner.Roll));
                }

                winners = winners.Where(p => p.Roll == winners.Max(m => m.Roll)).ToList();
            }

            // Give prize to winner
            winners[0].RollsWon++;
            TSPlayer.All.SendMessage(string.Format("{0} won {1}!", winners[0].Name, item.AffixName()), Color.SeaGreen);
            if (!TShock.Players[winners[0].Index].InventorySlotAvailable)
            {
                // Player didn't have room, inform them and add it to the automatic queue
                winQueue.Add(new WinQueue(item, winners[0].Index));
                TShock.Players[winners[0].Index].SendMessage("Your inventory is full! Make room to automatically receive your item!", Color.Red);
            }
            else
                TShock.Players[winners[0].Index].GiveItemCheck(item.type, item.name, item.width, item.height, item.stack, item.prefix);

            // Remove first item in queue and reset rolls
            itemsToRoll.RemoveAt(0);
            rollTimeout = 60;
            foreach (var player in players)
            {
                player.Roll = 0;
            }
            if (Main.player[0].inventory.FirstOrDefault(i => i.name.Contains("Iron Pickaxe")) != null)
            // Check for next item up
            if (itemsToRoll.Count > 0)
                TSPlayer.All.SendMessage("Next Item up for rolling: " + itemsToRoll[0].AffixName(), Color.Pink);
        }

        private string FormatCoins(UInt32 coins)
        {
            UInt32 platinum = coins / 1000000;
            coins -= platinum * 1000000;
            byte gold = (byte)(coins / 10000);
            coins -= (uint)gold * 10000;
            byte silver = (byte)(coins / 100);
            coins -= (uint)silver * 100;
            byte copper = (byte)coins;
            return string.Format("{0}p {1}g {2}s {3}c", platinum, gold, silver, copper);
        }

        private void Broadcast(string text, Color color)
        {
            for (int i = 0; i < Main.player.Length; i++)
            {
                if (Main.player[i].active)
                    NetMessage.SendData((int)PacketTypes.ChatText, i, -1, text, 255, color.R, color.G, color.B, 0);
            }
        }
    }
}
