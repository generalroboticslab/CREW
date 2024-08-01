using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Dojo.Netcode;

namespace Examples.HideAndSeek
{
    [RequireComponent(typeof(DojoNetcodeObjectPool))]
    public class MapManager : MonoBehaviour
    {
        private const string LOGSCOPE = "MapManager";

        private DojoNetcodeObjectPool _pool;

        [SerializeField]
        private Transform _ground;

        public List<List<int>> MapObstacles { get; private set; } = new();
        public List<Vector2Int> SpawnPoints { get; private set; } = new();
        private readonly List<Tuple<NetworkObject, GameObject>> _obstacles = new();

        public event Action OnMapReady;

        // 0 for empty space
        // x for spawn point for either hider or seeker
        // 1,2,3,4 are 4 possible obstacles
        public const string DEFAULT_MAP =
@"
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,1,1,1,1,1,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,1,0,0,0,0,0,1,0,0,0,0,0,0,0,
0,0,0,0,0,1,0,0,0,0,0,0,0,1,0,0,0,0,0,0,
0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,1,0,0,0,0,0,0,0,1,0,0,0,0,0,0,
0,0,0,0,0,0,1,0,0,0,0,0,1,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,1,1,1,1,1,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
";

        public int NumRows { get; private set; } = 0;
        public int NumCols { get; private set; } = 0;
        public float NumRowsHalf { get; private set; } = 0;
        public float NumColsHalf { get; private set; } = 0;
        public Vector3 GroundScale { get; private set; } = Vector3.zero;

        public List<Vector3> agent_positions = new();

        private void Awake()
        {
            _pool = GetComponent<DojoNetcodeObjectPool>();

            var scale = _ground.localScale;
            _ground.localScale.Set(scale.x, 1.0f, scale.z);
        }

        public bool LoadMap(string map)
        {
            // split by rows
            var lines = map.Split(Environment.NewLine).ToList();

            // filter out empty lines or comments
            lines = lines.Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line) && !line.StartsWith("//")).ToList();
            if (lines.Count == 0)
            {
                return false;
            }

            // validate and convert map
            var numRows = lines.Count;
            var numCols = -1;
            var tmpMap = new List<List<int>>();
            foreach (var line in lines)
            {
                var mapRow = line.Split(",")
                    .Where(val => !string.IsNullOrEmpty(val) && (val == "0" || int.TryParse(val, out _)))
                    .Select(val => val == "0" ? -1 : int.Parse(val)).ToList();
                if (numCols < 0)
                {
                    numCols = mapRow.Count;
                }
                if (mapRow.Count == 0 || numCols != mapRow.Count)
                {
                    return false;
                }
                tmpMap.Add(mapRow);
            }
            MapObstacles = tmpMap;

            // init map info
            NumRows = MapObstacles.Count;
            NumCols = MapObstacles[0].Count;
            NumRowsHalf = NumRows * 0.5f;
            NumColsHalf = NumCols * 0.5f;
            GroundScale = _ground.localScale * 20.0f;
            GroundScale = new(
                GroundScale.x / NumCols,
                GroundScale.y,
                GroundScale.z / NumRows
            );

            // scan spawn points
            agent_positions.Clear();
            SpawnPoints.Clear();
            for (var rowId = 0; rowId < numRows; ++rowId)
            {
                for (var colId = 0; colId < numCols; ++colId)
                {
                    if (MapObstacles[rowId][colId] == -1)
                    {
                        SpawnPoints.Add(new(rowId, colId));
                    }
                }
            }
            Debug.Assert(SpawnPoints.Count > 0, $"{LOGSCOPE}: Should have at least 1 spawn point!");

            // allocate obstacles
            ClearObstacles();
            AllocateObstacles();

            // invoke ready
            OnMapReady?.Invoke();

            return true;
        }

        private void ClearObstacles()
        {
            _obstacles.ForEach(obj => _pool.ReturnNetworkObject(obj.Item1, obj.Item2));
            _obstacles.Clear();
        }

        private void AllocateObstacles()
        {
            if (MapObstacles.Count == 0 || MapObstacles[0].Count == 0)
            {
                return;
            }

            for (var rowId = 0; rowId < NumRows; ++rowId)
            {
                for (var colId = 0; colId < NumCols; ++colId)
                {
                    var objIdx = MapObstacles[rowId][colId];
                    if (objIdx > 0)
                    {
                        var prefab = _pool.GetPrefabAt(objIdx - 1);
                        var netObj = _pool.GetNetworkObject(prefab);
                        _obstacles.Add(Tuple.Create(netObj, prefab));

                        var obj = netObj.gameObject;
                        var pos = obj.transform.localPosition;
                        obj.transform.localPosition = new Vector3(
                            (colId - NumColsHalf + 0.5f) * GroundScale.x,
                            pos.y,
                            (NumRowsHalf - rowId - 0.5f) * GroundScale.z

                        );
                        var scale = obj.transform.localScale;
                        obj.transform.localScale = new Vector3(GroundScale.x, scale.y, GroundScale.z);
                    }
                }
            }
        }
    }
}
