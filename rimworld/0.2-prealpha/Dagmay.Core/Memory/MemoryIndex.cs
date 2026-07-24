using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Memory
{
    public sealed class MemoryIndex
    {
        private readonly List<SubjectiveMemory> _memories = new List<SubjectiveMemory>();
        private readonly HashSet<MemoryId> _ids = new HashSet<MemoryId>();

        public int Count => _memories.Count;

        public void Add(SubjectiveMemory memory)
        {
            if (memory is null) throw new ArgumentNullException(nameof(memory));
            if (!_ids.Add(memory.Id)) throw new InvalidOperationException("A memory ID cannot be indexed twice.");
            _memories.Add(memory);
        }

        public IReadOnlyList<SubjectiveMemory> Recent(IndividualId ownerId, int maximum)
        {
            ValidateMaximum(maximum);
            return ReadOnly(_memories
                .Where(memory => memory.OwnerId == ownerId)
                .OrderByDescending(memory => memory.EncodedAtUtc)
                .ThenBy(memory => memory.Id.Value)
                .Take(maximum));
        }

        public IReadOnlyList<SubjectiveMemory> MostSignificant(IndividualId ownerId, int maximum)
        {
            ValidateMaximum(maximum);
            return ReadOnly(_memories
                .Where(memory => memory.OwnerId == ownerId)
                .OrderByDescending(memory => memory.Importance)
                .ThenByDescending(memory => memory.EmotionalWeight)
                .ThenByDescending(memory => memory.EncodedAtUtc)
                .ThenBy(memory => memory.Id.Value)
                .Take(maximum));
        }

        public IReadOnlyList<SubjectiveMemory> Snapshot()
        {
            return new ReadOnlyCollection<SubjectiveMemory>(new List<SubjectiveMemory>(_memories));
        }

        private static void ValidateMaximum(int maximum)
        {
            if (maximum < 1 || maximum > 1000) throw new ArgumentOutOfRangeException(nameof(maximum));
        }

        private static IReadOnlyList<SubjectiveMemory> ReadOnly(IEnumerable<SubjectiveMemory> values)
        {
            return new ReadOnlyCollection<SubjectiveMemory>(values.ToList());
        }
    }
}
