using System.Reflection;
using Landfall.Modding;
using UnityEngine;
using Zorro.Core;
using Zorro.Core.CLI;
using Newtonsoft.Json;
using Random = System.Random;
using NodeType = LevelSelectionNode.NodeType;

namespace SeedExplorer;

// Extension methods for Vector3
public static class Vector3Extensions {
    public static Vector2 xz(this Vector3 vector) {
        return new Vector2(vector.x, vector.z);
    }
}

/// <summary>
/// SeedExplorer plugin class. This is the entry point for the mod.
/// </summary>
[LandfallPlugin]
public class SeedExplorerProgram {
    /// <summary>
    /// SeedExplorer plugin name. This is used for the settings tab.
    /// </summary>
    /// <returns>The name of the plugin.</returns>
    public static string GetCategory() => "SeedExplorer";

    static SeedExplorerProgram() {
        Debug.Log("SeedExplorer: Hello World!");
    }

    [ConsoleCommand]
    public static void PrintHelloWorld() {
        Debug.Log("SeedExplorer: Hello World!");
    }

    [Serializable]
    public class SE_Node {
        public int ID;
        public int Depth;
        public Vector3 Position;
        public NodeType Type;
        public string Notes = string.Empty;

        public override string ToString() {
            return $"ID: {ID}, Depth: {Depth}, Type: {Type}, Position: {Position}";
        }
    }

    [Serializable]
    public class SE_Path {
        public int FromNodeID;
        public int ToNodeID;

        public override string ToString() {
            return $"From: {FromNodeID}, To: {ToNodeID}";
        }
    }

    [Serializable]
    public class SE_Info {
        [SerializeField]
        public List<SE_Node> nodes = new();
        [SerializeField]
        public List<SE_Path> paths = new();

        public int shard;
        public int seed;

        public override string ToString() {
            return string.Join('\n',
                $"Nodes: {nodes.Count}, Paths: {paths.Count}",
                string.Join('\n', nodes.Select(n => n.ToString())),
                string.Join('\n', paths.Select(p => p.ToString())));
        }
    }

    public static void GenerateSimplified(
        int depth,
        Random random,
        SE_Info infos
    ) {
        List<SE_Node> nodes = infos.nodes;
        List<SE_Path> paths = infos.paths;

        // Generate nodes 
        SpawnNode(Vector3.up * 10f, 0, nodes, NodeType.Default, "Start node");
        for (int depth1 = 1; depth1 <= depth; ++depth1) {
            int num = random.Next(2, 4);
            for (int index1 = 0; index1 < num; ++index1) {
                for (int index2 = 0; index2 < 15; ++index2) {
                    Vector3 vector3 = new Vector3(random.Range(-20f, 20f), 10f, random.Range(0.0f, 2f) + depth1 * 7f);
                    if (IsValidPosition(vector3, nodes)) {
                        SpawnNode(vector3, depth1, nodes, NodeType.Default, "Init node");
                        break;
                    }
                }
            }
        }

        // Generate paths
        SE_Node from1 = nodes.First();
        foreach (SE_Node to in nodes.FindAll(x => x.Depth == 1))
            paths.Add(new SE_Path { FromNodeID = from1.ID, ToNodeID = to.ID });

        for (int i = 1; i < depth; i++) {
            List<SE_Node> all1 = nodes.FindAll(x => x.Depth == i);
            List<SE_Node> all2 = nodes.FindAll(x => x.Depth == i + 1);
            foreach (SE_Node from2 in all1) {
                IEnumerable<SE_Node> nodesSortedByClose = GetNodesSortedByClose(all2, from2.Position);
                bool flag = false;
                foreach (SE_Node to in nodesSortedByClose) {
                    if (!DoesPathsIntersect(from2.Position, to.Position, paths, nodes)) {
                        paths.Add(new SE_Path { FromNodeID = from2.ID, ToNodeID = to.ID });
                        flag = true;
                        break;
                    }
                }
                if (!flag) {
                    SE_Node randomNode = random.Choice(all2);
                    paths.Add(new SE_Path { FromNodeID = from2.ID, ToNodeID = randomNode.ID });
                }
            }

            foreach (SE_Node unconnectedNode in GetUnconnectedNodes(all2, paths)) {
                IEnumerable<SE_Node> nodesSortedByClose = GetNodesSortedByClose(all1, unconnectedNode.Position);
                bool flag = false;
                foreach (SE_Node from3 in nodesSortedByClose) {
                    if (!DoesPathsIntersect(from3.Position, unconnectedNode.Position, paths, nodes)) {
                        paths.Add(new SE_Path { FromNodeID = from3.ID, ToNodeID = unconnectedNode.ID });
                        flag = true;
                        break;
                    }
                }
                if (!flag) {
                    SE_Node randomNode = random.Choice(all1);
                    paths.Add(new SE_Path { FromNodeID = randomNode.ID, ToNodeID = unconnectedNode.ID });
                }
            }

            foreach (SE_Node from4 in all1) {
                if (random.NextFloat() < 0.25) {
                    foreach (SE_Node to in GetNodesSortedByClose(all2, from4.Position).Reverse()) {
                        if (!DoesPathsIntersect(from4.Position, to.Position, paths, nodes)) {
                            paths.Add(new SE_Path { FromNodeID = from4.ID, ToNodeID = to.ID });
                            break;
                        }
                    }
                }
            }
        }

        // Add boss node
        SE_Node to1 = SpawnNode(new Vector3(0.0f, 10f, nodes.Last().Position.z + 15f), depth + 1, nodes, NodeType.Boss, "Boss node");
        foreach (SE_Node from5 in nodes.FindAll(x => x.Depth == depth))
            paths.Add(new SE_Path { FromNodeID = from5.ID, ToNodeID = to1.ID });

        // Assign node types
        MakeShops(nodes, paths, random, depth);
        SetPercentageRandomToType(NodeType.Challenge, 0.07f);
        SetPercentageRandomToType(NodeType.Encounter, 0.1f);
        SetPercentageRandomToType(NodeType.RestStop, 0.07f);
        SetPercentageRandomToType(NodeType.Shop, 0.02f);

        for (int index = 0; index < 10; ++index) {
            foreach (SE_Path path in paths) {
                SE_Node fromNode = nodes[path.FromNodeID];
                SE_Node toNode = nodes[path.ToNodeID];
                Vector3 vector3 = fromNode.Position - toNode.Position;

                if (fromNode.Depth > 0)
                    fromNode.Position += Vector3.left * vector3.x * 0.03f;

                if (toNode.Depth > 0)
                    toNode.Position -= Vector3.left * vector3.x * 0.03f;

                List<SE_Node> sameDepthNodes = nodes.FindAll(x => x.Depth == fromNode.Depth);
                foreach (SE_Node otherNode in sameDepthNodes) {
                    if (otherNode.ID != fromNode.ID) {
                        float direction = -Mathf.Sign(otherNode.Position.x - fromNode.Position.x);
                        float distance = Mathf.Abs(otherNode.Position.x - fromNode.Position.x);
                        float threshold = 10f;
                        float adjustment = Mathf.InverseLerp(threshold, 0.0f, distance);
                        fromNode.Position += Vector3.right * adjustment * direction * threshold * 0.25f;
                    }
                }
            }
        }

        foreach (SE_Path path in paths) {
            foreach (SE_Path otherPath in paths) {
                if (path != otherPath && Math2DUtility.AreLinesIntersecting(
                    nodes[path.FromNodeID].Position.xz(),
                    nodes[path.ToNodeID].Position.xz(),
                    nodes[otherPath.FromNodeID].Position.xz(),
                    nodes[otherPath.ToNodeID].Position.xz(),
                    false)) {
                    Debug.Log("Intersection detected");
                }
            }
        }

        void SetPercentageRandomToType(
            NodeType type,
            float percentage
        ) {
            int num = Mathf.RoundToInt(nodes.Count * percentage);
            for (int index = 0; index < num; ++index) {
                List<SE_Node> validNodes = nodes
                    .Skip(1)
                    .Where(n => AllowedToConvertNode(n, type, nodes, paths))
                    .ToList();

                if (validNodes.Count == 0)
                    break;

                SE_Node selectedNode = random.Choice(validNodes);
                selectedNode.Type = type;
                selectedNode.Notes = $"Generated in SetPercentageRandomToType({type}) (p: {percentage}, i: {index})";
            }
        }
    }

    private static void MakeShops(
        List<SE_Node> nodes,
        List<SE_Path> paths,
        Random random,
        int depth
    ) {
        for (int i = 3; i < depth; i += 4)
            SetShopAtDepth(i);
        SetShopAtDepth(depth);

        void SetShopAtDepth(int d) {
            SE_Node[] array = nodes.Where(n => n.Depth == d).ToArray();
            if (array.Length == 0)
                return;
            SE_Node levelSelectionNode = random.Choice(array);
            foreach (SE_Node node in array) {
                if ((node == levelSelectionNode || random.NextFloat() > 0.5) && AllowedToConvertNode(node, NodeType.Shop, nodes, paths)) {
                    node.Type = NodeType.Shop;
                    node.Notes = $"Generated in MakeShops ({d})";
                }
            }
        }
    }

    private static SE_Node SpawnNode(
        Vector3 pos,
        int depth,
        List<SE_Node> nodes,
        NodeType type,
        string notes = ""
    ) {
        SE_Node node = new SE_Node {
            ID = nodes.Count,
            Depth = depth,
            Position = pos,
            Type = type,
            Notes = notes
        };
        nodes.Add(node);
        return node;
    }

    private static bool IsValidPosition(Vector3 spawnPos, List<SE_Node> nodes) {
        foreach (SE_Node node in nodes) {
            if (Vector3.Distance(node.Position, spawnPos) < 5.0)
                return false;
        }
        return true;
    }

    private static List<SE_Node> GetUnconnectedNodes(
        List<SE_Node> nodes,
        List<SE_Path> paths
    ) {
        return nodes.FindAll(x => paths.All(y => y.FromNodeID != x.ID && y.ToNodeID != x.ID));
    }

    private static IEnumerable<SE_Node> GetNodesSortedByClose(
        List<SE_Node> nodes,
        Vector3 pos
    ) {
        return nodes.OrderBy(x => Vector3.Distance(pos, x.Position));
    }

    private static bool DoesPathsIntersect(
        Vector3 start,
        Vector3 end,
        List<SE_Path> paths,
        List<SE_Node> nodes
    ) {
        foreach (SE_Path path in paths) {
            Vector3 pathStart = nodes[path.FromNodeID].Position;
            Vector3 pathEnd = nodes[path.ToNodeID].Position;
            if (Math2DUtility.AreLinesIntersecting(pathStart.xz(), pathEnd.xz(), start.xz(), end.xz(), false))
                return true;
        }
        return false;
    }

    private static bool AllowedToConvertNode(
        SE_Node node,
        NodeType type,
        List<SE_Node> nodes,
        List<SE_Path> paths
    ) {
        return node.Type == NodeType.Default &&
               !WouldConvertingProduceDoubleNode(node, type, nodes, paths) &&
               (type == NodeType.Challenge || ConvertingWouldProduceStreakOf(node, nodes, paths) <= 2) &&
               (type != NodeType.Shop || node.Depth >= 3);
    }

    private static bool WouldConvertingProduceDoubleNode(
        SE_Node node,
        NodeType type,
        List<SE_Node> nodes,
        List<SE_Path> paths
    ) {
        foreach (SE_Path path in paths) {
            SE_Node? connectedNode = 
                path.FromNodeID == node.ID ? nodes[path.ToNodeID] : 
                path.ToNodeID == node.ID ? nodes[path.FromNodeID] : null;

            if (connectedNode != null && connectedNode.Type == type)
                return true;
        }
        return false;
    }

    private static int ConvertingWouldProduceStreakOf(
        SE_Node node,
        List<SE_Node> nodes,
        List<SE_Path> paths
    ) {
        return Count(node, true) + Count(node, false) + 1;

        int Count(SE_Node n, bool forwards) {
            int maxStreak = 0;
            foreach (SE_Path path in paths) {
                if ((forwards ? path.FromNodeID : path.ToNodeID) != n.ID)
                    continue;
                int otherNodeID = forwards ? path.ToNodeID : path.FromNodeID;
                switch (nodes[otherNodeID].Type) {
                    case NodeType.Shop:
                    case NodeType.Encounter:
                    case NodeType.RestStop:
                        maxStreak = Mathf.Max(maxStreak, Count(nodes[otherNodeID], forwards) + 1);
                        break;
                }
            }
            return maxStreak;
        }
    }

    public static string SFInfoToJSON(SE_Info infos, Formatting formatting) {
        return JsonConvert.SerializeObject(new {
            infos.shard,
            infos.seed,
            nodes = infos.nodes.Select(n => new {
                id = n.ID,
                depth = n.Depth,
                type = n.Type
            }),
            paths = infos.paths.Select(p => new {
                from = p.FromNodeID,
                to = p.ToNodeID
            })
        }, formatting);
    }

    public static void PrintSFInfoToFile(SE_Info infos, bool indented = true) {
        string json = SFInfoToJSON(infos, indented ? Formatting.Indented : Formatting.None);
        string path = Path.Combine(Application.persistentDataPath, "SeedExplorer", $"SeedExplorer_{infos.shard}_{infos.seed}.json");
        if (File.Exists(path)) {
            Debug.LogWarning($"SeedExplorer: File {path} already exists. Overwriting.");
        }
        new FileInfo(path).Directory?.Create();
        File.WriteAllText(path, json);
        Debug.Log($"SeedExplorer: JSON saved to {path}");
    }

    public static void PrintSFInfoToLogs(SE_Info infos) {
        string json = SFInfoToJSON(infos, Formatting.Indented);
        Debug.Log($"SeedExplorer: JSON: Shard {infos.shard} | Seed {infos.seed}\n{json}");
    }

    public static SE_Info SFInfoFromCurrent() {
        SE_Info infos = new();
        infos.shard = RunHandler.RunData.shardID;
        infos.seed = RunHandler.RunData.currentSeed;
        LevelSelectionHandler handler = GameObject.FindObjectOfType<LevelSelectionHandler>();
        if (handler == null) {
            Debug.LogError("SeedExplorer: LevelSelectionHandler not found");
            return null!;
        }
        foreach (LevelSelectionNode node in handler.Nodes) {
            infos.nodes.Add(new SE_Node {
                ID = node.ID,
                Depth = node.Depth,
                Position = node.transform.position,
                Type = node.Type,
                Notes = "CustomFromCurrent()"
            });
        }
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo field = typeof(LevelSelectionHandler).GetField("m_levelSelectionPaths", flags);
        if (field == null) {
            Debug.LogError("SeedExplorer: Field m_levelSelectionPaths not found in LevelSelectionNode");
            return null!;
        }
        List<LevelSelectionPath> officialPaths = (List<LevelSelectionPath>)field.GetValue(handler);
        foreach (LevelSelectionPath path in officialPaths) {
            infos.paths.Add(new SE_Path { FromNodeID = path.From.ID, ToNodeID = path.To.ID });
        }
        return infos;
    }

    public static SE_Info SFInfoFromGeneration(int shardID, int seed) {
        string[] names = ObjectDatabaseAsset<RunConfigDatabase, RunConfig>.GetAllObjectNames();
        string name = Array.Find(names, n => n.Contains(shardID.ToString()));
        if (string.IsNullOrEmpty(name)) {
            Debug.LogError($"SeedExplorer: No RunConfig found for shard {shardID}");
            return null!;
        }
        RunConfig runConfig = ObjectDatabaseAsset<RunConfigDatabase, RunConfig>.GetObjectFromString(name.WithoutWhitespace());
        Random random = new (seed);
        SE_Info infos = new();
        infos.shard = shardID;
        infos.seed = seed;
        GenerateSimplified(runConfig.nrOfLevels, random, infos);
        return infos;
    }

    internal static bool Compare(SE_Node custom, LevelSelectionNode official) {
        return custom.ID == official.ID && custom.Depth == official.Depth && custom.Type == official.Type;
    }

    internal static bool Compare(LevelSelectionNode official, SE_Node custom) {
        return Compare(custom, official);
    }

    internal static bool Compare(SE_Node node1, SE_Node node2) {
        return node1.ID == node2.ID && node1.Depth == node2.Depth && node1.Type == node2.Type;
    }

    internal static bool Compare(SE_Path custom, LevelSelectionPath official) {
        return custom.FromNodeID == official.From.ID && custom.ToNodeID == official.To.ID;
    }

    internal static bool Compare(LevelSelectionPath official, SE_Path custom) {
        return Compare(custom, official);
    }

    internal static bool Compare(SE_Path path1, SE_Path path2) {
        return path1.FromNodeID == path2.FromNodeID && path1.ToNodeID == path2.ToNodeID;
    }

    public static void DebugCompareInfos(SE_Info info1, SE_Info info2, string name1, string name2) {
        Debug.Log($"SeedExplorer: Comparing {name1} and {name2}");

        Debug.Log($"SeedExplorer: Nodes: {name1}: {info1.nodes.Count}, {name2}: {info2.nodes.Count}");
        int i = 0;
        while (i < info1.nodes.Count && i < info2.nodes.Count) {
            SE_Node info1Node = info1.nodes[i];
            SE_Node info2Node = info2.nodes[i];
            if (Compare(info1Node, info2Node)) {
                Debug.Log($"SeedExplorer: Node[{i}] {name1} == {name2}");
            } else {
                Debug.Log($"SeedExplorer: Node[{i}] {name1} != {name2}");
                Debug.Log($"SeedExplorer: Node[{i}] {{\n" +
                    $"\tID: {name1} {info1Node.ID}, {name2} {info2Node.ID}\n" +
                    $"\tDepth: {name1} {info1Node.Depth}, {name2} {info2Node.Depth}\n" +
                    $"\tType: {name1} {info1Node.Type}, {name2} {info2Node.Type}\n" +
                    $"}}");
            }
            ++i;
        }
        while (i < info1.nodes.Count) {
            SE_Node info1Node = info1.nodes[i];
            Debug.Log($"SeedExplorer: Node[{i}] {name1} != {name2}");
            Debug.Log($"SeedExplorer: Node[{i}] {{\n" +
                $"\tID: {name1} {info1Node.ID}, {name2} null\n" +
                $"\tDepth: {name1} {info1Node.Depth}, {name2} null\n" +
                $"\tType: {name1} {info1Node.Type}, {name2} null\n" +
                $"}}");
            ++i;
        }
        while (i < info2.nodes.Count) {
            SE_Node info2Node = info2.nodes[i];
            Debug.Log($"SeedExplorer: Node[{i}] {name1} != {name2}");
            Debug.Log($"SeedExplorer: Node[{i}] {{\n" +
                $"\tID: {name1} null, {name2} {info2Node.ID}\n" +
                $"\tDepth: {name1} null, {name2} {info2Node.Depth}\n" +
                $"\tType: {name1} null, {name2} {info2Node.Type}\n" +
                $"}}");
            ++i;
        }

        Debug.Log($"SeedExplorer: Paths: {name1}: {info1.paths.Count}, {name2}: {info2.paths.Count}");
        i = 0;
        while (i < info1.paths.Count && i < info2.paths.Count) {
            SE_Path info1Path = info1.paths[i];
            SE_Path info2Path = info2.paths[i];
            if (Compare(info1Path, info2Path)) {
                Debug.Log($"SeedExplorer: Path[{i}] {name1} == {name2}");
            } else {
                Debug.Log($"SeedExplorer: Path[{i}] {name1} != {name2}");
                Debug.Log($"SeedExplorer: Path[{i}] {{\n" +
                    $"\tFrom: {name1} {info1Path.FromNodeID}, {name2} {info2Path.FromNodeID}\n" +
                    $"\tTo: {name1} {info1Path.ToNodeID}, {name2} {info2Path.ToNodeID}\n" +
                    $"}}");
            }
            ++i;
        }
        while (i < info1.paths.Count) {
            SE_Path info1Path = info1.paths[i];
            Debug.Log($"SeedExplorer: Path[{i}] {name1} != {name2}");
            Debug.Log($"SeedExplorer: Path[{i}] {{\n" +
                $"\tFrom: {name1} {info1Path.FromNodeID}, {name2} null\n" +
                $"\tTo: {name1} {info1Path.ToNodeID}, {name2} null\n" +
                $"}}");
            ++i;
        }
        while (i < info2.paths.Count) {
            SE_Path info2Path = info2.paths[i];
            Debug.Log($"SeedExplorer: Path[{i}] {name1} != {name2}");
            Debug.Log($"SeedExplorer: Path[{i}] {{\n" +
                $"\tFrom: {name1} null, {name2} {info2Path.FromNodeID}\n" +
                $"\tTo: {name1} null, {name2} {info2Path.ToNodeID}\n" +
                $"}}");
            ++i;
        }
        Debug.Log($"SeedExplorer: Finished comparing {name1} and {name2}");
    }

    [ConsoleCommand]
    public static void CurrentMapToFile(bool indented) {
        SE_Info infos = SFInfoFromCurrent();
        if (infos == null) {
            Debug.LogError("SeedExplorer: Failed to get current map info");
            return;
        }
        PrintSFInfoToFile(infos, indented);
    }

    [ConsoleCommand]
    public static void GenerateMapToFile(int shardID, int seed, bool indented) {
        SE_Info infos = SFInfoFromGeneration(shardID, seed);
        if (infos == null) {
            Debug.LogError($"SeedExplorer: Failed to generate map for shard {shardID} and seed {seed}");
            return;
        }
        PrintSFInfoToFile(infos, indented);
    }

    [ConsoleCommand]
    public static void BatchGenerateMapToFile(int shardID, int seedStart, int seedEnd, bool indented) {
        if (seedEnd < seedStart) {
            BatchGenerateMapToFile(shardID, seedEnd, seedStart, indented);
            return;
        }

        long maxSeed = seedEnd;

        if (false) { // Parallel generation is disabled for now (crashes the game)
            Parallel.For(seedStart, maxSeed + 1, GenerateAndLog);
        } else {
            for (long i = seedStart; i <= maxSeed; ++i) GenerateAndLog(i);
        }

        void GenerateAndLog(long longSeed) {
            int seed = (int) longSeed;
            GenerateMapToFile(shardID, seed, indented);
            Debug.Log($"SeedExplorer: Generated map for shard {shardID} and seed {seed}");
        }
    }

    [ConsoleCommand]
    public static void CurrentMapToLogs() {
        SE_Info infos = SFInfoFromCurrent();
        if (infos == null) {
            Debug.LogError("SeedExplorer: Failed to get current map info");
            return;
        }
        PrintSFInfoToLogs(infos);
    }

    [ConsoleCommand]
    public static void GenerateMapToLogs(int shardID, int seed) {
        SE_Info infos = SFInfoFromGeneration(shardID, seed);
        if (infos == null) {
            Debug.LogError($"SeedExplorer: Failed to generate map for shard {shardID} and seed {seed}");
            return;
        }
        PrintSFInfoToLogs(infos);
    }

    [ConsoleCommand]
    public static void BatchGenerateMapToLogs(int shardID, int seedStart, int seedEnd) {
        if (seedEnd < seedStart) {
            BatchGenerateMapToLogs(shardID, seedEnd, seedStart);
            return;
        }

        long maxSeed = seedEnd;

        if (false) { // Parallel generation is disabled for now (crashes the game)
            Parallel.For(seedStart, maxSeed + 1, GenerateAndLog);
        } else {
            for (long i = seedStart; i <= maxSeed; ++i) GenerateAndLog(i);
        }

        void GenerateAndLog(long longSeed) {
            int seed = (int) longSeed;
            GenerateMapToLogs(shardID, seed);
            Debug.Log($"SeedExplorer: Generated map for shard {shardID} and seed {seed}");
        }
    }

    public static void CurrentMapToAction(Action<SE_Info> action) {
        SE_Info infos = SFInfoFromCurrent();
        if (infos == null) {
            Debug.LogError("SeedExplorer: Failed to get current map info");
            return;
        }
        action(infos);
    }

    public static void GenerateMapToAction(int shardID, int seed, Action<SE_Info> action) {
        SE_Info infos = SFInfoFromGeneration(shardID, seed);
        if (infos == null) {
            Debug.LogError($"SeedExplorer: Failed to generate map for shard {shardID} and seed {seed}");
            return;
        }
        action(infos);
    }

    public static void BatchGenerateMapToAction(int shardID, int seedStart, int seedEnd, Action<SE_Info> action) {
        if (seedEnd < seedStart) {
            BatchGenerateMapToAction(shardID, seedEnd, seedStart, action);
            return;
        }

        long maxSeed = seedEnd;

        if (false) { // Parallel generation is disabled for now (crashes the game)
            Parallel.For(seedStart, maxSeed + 1, longSeed => {
                int seed = (int)longSeed;
                SE_Info infos = SFInfoFromGeneration(shardID, seed);
                if (infos != null) {
                    action(infos);
                }
            });
        } else {
            for (long i = seedStart; i <= maxSeed; ++i) {
                int seed = (int)i;
                SE_Info infos = SFInfoFromGeneration(shardID, seed);
                if (infos != null) {
                    action(infos);
                }
            }
        }
    }

    [ConsoleCommand]
    public static void CompareCurrentVSGenerated() {
        SE_Info current = SFInfoFromCurrent();
        if (current == null) {
            Debug.LogError("SeedExplorer: Failed to get current map info");
            return;
        }

        SE_Info generated = new();
        generated.shard = current.shard;
        generated.seed = current.seed;
        int depth = RunHandler.RunData.MaxLevels;
        GenerateSimplified(depth, new Random(generated.seed), generated);

        DebugCompareInfos(current, generated, "Current", "Generated");
    }

    [ConsoleCommand]
    public static void CompareSeeds(int shardID, int seed1, int seed2) {
        SE_Info generated1 = SFInfoFromGeneration(shardID, seed1);
        if (generated1 == null) {
            Debug.LogError($"SeedExplorer: Failed to generate map for shard {shardID} and seed {seed1}");
            return;
        }

        SE_Info generated2 = SFInfoFromGeneration(shardID, seed2);
        if (generated2 == null) {
            Debug.LogError($"SeedExplorer: Failed to generate map for shard {shardID} and seed {seed2}");
            return;
        }

        DebugCompareInfos(generated1, generated2, $"S{seed1}", $"S{seed2}");
    }
}
