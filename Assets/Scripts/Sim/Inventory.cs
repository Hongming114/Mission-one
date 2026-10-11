using System.Collections.Generic;
using UnityEngine;

namespace DungeonCity.Sim
{
    /// <summary>
    /// 背包（文档 L34）：50 个格子，编号 1~50。
    ///   * 同种物品放在同一格；
    ///   * 新物品类型按"第一次获得"的次序占用 1 号、2 号……格子；
    ///   * 同一格同种物品上限 99；
    ///   * 50 个格子都占满后，不再存入新的物品类型（返回 0 = 存放失败）。
    /// 纯逻辑、不依赖场景，方便台架断言。
    /// </summary>
    public class Inventory
    {
        public const int SlotCount = 50;
        public const int StackMax = 99;

        public class Slot
        {
            public string item;
            public int count;
        }

        readonly List<Slot> slots = new List<Slot>();

        /// <summary>已占用的格子数（= 已获得的物品类型数）。</summary>
        public int UsedSlots { get { return slots.Count; } }

        /// <summary>50 格是否已满（新物品类型放不下了）。</summary>
        public bool TypeFull { get { return slots.Count >= SlotCount; } }

        /// <summary>取某一格（编号 1~50），空格返回 null。</summary>
        public Slot SlotAtNumber(int number)
        {
            int i = number - 1;
            if (i < 0 || i >= slots.Count) return null;
            return slots[i];
        }

        public string SlotItem(int number)
        {
            Slot s = SlotAtNumber(number);
            return s == null ? null : s.item;
        }

        public int SlotAmount(int number)
        {
            Slot s = SlotAtNumber(number);
            return s == null ? 0 : s.count;
        }

        /// <summary>某物品的总数。</summary>
        public int Count(string item)
        {
            if (string.IsNullOrEmpty(item)) return 0;
            int n = 0;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].item == item) n += slots[i].count;
            return n;
        }

        public bool Has(string item) { return Count(item) > 0; }

        /// <summary>该物品所在的格子编号（1~50），没有则返回 0。</summary>
        public int SlotNumberOf(string item)
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].item == item) return i + 1;
            return 0;
        }

        /// <summary>
        /// 存放指令：返回实际存入的数量。
        /// 返回 0 表示一个也存不下（同格已到 99，或 50 格已满）——调用方据此终止存放指令、物品留在原地。
        /// </summary>
        public int Add(string item, int amount)
        {
            if (string.IsNullOrEmpty(item) || amount <= 0) return 0;

            Slot s = null;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].item == item) { s = slots[i]; break; }
            }

            if (s != null)
            {
                int room = StackMax - s.count;
                if (room <= 0) return 0;
                int put = Mathf.Min(room, amount);
                s.count += put;
                return put;
            }

            if (TypeFull) return 0;   // 50 格已满：不再存入新的物品类型
            var ns = new Slot();
            ns.item = item;
            ns.count = Mathf.Min(StackMax, amount);
            slots.Add(ns);
            return ns.count;
        }

        /// <summary>HUD / 调试用的一行摘要。</summary>
        public string Describe()
        {
            if (slots.Count == 0) return "空";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < slots.Count; i++)
            {
                if (i > 0) sb.Append("  ");
                sb.Append(slots[i].item).Append(" x").Append(slots[i].count).Append("（").Append(i + 1).Append(" 号格）");
            }
            return sb.ToString();
        }
    }
}
