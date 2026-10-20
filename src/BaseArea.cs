using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BagOHeim
{
    public enum RangeMode
    {
        /// <summary>Your base: everything within range of the workbench(es) you're standing near.</summary>
        Workbench,
        /// <summary>A fixed straight-line distance from you.</summary>
        Distance
    }

    /// <summary>
    /// The area BagOHeim works in: which chests packs may refill from.
    /// </summary>
    internal class BaseArea
    {
        private const string WorkbenchName = "$piece_workbench";

        private readonly Vector3 _origin;
        private readonly float _distance;
        private readonly List<CraftingStation> _workbenches;

        private BaseArea(Vector3 origin, float distance, List<CraftingStation> workbenches)
        {
            _origin = origin;
            _distance = distance;
            _workbenches = workbenches;
        }

        /// <summary>False when the player isn't inside any base (workbench mode only).</summary>
        public bool IsValid => _workbenches == null || _workbenches.Count > 0;

        public static BaseArea For(Vector3 position)
        {
            if (BagOHeimPlugin.Mode.Value == RangeMode.Distance)
                return new BaseArea(position, BagOHeimPlugin.Range.Value, null);
            return new BaseArea(position, 0f, ConnectedWorkbenches(position));
        }

        public bool Contains(Vector3 point)
        {
            if (_workbenches == null) return Vector3.Distance(_origin, point) <= _distance;
            return _workbenches.Any(w => w != null && Vector3.Distance(w.transform.position, point) <= w.GetStationBuildRange());
        }

        public float DistanceFrom(Vector3 point) => Vector3.Distance(_origin, point);

        public string Describe() =>
            _workbenches == null ? $"within {BagOHeimPlugin.Range.Value:0} m" : "in this base";

        /// <summary>
        /// Workbenches whose build range (including upgrades) covers the position, plus any whose ranges overlap
        /// those (so a base with several workbenches counts as one).
        /// </summary>
        private static List<CraftingStation> ConnectedWorkbenches(Vector3 position)
        {
            var all = CraftingStation.m_allStations
                .Where(s => s != null && s.m_name == WorkbenchName)
                .ToList();

            var found = all.Where(s => Vector3.Distance(s.transform.position, position) <= s.GetStationBuildRange()).ToList();
            var queue = new Queue<CraftingStation>(found);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var other in all)
                {
                    if (found.Contains(other)) continue;
                    float reach = current.GetStationBuildRange() + other.GetStationBuildRange();
                    if (Vector3.Distance(current.transform.position, other.transform.position) <= reach)
                    {
                        found.Add(other);
                        queue.Enqueue(other);
                    }
                }
            }
            return found;
        }
    }
}
