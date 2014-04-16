using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;

namespace LootWhore
{
    public class WinQueue
    {
        public Item Item { get; set; }
        public int PlayerID { get; set; }

        public WinQueue(Item item, int playerID)
        {
            Item = item;
            PlayerID = playerID;
        }
    }
}
