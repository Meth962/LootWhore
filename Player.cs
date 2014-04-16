using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;

namespace LootWhore
{
    public class Player
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public List<string> Loots { get; set; }
        public UInt32 ItemsCollected { get; set; }
        public UInt32 PreciousItemsLooted { get; set; }
        public UInt32 Money { get; set; }
        public SByte Roll { get; set; }
        public bool PassOnIdle { get; set; }
        public UInt32 RollsWon { get; set; }
        //public List<Item> QueuedItems { get; set; }

        public string LootHistory
        {
            get
            {
                return string.Join(",", Loots);
            }
        }

        public Player()
        {
            Loots = new List<string>();
        }

        public Player(int index) : this()
        {
            Index = index;
        }

        public Player(int index, string name) : this()
        {
            Index = index;
            Name = name;
        }

        public void AddLoot(string itemName, int stackSize, bool precious)
        {
            ItemsCollected++;
            if (precious)
                PreciousItemsLooted++;
            if (Loots.Count > 10)
            {
                Loots.RemoveAt(0);
            }

            if (stackSize > 1)
                Loots.Add(string.Format("{0} ({1})", itemName, stackSize));
            else
                Loots.Add(itemName);
        }
    }
}
