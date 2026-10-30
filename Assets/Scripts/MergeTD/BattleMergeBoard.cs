using System;
using System.Collections.Generic;

namespace MergeTD
{
    public sealed class MergePiece
    {
        public string InstanceId { get; }
        public string UnitId { get; }
        public int Star { get; }
        public MergePiece(string instanceId, string unitId, int star)
        { InstanceId = instanceId; UnitId = unitId; Star = star; }
    }

    // A per-battle board. Definition IDs and crafting tiers are never modified.
    public sealed class BattleMergeBoard
    {
        private readonly MergePiece[] slots;
        private readonly HashSet<string> deck;
        public int Capacity => slots.Length;
        public int MaxStar { get; }
        public int Count { get; private set; }
        public event Action Changed;

        public BattleMergeBoard(IEnumerable<string> deckIds, int capacity, int maxStar)
        {
            if (deckIds == null) throw new ArgumentNullException(nameof(deckIds));
            deck = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in deckIds)
                if (string.IsNullOrWhiteSpace(id) || !deck.Add(id))
                    throw new ArgumentException("Deck IDs must be nonempty and unique.");
            if (deck.Count < 1 || deck.Count > 5) throw new ArgumentException("Deck requires 1 to 5 types.");
            if (capacity < 1 || maxStar < 2) throw new ArgumentOutOfRangeException();
            slots = new MergePiece[capacity];
            MaxStar = maxStar;
        }

        public MergePiece Get(int slot)
        {
            if (!InBounds(slot)) throw new ArgumentOutOfRangeException(nameof(slot));
            return slots[slot];
        }

        public bool TrySpawn(string unitId, out int slot, out string message)
        {
            slot = -1;
            if (unitId == null || !deck.Contains(unitId)) { message = "출전 덱에 없는 유닛입니다."; return false; }
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) continue;
                slots[i] = new MergePiece(Guid.NewGuid().ToString("N"), unitId, 1);
                slot = i;
                Count++;
                message = "1성 유닛을 소환했습니다.";
                Changed?.Invoke();
                return true;
            }
            message = "보드가 가득 찼습니다. 먼저 같은 유닛을 합쳐 주세요.";
            return false;
        }

        public bool CanMerge(int source, int target)
        {
            if (!InBounds(source) || !InBounds(target) || source == target) return false;
            var a = slots[source]; var b = slots[target];
            return a != null && b != null && a.UnitId == b.UnitId && a.Star == b.Star && a.Star < MaxStar;
        }

        // Move into an empty slot, or atomically consume source and promote target.
        public bool TryMoveOrMerge(int source, int target, out string message)
        {
            if (!InBounds(source) || !InBounds(target)) { message = "보드 밖으로 이동할 수 없습니다."; return false; }
            if (source == target) { message = "다른 칸을 선택해 주세요."; return false; }
            var a = slots[source]; var b = slots[target];
            if (a == null) { message = "이동할 유닛이 없습니다."; return false; }
            if (b == null)
            {
                slots[target] = a; slots[source] = null;
                message = "유닛을 이동했습니다.";
            }
            else
            {
                if (a.UnitId != b.UnitId) { message = "같은 종류의 유닛끼리 합칠 수 있습니다."; return false; }
                if (a.Star != b.Star) { message = "같은 성급끼리 합칠 수 있습니다."; return false; }
                if (a.Star >= MaxStar) { message = "최대 성급입니다."; return false; }
                slots[target] = new MergePiece(b.InstanceId, b.UnitId, b.Star + 1);
                slots[source] = null;
                Count--;
                message = $"합성 성공! {b.Star + 1}성으로 강화했습니다.";
            }
            Changed?.Invoke();
            return true;
        }

        public void Reset()
        {
            Array.Clear(slots, 0, slots.Length);
            Count = 0;
            Changed?.Invoke();
        }

        private bool InBounds(int index) => index >= 0 && index < slots.Length;
    }
}
