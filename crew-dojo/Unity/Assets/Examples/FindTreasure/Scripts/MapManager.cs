using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Dojo.Netcode;

namespace Examples.FindTreasure
{
    [RequireComponent(typeof(DojoNetcodeObjectPool))]
    public class MapManager : MonoBehaviour
    {
        private const string LOGSCOPE = "MapManager";

        private DojoNetcodeObjectPool _pool;

        [SerializeField]
        private Transform _ground;

        public List<List<int>> MapObstacles { get; private set; } = new();
        public List<Vector2Int> PlayerSpawnPoints { get; private set; } = new();
        public List<Vector2Int> TreasureSpawnPoints { get; private set; } = new();
        private readonly List<Tuple<NetworkObject, GameObject>> _obstacles = new();

        private Vector2Int current_treasure = new(0, 0);
        private Vector2Int last_treasure = new(0, 0);

        public event Action OnMapReady;

        // 0 for empty space
        // x for spawn point of player
        // y for spawn point of treasure
        // 1,2,3,4 are 4 possible obstacles
        public const string DEFAULT_MAP =
// @"
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,y,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,1,1,1,1,1,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,1,0,0,0,0,0,1,0,0,0,0,0,0,0,
// 0,0,0,0,0,1,0,0,0,0,0,0,0,1,0,0,x,0,0,0,
// 0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,1,0,0,0,0,0,0,0,0,x,0,0,0,0,0,0,
// 0,0,0,0,1,0,0,0,x,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,1,0,0,0,0,0,x,0,0,0,0,0,0,x,0,0,
// 0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,1,0,0,0,0,0,0,0,1,0,0,0,0,0,0,
// 0,0,0,0,0,0,1,0,0,0,0,0,1,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,1,1,1,1,1,0,0,0,0,0,0,x,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,x,0,0,0,0,0,
// 0,0,0,0,0,x,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,x,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// ";
@"
-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
-1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,-1,
-1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,-1,
-1,0,0,0,0,0,0,-1,-1,-1,-1,-1,0,0,0,0,0,0,0,-1,
-1,0,0,0,0,0,-1,1,1,1,1,1,-1,0,0,0,0,0,0,-1,
-1,0,0,0,0,-1,1,-1,-1,-1,-1,-1,1,-1,0,0,0,0,0,-1,
-1,0,0,0,-1,1,-1,0,0,0,0,0,-1,1,-1,0,0,0,0,-1,
-1,0,0,-1,1,-1,0,0,0,0,0,0,0,-1,0,0,0,0,0,-1,
-1,0,0,-1,1,-1,0,0,0,0,0,0,0,0,0,0,0,0,0,-1,
-1,0,0,-1,1,-1,0,0,0,0,0,0,0,0,0,0,0,0,0,-1,
-1,0,0,-1,1,-1,0,0,0,0,0,0,0,0,0,0,0,0,0,-1,
-1,0,0,-1,1,-1,0,0,0,0,0,0,0,0,0,0,0,0,0,-1,
-1,0,0,-1,1,-1,0,0,0,0,0,0,0,-1,0,0,0,0,0,-1,
-1,0,0,0,-1,1,-1,0,0,0,0,0,-1,1,-1,0,0,0,0,-1,
-1,0,0,0,0,-1,1,-1,-1,-1,-1,-1,1,-1,0,0,0,0,0,-1,
-1,0,0,0,0,0,-1,1,1,1,1,1,-1,0,0,0,0,0,0,-1,
-1,0,0,0,0,0,0,-1,-1,-1,-1,-1,0,0,0,0,0,0,0,-1,
-1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,-1,
-1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,-1,
-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
";
// @"
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,                                                           
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// ";

// @"
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,                                                           
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,1,1,1,1,1,1,1,1,1,1,0,0,0,0,0,
// 0,0,0,0,0,1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,
// 0,0,0,0,0,1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,
// 0,0,0,0,0,1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,
// 0,0,0,0,0,1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,
// 0,0,0,0,0,1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,
// 0,0,0,0,0,1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,
// 0,0,0,0,0,1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,
// 0,0,0,0,0,1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,
// 0,0,0,0,0,1,1,1,1,1,1,1,1,1,1,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
// ";


        public int NumRows { get; private set; } = 0;
        public int NumCols { get; private set; } = 0;
        public float NumRowsHalf { get; private set; } = 0;
        public float NumColsHalf { get; private set; } = 0;
        public Vector3 GroundScale { get; private set; } = Vector3.zero;

        private void Awake()
        {
            _pool = GetComponent<DojoNetcodeObjectPool>();

            var scale = _ground.localScale;
            _ground.localScale.Set(scale.x, 1.0f, scale.z);

            var args = Environment.GetCommandLineArgs();

            for (var idx = 0; idx < args.Length; ++idx)
            {
                var arg = args[idx];
                if (arg.Equals("-Seed") && idx < args.Length - 1 && float.TryParse(args[idx + 1], out var random_seed))
                {
                    UnityEngine.Random.seed = (int)random_seed;
                    Debug.Log($"{LOGSCOPE}: Random seed set to {random_seed}");
                    ++idx;
                }
            }
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
                    .Where(val => !string.IsNullOrEmpty(val) && (val == "x" || val == "y" || int.TryParse(val, out _)))
                    .Select(val =>
                    {
                        if (val == "x")
                            return -1;
                        else if (val == "y")
                            return -2;
                        return int.Parse(val);
                    }).ToList();
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

            // scan player spawn points
            AddObstaclesToPoints(PlayerSpawnPoints, 0);
            Debug.Assert(PlayerSpawnPoints.Count > 0, $"{LOGSCOPE}: Should have at least 1 player spawn point!");
            // scan treasure spawn points
            AddObstaclesToPoints(TreasureSpawnPoints, 0);
            Debug.Assert(TreasureSpawnPoints.Count > 0, $"{LOGSCOPE}: Should have at least 1 treasure spawn point!");

            // allocate obstacles
            ClearObstacles();
            AllocateObstacles();

            // invoke ready
            OnMapReady?.Invoke();

            return true;
        }

        private void AddObstaclesToPoints(List<Vector2Int> points, int obstacleId)
        {
            points.Clear();
            for (var rowId = 0; rowId < NumRows; ++rowId)
            {
                for (var colId = 0; colId < NumCols; ++colId)
                {
                    if (MapObstacles[rowId][colId] == obstacleId)
                    {
                        points.Add(new(rowId, colId));
                    }
                }
            }
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

                        netObj.transform.SetLocalPositionAndRotation(
                            new Vector3(
                                (colId - NumColsHalf + 0.5f) * GroundScale.x,
                                netObj.transform.localPosition.y,
                                (NumRowsHalf - rowId - 0.5f) * GroundScale.z
                            ),
                            netObj.transform.localRotation
                        );
                        var scale = netObj.transform.localScale;
                        netObj.transform.localScale = new Vector3(GroundScale.x, scale.y, GroundScale.z);
                    }
                }
            }
        }


        private Bounds FindSpawnPoint(List<Vector2Int> SpawnPoints, float yPos, bool isTreasure = false)
        {

            if (!isTreasure)
            {
                SpawnPoints = SpawnPoints.Where(point => Math.Pow(point.x - current_treasure.x, 2) + Math.Pow(point.y - current_treasure.y, 2) > 144).ToList();
            }
            else
            {

            }
            var rndIdx = UnityEngine.Random.Range(0, SpawnPoints.Count);

            var rndPos = SpawnPoints[rndIdx];

            if (isTreasure)
            {
                last_treasure = current_treasure;
                current_treasure = rndPos;
            }
            else
            {
            }

            var bounding = new Bounds(
                new(
                    (rndPos.y - NumColsHalf + 0.5f) * GroundScale.x,
                    yPos,
                    (NumRowsHalf - rndPos.x - 0.5f) * GroundScale.z
                ),
                Vector3.one
            );
            return bounding;
        }

        public Bounds FindSpawnPointForPlayer()
        {
            return FindSpawnPoint(PlayerSpawnPoints, 0.0f);
        }

        public Bounds FindSpawnPointForTreasure()
        {
            return FindSpawnPoint(TreasureSpawnPoints, -0.5f, true);
        }
    }
}
