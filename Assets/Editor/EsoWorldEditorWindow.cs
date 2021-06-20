using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ESOWorld;
using System.IO;
using System;
using System.Text;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class EsoWorldEditorWindow : EditorWindow
{
    uint worldID;
    int pathCount;
    Dictionary<ulong, string> paths;

    [MenuItem("Window/ESOWorld")]
    static void Init() {
        EsoWorldEditorWindow window = (EsoWorldEditorWindow)EditorWindow.GetWindow(typeof(EsoWorldEditorWindow));
        window.Show();
    }

    private void OnGUI() {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Load Paths")) {
            paths = Util.LoadWorldFiles();
            pathCount = paths.Count;
        }
        EditorGUILayout.IntField(pathCount);
        EditorGUILayout.EndHorizontal();
        worldID = (uint)EditorGUILayout.IntField("World:", (int)worldID);
        if (GUILayout.Button("Import Models")) {
            GatherMeshes(worldID);
        }
        if (GUILayout.Button("Load Fixtures")) {
            LoadFixtures(worldID);
        }
        if (GUILayout.Button("Load Volumes")) {
            LoadVolumes(worldID);
        }
        if (GUILayout.Button("BVH Test")) {
            BVHTest();
        }
    }

    void LoadVolumes(uint worldID) {
        GameObject fixturePrefab = Resources.Load<GameObject>("VolumePrefab");
        Toc t = Toc.Read(paths[Util.WorldTocID(worldID)]);
        Layer l = t.layers[21];
        for (uint y = 0; y < l.cellsY; y++) {
            for (uint x = 0; x < l.cellsX; x++) {
                if (paths.ContainsKey(Util.WorldCellID(worldID, 21, x, y))) {
                    FixtureFile fixtures = FixtureFile.Open(paths[Util.WorldCellID(worldID, 21, x, y)]);
                    if (fixtures.volumes.Length == 0) continue;
                    Transform cell = new GameObject($"UNK {x},{y}:").transform;
                    cell.position = new Vector3(fixtures.fixtures[0].fixture.offsetX / 100, 0, fixtures.fixtures[0].fixture.offsetY / -100);
                    for (int i = 0; i < fixtures.volumes.Length; i++) {
                        GameObject o = (GameObject)Instantiate(fixturePrefab, cell);
                        o.transform.localPosition = new Vector3(fixtures.volumes[i].fixture.posX, fixtures.volumes[i].fixture.posY, fixtures.volumes[i].fixture.posZ * -1);
                        o.transform.localRotation = Quaternion.Euler(
                            (float)(fixtures.volumes[i].fixture.rotX * 180 / Math.PI),
                            (float)(fixtures.volumes[i].fixture.rotY * -180 / Math.PI + 180d),
                            (float)(fixtures.volumes[i].fixture.rotZ * -180 / Math.PI));
                        o.transform.localScale = new Vector3(fixtures.volumes[i].x, fixtures.volumes[i].y, fixtures.volumes[i].z);
                        o.name = fixtures.volumes[i].fixture.id.ToString();
                    }
                }
            }
        }
    }

    void BVHTest() {
        FixtureFile f = new FixtureFile(new BinaryReader(File.OpenRead(@"F:\Extracted\ESO\139\fixtures_3_3.fft")));
        uint i = 0;
        CreateTree(f.bvh1.root);
        CreateTree(f.bvh2.root);
        CreateTree(f.bvh3.root);
        CreateTree(f.bvh4.root);
        Debug.Log(i);
    }

    void CreateTree(RTreeNode node, Transform parent = null) {
        GameObject fixturePrefab = Resources.Load<GameObject>("TreePrefab");
        Transform nodeObj = Instantiate(fixturePrefab, new Vector3((node.bbox[0] + node.bbox[3])/2, (node.bbox[1] + node.bbox[4]) / 2, (node.bbox[2] + node.bbox[5]) / -2), Quaternion.identity).transform;
        if (node.nodes.Length == 0) {
            nodeObj.name = node.levelsBelow.ToString();
            BoxCollider b = nodeObj.gameObject.AddComponent<BoxCollider>();
            b.size = new Vector3(node.bbox[3] - node.bbox[0], node.bbox[4] - node.bbox[1], node.bbox[5] - node.bbox[2]);
        }
        if (parent != null) nodeObj.SetParent(parent, true);
        for (int i = 0; i < node.nodes.Length; i++) CreateTree(node.nodes[i], nodeObj);
    }

    void LoadFixtures(uint worldID) {
        if(paths == null || paths.Count < 100) paths = Util.LoadWorldFiles();

        GameObject fixturePrefab = Resources.Load<GameObject>("FixturePrefab");
        Material mat = Resources.Load<Material>("FixtureMat");
        Material clnmat = Resources.Load<Material>("CLNMat");

        //unneccecary?
        Dictionary<uint, string> meshnames = new Dictionary<uint, string>();
        foreach (string line in File.ReadAllLines(@"F:\Extracted\ESO\meshids.txt")) {
            string[] words = line.Split(' ');
            meshnames[UInt32.Parse(words[0])] = words[1];
        }

        Toc t = Toc.Read(paths[Util.WorldTocID(worldID)]);
        Layer l = t.layers[21];
        for (uint y = 0; y < l.cellsY; y++) {
            for (uint x = 0; x < l.cellsX; x++) {
                if (paths.ContainsKey(Util.WorldCellID(worldID, 21, x, y))) {
                    FixtureFile fixtures = FixtureFile.Open(paths[Util.WorldCellID(worldID, 21, x, y)]);
                    if (fixtures.fixtures.Length == 0) continue;
                    Transform cell = new GameObject($"CELL {x},{y}:").transform;
                    cell.position = new Vector3(fixtures.fixtures[0].fixture.offsetX / 100, 0, fixtures.fixtures[0].fixture.offsetY / -100);
                    for (int i = 0; i < fixtures.fixtures.Length; i++) {
                        if(meshnames.ContainsKey(fixtures.fixtures[i].model)) {
                            if (meshnames[fixtures.fixtures[i].model].StartsWith("VEG_") || meshnames[fixtures.fixtures[i].model].StartsWith("TRE_")
                                || meshnames[fixtures.fixtures[i].model].Contains("_INC_")) continue;
                        }
                        var prefab = Resources.Load(fixtures.fixtures[i].model.ToString());
                        if (prefab == null) prefab = fixturePrefab;
                        GameObject o = (GameObject) Instantiate(prefab, cell);
                        o.transform.localPosition = new Vector3(fixtures.fixtures[i].fixture.posX, fixtures.fixtures[i].fixture.posY, fixtures.fixtures[i].fixture.posZ * -1);
                        o.transform.localRotation = Quaternion.Euler(
                            (float)(fixtures.fixtures[i].fixture.rotX*180/Math.PI), 
                            (float)(fixtures.fixtures[i].fixture.rotY*-180/Math.PI+180d),
                            (float)(fixtures.fixtures[i].fixture.rotZ*-180/Math.PI));
                        o.name = fixtures.fixtures[i].fixture.id.ToString();
                        //o.name = meshnames.ContainsKey(fixtures.fixtures[i].model) ? $"{meshnames[fixtures.fixtures[i].model]}_{fixtures.fixtures[i].id}" : $"UNKNOWN_{fixtures.fixtures[i].id}";
                        foreach (var renderer in o.GetComponentsInChildren<MeshRenderer>()) {
                            if(renderer.gameObject.name.StartsWith("CLN")) renderer.sharedMaterial = clnmat;
                            else renderer.sharedMaterial = mat;
                        }
                    }
                } else
                    Debug.Log("MISSING FIXTURE FILE");
            }
        }
    }

    void GatherMeshes(uint worldID) {
        if (paths == null)  paths = Util.LoadWorldFiles();


        HashSet<uint> models = new HashSet<uint>();


        Toc t = Toc.Read(paths[Util.WorldTocID(worldID)]);
        Layer l = t.layers[21];
        for (uint y = 0; y < l.cellsY; y++) {
            for (uint x = 0; x < l.cellsX; x++) {
                if (paths.ContainsKey(Util.WorldCellID(worldID, 21, x, y))) {
                    FixtureFile fixtures = FixtureFile.Open(paths[Util.WorldCellID(worldID, 21, x, y)]);
                    if (fixtures.fixtures.Length == 0) continue;
                    for (int i = 0; i < fixtures.fixtures.Length; i++) {
                        if (models.Contains(fixtures.fixtures[i].model)) continue;
                        models.Add(fixtures.fixtures[i].model);
                    }
                } else
                    Debug.Log("MISSING FIXTURE FILE");
            }
        }
        //Debug.Log(models.Count);

        StringBuilder args = new StringBuilder();
        int exported = 0;
        foreach(uint model in models) {
            if (!File.Exists($@"F:\Anna\Files\Unity\esoworldedit\Assets\Resources\{model}.obj") &&
                File.Exists($@"F:\Extracted\ESO\Granny\{model}.gr2")) {
                args.Append($" \"F:\\Extracted\\ESO\\Granny\\{model}.gr2\"");
                exported++;
                if (exported >= 512) break;
            }
        }
        Debug.Log($"Exporting {exported} models");
        ProcessStartInfo info = new ProcessStartInfo() {
            FileName = @"F:\Anna\Visual Studio\gr2obj\x64\Release\gr2obj.exe",
            Arguments = args.ToString()
        };
        Process gr2obj = Process.Start(info);
        gr2obj.WaitForExit();
        foreach(string file in Directory.EnumerateFiles(@"F:\Anna\Files\Unity\esoworldedit\", "*.obj", SearchOption.TopDirectoryOnly)) {
            if (!File.Exists($@"F:\Anna\Files\Unity\esoworldedit\Assets\Resources\{Path.GetFileName(file)}"))
                File.Move(file, $@"F:\Anna\Files\Unity\esoworldedit\Assets\Resources\{Path.GetFileName(file)}");
            else File.Delete(file);
        }
        AssetDatabase.Refresh();

    }
}
